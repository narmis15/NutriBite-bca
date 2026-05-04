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
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace NUTRIBITE.Controllers
{
    public class PaymentController : Controller
    {
        private readonly IRazorpayService _razorpayService;
        private readonly IPaymentDistributionService _distributionService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<PaymentController> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public PaymentController(
            IRazorpayService razorpayService,
            IPaymentDistributionService distributionService,
            IEmailService emailService,
            IConfiguration configuration,
            ApplicationDbContext db,
            ILogger<PaymentController> logger,
            IServiceScopeFactory scopeFactory)
        {
            _razorpayService = razorpayService ?? throw new ArgumentNullException(nameof(razorpayService));
            _distributionService = distributionService ?? throw new ArgumentNullException(nameof(distributionService));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
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
                    if (c.IsBulk)
                    {
                        var bulk = _db.BulkItems.FirstOrDefault(b => b.Id == c.Pid);
                        if (bulk != null)
                            subtotal += bulk.Price * c.Qty;
                        else
                            _logger.LogWarning("Cart item Pid {Pid} (Bulk) not found in BulkItems", c.Pid);
                    }
                    else
                    {
                        var food = _db.Foods.FirstOrDefault(f => f.Id == c.Pid);
                        if (food != null)
                            subtotal += food.Price * c.Qty;
                        else
                            _logger.LogWarning("Cart item Pid {Pid} (Regular) not found in Foods", c.Pid);
                    }
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
                return StatusCode(500, new { success = false, message = "Failed to create payment order: " + ex.Message, detail = ex.ToString() });
            }
        }

        // POST: /Payment/VerifyPayment
        // Accepts fields posted by client after checkout:
        // razorpay_payment_id, razorpay_order_id, razorpay_signature
        // optional localOrderId to link to your OrderTable
        [HttpPost]
        public async Task<IActionResult> VerifyPayment()
        {
            string paymentId = null;
            string orderId = null;
            string signature = null;
            string localOrderIdStr = null;

            // 1. Try reading from form if available
            if (Request.HasFormContentType)
            {
                paymentId = Request.Form["razorpay_payment_id"];
                orderId = Request.Form["razorpay_order_id"];
                signature = Request.Form["razorpay_signature"];
                localOrderIdStr = Request.Form["localOrderId"];
            }

            // 2. Try reading from JSON body if form fields are still empty
            if (string.IsNullOrWhiteSpace(paymentId) && Request.ContentLength > 0)
            {
                try
                {
                    // Enable buffering to allow reading the body multiple times if needed
                    Request.EnableBuffering();
                    using var reader = new System.IO.StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
                    var body = await reader.ReadToEndAsync();
                    Request.Body.Position = 0; // Reset position

                    _logger.LogInformation("VerifyPayment JSON Body: {Body}", body);

                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        using var json = JsonDocument.Parse(body);
                        if (json.RootElement.TryGetProperty("razorpay_payment_id", out var p)) paymentId = p.GetString();
                        if (json.RootElement.TryGetProperty("razorpay_order_id", out var o)) orderId = o.GetString();
                        if (json.RootElement.TryGetProperty("razorpay_signature", out var s)) signature = s.GetString();
                        
                        if (json.RootElement.TryGetProperty("localOrderId", out var l))
                        {
                            if (l.ValueKind == JsonValueKind.Number)
                                localOrderIdStr = l.GetInt32().ToString();
                            else if (l.ValueKind == JsonValueKind.String)
                                localOrderIdStr = l.GetString();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse JSON body for VerifyPayment");
                }
            }

            if (string.IsNullOrWhiteSpace(paymentId) || string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(signature))
            {
                _logger.LogWarning("Missing payment info: paymentId={P}, orderId={O}, signature={S}", paymentId, orderId, signature);
                return BadRequest(new { success = false, message = "Payment verification failed. Missing payment information" });
            }

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
                        if (c.IsBulk)
                        {
                            var bulk = _db.BulkItems.FirstOrDefault(b => b.Id == c.Pid);
                            if (bulk != null)
                                serverTotal += bulk.Price * c.Qty;
                        }
                        else
                        {
                            var food = _db.Foods.FirstOrDefault(f => f.Id == c.Pid);
                            if (food != null)
                                serverTotal += food.Price * c.Qty;
                        }
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
                    TransactionId = paymentId, // Store the gateway transaction ID
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
                        order.TrackingProgress = 1; // First tracking increment on "Placed"
                        order.TotalAmount = amountMajor; // Ensure TotalAmount is set from payment
                        order.DeliveryOTP = new Random().Next(100000, 999999); // Generate 6-digit OTP

                        // Trigger Distribution
                        // Requirements say "immediately after confirmed"
                        // Since we are in a controller, we'll await it for simplicity/atomicity as requested.
                        try
                        {
                            await _distributionService.DistributePaymentAsync(order.OrderId);
                            _logger.LogInformation("Payment distribution successful for order {OrderId}", order.OrderId);
                        }
                        catch (Exception dex)
                        {
                            _logger.LogError(dex, "Payment distribution failed for order {OrderId}", order.OrderId);
                            // We don't fail the verification if distribution fails, but log it for manual intervention
                        }

                        // ⭐ AUTOMATIC ACCEPTANCE LOGIC (3-5 seconds delay)
                        // This triggers vendor acceptance and automatic delivery agent assignment
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await Task.Delay(5000); // 5 seconds delay
                                using (var scope = _scopeFactory.CreateScope())
                                {
                                    var scopedOrderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
                                    await scopedOrderService.UpdateOrderStatusAsync(order.OrderId, "Accepted");
                                    _logger.LogInformation("Automatic order acceptance and agent assignment successful for order {OrderId}", order.OrderId);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Automatic order acceptance failed for order {OrderId}", order.OrderId);
                            }
                        });

                        // Clear cart
                        var cartItems = _db.Carttables.Where(c => c.Uid == order.UserId);
                        _db.Carttables.RemoveRange(cartItems);

                        // 📧 Send Confirmation Email
                        try
                        {
                            var user = await _db.UserSignups.FindAsync(order.UserId);
                            if (user != null && !string.IsNullOrEmpty(user.Email))
                            {
                                string subject = $"Order Confirmed! NutriBite #{order.OrderId}";
                                string body = $@"
                                    <div style='font-family: Poppins, sans-serif; padding: 20px; border: 1px solid #eef2f3; border-radius: 10px;'>
                                        <h2 style='color: #2d6a4f;'>Thank you for your order, {user.Name}!</h2>
                                        <p>Your order <strong>#{order.OrderId}</strong> has been successfully placed and is being prepared.</p>
                                        <div style='background: #f8f9fa; padding: 15px; border-radius: 8px; margin: 20px 0;'>
                                            <p style='margin: 0;'><strong>Total Amount:</strong> ₹{order.TotalAmount}</p>
                                            <p style='margin: 0;'><strong>Status:</strong> {order.Status}</p>
                                            <p style='margin: 0;'><strong>Delivery OTP:</strong> <span style='font-size: 1.25rem; font-weight: bold; color: #2d6a4f;'>{order.DeliveryOTP}</span></p>
                                        </div>
                                        <p>Please share this OTP with your delivery executive to receive your order securely.</p>
                                        <p>You can track your order live on our website.</p>
                                        <br>
                                        <p style='color: #888; font-size: 12px;'>This is an automated message from NutriBite.</p>
                                    </div>";
                                await _emailService.SendEmailAsync(user.Email, subject, body);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to send order confirmation email");
                        }
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
