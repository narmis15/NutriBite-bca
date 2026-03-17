using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using NUTRIBITE.Controllers;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NUTRIBITE.Models;
using NUTRIBITE.Services;
using Razorpay.Api;
using System.IO;
using System.Text.Json;
using System.Globalization;
using System.Linq;

namespace NUTRIBITE.Controllers
{
    public class PaymentController : Controller
    {
        private readonly IRazorpayService _razorpayService;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            IRazorpayService razorpayService,
            IConfiguration configuration,
            ApplicationDbContext db,
            ILogger<PaymentController> logger)
        {
            _razorpayService = razorpayService ?? throw new ArgumentNullException(nameof(razorpayService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // POST: /Payment/CreateOrder
        [HttpPost]
        public async Task<IActionResult> CreateOrder()
        {
            var uid = HttpContext.Session.GetInt32("UserId");

            if (!uid.HasValue)
                return Unauthorized(new { success = false, message = "Please login" });

            try
            {
                var cartRows = _db.Carttables.Where(c => c.Uid == uid.Value).ToList();

                if (!cartRows.Any())
                    return BadRequest(new { success = false, message = "Cart is empty" });

                decimal subtotal = 0m;

                foreach (var c in cartRows)
                {
                    var food = _db.Foods.FirstOrDefault(f => f.Id == c.Pid);

                    if (food != null)
                        subtotal += food.Price * c.Qty;
                    else
                        _logger.LogWarning("Cart item Pid {Pid} not found in Foods", c.Pid);
                }

                if (subtotal <= 0)
                    return BadRequest(new { success = false, message = "Cart total invalid" });

                var orderResult = await _razorpayService.CreateOrderAsync(
                    subtotal,
                    "INR",
                    receipt: $"uid_{uid.Value}_{DateTime.UtcNow:yyyyMMddHHmmss}"
                );

                var keyId = _configuration["Razorpay:KeyId"] ?? _configuration["RAZORPAY_KEY_ID"];

                return Json(new
                {
                    success = true,
                    orderId = orderResult.OrderId,
                    amount = orderResult.Amount,
                    amountMajor = orderResult.Amount / 100.0m,
                    currency = orderResult.Currency,
                    receipt = orderResult.Receipt,
                    keyId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateOrder failed");
                return StatusCode(500, new { success = false, message = "Failed to create payment order" });
            }
        }

        // POST: /Payment/VerifyPayment
        // Accepts fields posted by client after checkout:
        // razorpay_payment_id, razorpay_order_id, razorpay_signature
        // optional localOrderId to link to your OrderTable
        [HttpPost]
        public IActionResult VerifyPayment()
        {
            // read from form (Razorpay checkout posts form fields) or from JSON body
            string paymentId = Request.Form["razorpay_payment_id"];
            string orderId = Request.Form["razorpay_order_id"];
            string signature = Request.Form["razorpay_signature"];
            string localOrderIdStr = Request.Form["localOrderId"];

            // fallback: try JSON body if form fields empty
            if (string.IsNullOrWhiteSpace(paymentId) && Request.ContentLength > 0 && Request.ContentType?.Contains("application/json") == true)
            {
                try
                {
                    using var reader = new System.IO.StreamReader(Request.Body);
                    var body = reader.ReadToEnd();
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        var json = System.Text.Json.JsonDocument.Parse(body);
                        if (json.RootElement.TryGetProperty("razorpay_payment_id", out var p)) paymentId = p.GetString();
                        if (json.RootElement.TryGetProperty("razorpay_order_id", out var o)) orderId = o.GetString();
                        if (json.RootElement.TryGetProperty("razorpay_signature", out var s)) signature = s.GetString();
                        if (json.RootElement.TryGetProperty("localOrderId", out var l)) localOrderIdStr = l.GetRawText();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse JSON body for VerifyPayment");
                }
            }

            if (string.IsNullOrWhiteSpace(paymentId) || string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(signature))
                return BadRequest(new { success = false, message = "Missing payment information" });

            // get secret
            var keySecret = _configuration["Razorpay:KeySecret"] ?? _configuration["RAZORPAY_KEY_SECRET"];
            var keyId = _configuration["Razorpay:KeyId"] ?? _configuration["RAZORPAY_KEY_ID"];

            if (string.IsNullOrWhiteSpace(keySecret) || string.IsNullOrWhiteSpace(keyId))
            {
                _logger.LogError("Razorpay keys not configured for signature verification");
                return StatusCode(500, new { success = false, message = "Payment verification configuration error" });
            }

            try
            {
                // Compute expected signature: HMAC_SHA256(orderId + "|" + paymentId, keySecret)
                var payload = $"{orderId}|{paymentId}";
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(keySecret));
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
                var expectedSignature = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

                var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);
                var providedBytes = Encoding.UTF8.GetBytes(signature ?? "");

                if (!CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
                {
                    _logger.LogWarning("Razorpay signature mismatch. Order:{OrderId} Payment:{PaymentId}", orderId, paymentId);
                    return BadRequest(new { success = false, message = "Invalid signature" });
                }

                // Use Razorpay API to fetch payment details (ensures amount/order association are correct)
                var client = new RazorpayClient(keyId, keySecret);
                var payment = client.Payment.Fetch(paymentId);

                // Verify the payment's order id matches and status is captured
                string fetchedOrderId = Convert.ToString(payment["order_id"]);
                string status = Convert.ToString(payment["status"]);

                if (!string.Equals(fetchedOrderId, orderId, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Payment fetched order_id mismatch. expected=" + orderId + " fetched=" + fetchedOrderId);
                    return BadRequest(new { success = false, message = "Order mismatch" });
                }

                if (!string.Equals(status, "captured", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(status, "authorized", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Payment not captured/authorized. status=" + status);
                    return BadRequest(new { success = false, message = "Payment not captured" });
                }

                // Get amount from fetched payment (amount is in paise)
                var amountPaise = Convert.ToInt32(payment["amount"]);
                var amountMajor = amountPaise / 100.0m;
                var uid = HttpContext.Session.GetInt32("UserId");

                if (uid.HasValue)
                {
                    decimal serverTotal = 0m;

                    var cartRows = _db.Carttables.Where(c => c.Uid == uid.Value).ToList();

                    foreach (var c in cartRows)
                    {
                        var food = _db.Foods.FirstOrDefault(f => f.Id == c.Pid);
                        if (food != null)
                            serverTotal += food.Price * c.Qty;
                    }

                    if (Math.Abs(serverTotal - amountMajor) > 0.01m)
                    {
                        _logger.LogWarning("Payment amount mismatch");
                        return BadRequest(new { success = false, message = "Payment amount mismatch" });
                    }
                }

                // Parse localOrderId if provided
                int? localOrderId = null;
                if (int.TryParse(localOrderIdStr, out var parsedLocalId))
                    localOrderId = parsedLocalId;

                // Save payment to DB (link to local OrderTable when possible)
                var dbPayment = new NUTRIBITE.Models.Payment
                {
                    OrderId = localOrderId,
                    PaymentMode = "Razorpay",
                    Amount = amountMajor,
                    IsRefunded = false,
                    CreatedAt = DateTime.UtcNow
                };

                _db.Payments.Add(dbPayment);
                if (localOrderId.HasValue)
                {
                    var order = _db.OrderTables.FirstOrDefault(o => o.OrderId == localOrderId.Value);

                    if (order != null)
                    {
                        order.PaymentStatus = "Paid";
                        order.Status = "Placed"; // Change from Pending Payment to Placed

                        // Clear cart
                        var cartItems = _db.Carttables.Where(c => c.Uid == order.UserId);
                        _db.Carttables.RemoveRange(cartItems);
                    }
                }
                _db.SaveChanges();

                // Return success (frontend may redirect to PaymentSuccess with localOrderId)
                return Json(new { success = true, amount = amountMajor, orderId = orderId, paymentId = paymentId, localOrderId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during payment verification");
                return StatusCode(500, new { success = false, message = "Payment verification failed" });
            }
        }

        // GET: /Payment/Success
        [HttpGet]
        public IActionResult PaymentSuccess(int? localOrderId = null)
        {
            ViewBag.LocalOrderId = localOrderId;
            return View();
        }
    }
}
