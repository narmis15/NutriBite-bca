using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using global::NUTRIBITE.Services;
using global::NUTRIBITE.Models;
using Razorpay.Api;

namespace NUTRIBITE.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IActivityLogger _activityLogger;
        private readonly IEmailService _emailService;
        private readonly IPaymentDistributionService _distributionService;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _config;

        public CartController(ApplicationDbContext context, IActivityLogger activityLogger, IEmailService emailService, IPaymentDistributionService distributionService, Microsoft.Extensions.Configuration.IConfiguration config)
        {
            _context = context;
            _activityLogger = activityLogger;
            _emailService = emailService;
            _distributionService = distributionService;
            _config = config;
        }

        // ================= ADD TO CART =================
        [HttpPost]
        public async System.Threading.Tasks.Task<IActionResult> AddToCart(AddToCartRequest? request = null, int? foodId = null, int? productId = null, int? quantity = null, bool isBulk = false)
        {
            // Robust ID resolution
            int finalProductId = 0;
            int finalQuantity = 1;
            bool finalIsBulk = false;
            string? itemName = null;
            decimal? price = null;
            string? category = null;
            string? description = null;

            // 1. Try from request object (Model bound)
            if (request != null && (request.ProductId > 0 || !string.IsNullOrEmpty(request.ItemName)))
            {
                finalProductId = request.ProductId;
                finalQuantity = request.Quantity > 0 ? request.Quantity : 1;
                finalIsBulk = request.IsBulk;
                itemName = request.ItemName;
                price = request.Price;
                category = request.Category;
                description = request.Description;
            }
            // 2. Try from individual parameters (Form-data / Query)
            else if (productId.HasValue && productId.Value > 0)
            {
                finalProductId = productId.Value;
                finalQuantity = quantity.HasValue && quantity.Value > 0 ? quantity.Value : 1;
                finalIsBulk = isBulk;
            }
            else if (foodId.HasValue && foodId.Value > 0)
            {
                finalProductId = foodId.Value;
                finalQuantity = quantity.HasValue && quantity.Value > 0 ? quantity.Value : 1;
                finalIsBulk = isBulk;
            }
            // 3. Fallback: Manually parse JSON if it's a JSON request
            else if (Request.ContentType != null && Request.ContentType.Contains("application/json"))
            {
                try
                {
                    Request.EnableBuffering();
                    Request.Body.Position = 0;
                    using (var reader = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8, true, 1024, true))
                    {
                        var body = await reader.ReadToEndAsync();
                        if (!string.IsNullOrEmpty(body))
                        {
                            var jsonRequest = System.Text.Json.JsonSerializer.Deserialize<AddToCartRequest>(body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (jsonRequest != null)
                            {
                                finalProductId = jsonRequest.ProductId;
                                finalQuantity = jsonRequest.Quantity > 0 ? jsonRequest.Quantity : 1;
                                finalIsBulk = jsonRequest.IsBulk;
                                itemName = jsonRequest.ItemName;
                                price = jsonRequest.Price;
                                category = jsonRequest.Category;
                                description = jsonRequest.Description;
                            }
                        }
                    }
                    Request.Body.Position = 0;
                }
                catch { }
            }

            var uid = HttpContext.Session.GetInt32("UserId");
            if (!uid.HasValue)
            {
                return Json(new { success = false, message = "Please login first" });
            }

            try
            {
                // Handle Bulk Item creation if not exists
                if (finalIsBulk)
                {
                    bool exists = false;
                    if (finalProductId > 0)
                    {
                        exists = _context.BulkItems.Any(b => b.Id == finalProductId);
                    }

                    if (!exists && !string.IsNullOrEmpty(itemName))
                    {
                        // Create new Bulk Item
                        var newBulkItem = new BulkItem
                        {
                            Name = itemName,
                            Price = price ?? 0,
                            Category = category ?? "Custom",
                            Description = description ?? "Custom bulk order item",
                            Status = "Active",
                            CreatedAt = DateTime.Now
                        };
                        _context.BulkItems.Add(newBulkItem);
                        await _context.SaveChangesAsync();
                        finalProductId = newBulkItem.Id;
                    }
                }

                if (finalProductId <= 0)
                {
                    return Json(new { success = false, message = "Invalid product ID" });
                }

                // Phase 1: Daily Stock Limits Validation
                if (!finalIsBulk)
                {
                    var food = _context.Foods.FirstOrDefault(f => f.Id == finalProductId);
                    if (food != null && food.DailyLimit.HasValue)
                    {
                        var today = DateTime.Today;
                        var unitsSoldToday = _context.OrderItems
                            .Include(oi => oi.Order)
                            .Where(oi => oi.FoodId == finalProductId && oi.Order != null && oi.Order.CreatedAt >= today && oi.Order.Status != "Cancelled")
                            .Sum(oi => (int?)oi.Quantity) ?? 0;
                            
                        var unitsInCart = _context.Carttables
                            .Where(c => c.Uid == uid.Value && c.Pid == finalProductId && !c.IsBulk)
                            .Sum(c => (int?)c.Qty) ?? 0;

                        if (unitsSoldToday + unitsInCart + finalQuantity > food.DailyLimit.Value)
                        {
                            int remaining = food.DailyLimit.Value - unitsSoldToday - unitsInCart;
                            if (remaining <= 0)
                                return Json(new { success = false, message = "Sorry, this item is sold out for today!" });
                            else
                                return Json(new { success = false, message = $"Sorry, only {remaining} more units can be added to your cart today due to stock limits." });
                        }
                    }
                }

                // --- Calorie Check Logic (BEFORE DOING ANYTHING) ---
                if (!finalIsBulk)
                {
                    var food = _context.Foods.FirstOrDefault(f => f.Id == finalProductId);
                    if (food != null && food.Calories.HasValue)
                    {
                        var today = DateTime.Today;
                        var survey = _context.HealthSurveys.FirstOrDefault(s => s.UserId == uid.Value);
                        int recommended = survey != null ? (int)survey.RecommendedCalories : 2000;

                        int todayConsumed = _context.DailyCalorieEntries
                            .Where(d => d.UserId == uid.Value && d.Date == today)
                            .Sum(d => (int?)d.Calories) ?? 0;

                        // Calculate current cart calories WITHOUT the new item yet
                        var cartItemPids = _context.Carttables
                            .Where(c => c.Uid == uid.Value && !c.IsBulk)
                            .Select(c => new { c.Pid, c.Qty })
                            .ToList();
                            
                        var pids = cartItemPids.Select(c => c.Pid).ToList();
                        var foodCalories = _context.Foods
                            .Where(f => pids.Contains(f.Id))
                            .ToDictionary(f => f.Id, f => f.Calories ?? 0);

                        int cartCalories = cartItemPids.Sum(c => c.Qty * (foodCalories.ContainsKey(c.Pid) ? foodCalories[c.Pid] : 0));
                        int additionalCalories = food.Calories.Value * finalQuantity;

                        int totalExpected = todayConsumed + cartCalories + additionalCalories;

                        // BLOCK IF EXCEEDED
                        if (totalExpected > recommended)
                        {
                            return Json(new { success = false, message = $"WARNING: Calorie Limit Exceeded! Adding this item ({additionalCalories} kcal) puts your today's total at {totalExpected} kcal (Goal: {recommended} kcal). Please adjust your intake!" });
                        }
                    }
                }

                // Check if item already exists in cart
                var existingItem = _context.Carttables
                    .FirstOrDefault(c => c.Uid == uid.Value && c.Pid == finalProductId && c.IsBulk == finalIsBulk);

                if (existingItem != null)
                {
                    existingItem.Qty += finalQuantity;
                }
                else
                {
                    var cartItem = new Carttable
                    {
                        Uid = uid.Value,
                        Pid = finalProductId,
                        IsBulk = finalIsBulk,
                        Qty = finalQuantity,
                        Date = DateTime.Now
                    };

                    _context.Carttables.Add(cartItem);
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Item added to cart successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error adding to cart: " + ex.Message);
                return Json(new { success = false, message = "An error occurred while adding to cart." });
            }
        }

        public class AddToCartRequest
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }
            public bool IsBulk { get; set; }
            public string? ItemName { get; set; }
            public decimal? Price { get; set; }
            public string? Category { get; set; }
            public string? Description { get; set; }
        }


        // ================= REMOVE ITEM =================
        [HttpPost]
        public IActionResult Remove(int id)
        {
            var uid = HttpContext.Session.GetInt32("UserId");

            if (!uid.HasValue)
                return RedirectToAction("Login", "Auth");

            var item = _context.Carttables
                .FirstOrDefault(c => c.Crid == id && c.Uid == uid.Value);

            if (item != null)
            {
                _context.Carttables.Remove(item);
                _context.SaveChanges();
            }

            return RedirectToAction("Index");
        }


        // ================= UPDATE QUANTITY =================
        [HttpPost]
        public IActionResult UpdateQuantity(
     int id,
     int qty,
     string lessOil,
     string lessSalt,
     string lessSpicy,
     string noOnion,
     string noGarlic)
        {
            var uid = HttpContext.Session.GetInt32("UserId");

            if (!uid.HasValue)
                return RedirectToAction("Login", "Auth");

            var item = _context.Carttables
                .FirstOrDefault(c => c.Crid == id && c.Uid == uid.Value);

            if (item != null && qty > 0)
            {
                item.Qty = qty;

                // Build instruction text
                string instruction = "";

                if (!string.IsNullOrEmpty(lessOil))
                    instruction += "Less Oil, ";

                if (!string.IsNullOrEmpty(lessSalt))
                    instruction += "Less Salt, ";

                if (!string.IsNullOrEmpty(lessSpicy))
                    instruction += "Less Spicy, ";

                if (!string.IsNullOrEmpty(noOnion))
                    instruction += "No Onion, ";

                if (!string.IsNullOrEmpty(noGarlic))
                    instruction += "No Garlic";

                item.SpecialInstruction = instruction;

                _context.SaveChanges();
            }

            return RedirectToAction("Index");
        }


        // ================= GET CART COUNT =================
        [HttpGet]
        public IActionResult GetCartCount()
        {
            var uid = HttpContext.Session.GetInt32("UserId");

            if (!uid.HasValue)
                return Json(new { success = false });

            var count = _context.Carttables
                .Where(c => c.Uid == uid.Value)
                .Sum(c => c.Qty);

            return Json(new { success = true, count });
        }

        // ================= REORDER =================
        [HttpPost]
        public async Task<IActionResult> Reorder(int orderId)
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (!uid.HasValue)
                return Json(new { success = false, message = "Please login first" });

            try
            {
                var orderItems = await _context.OrderItems
                    .Where(oi => oi.OrderId == orderId)
                    .ToListAsync();

                if (!orderItems.Any())
                    return Json(new { success = false, message = "Order not found" });

                foreach (var item in orderItems)
                {
                    var existing = await _context.Carttables
                        .FirstOrDefaultAsync(c => c.Uid == uid.Value && c.Pid == (item.FoodId ?? item.BulkItemId ?? 0) && c.IsBulk == (item.BulkItemId.HasValue));

                    if (existing != null)
                    {
                        existing.Qty += item.Quantity ?? 1;
                    }
                    else
                    {
                        _context.Carttables.Add(new Carttable
                        {
                            Uid = uid.Value,
                            Pid = item.FoodId ?? item.BulkItemId ?? 0,
                            IsBulk = item.BulkItemId.HasValue,
                            Qty = item.Quantity ?? 1,
                            Date = DateTime.Now
                        });
                    }
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }


        private decimal GetBulkDiscountedPrice(decimal basePrice, int quantity)
        {
            if (quantity >= 500) return basePrice * 0.70m;
            if (quantity >= 200) return basePrice * 0.78m;
            if (quantity >= 100) return basePrice * 0.85m;
            if (quantity >= 50) return basePrice * 0.92m;
            return basePrice;
        }

        // ================= CART PAGE =================
        [HttpGet]
        public IActionResult Index()
        {
            var uid = HttpContext.Session.GetInt32("UserId");

            if (!uid.HasValue)
                return RedirectToAction("Login", "Auth");

            try
            {
                // Optimized query: Fetch all cart items for the user in one go
                var cartRows = _context.Carttables
                    .Where(c => c.Uid == uid.Value)
                    .ToList();

                var allItems = new List<CartItem>();

                // Process in memory to handle the logic of joining with either Foods or BulkItems
                foreach (var row in cartRows)
                {
                    if (row.IsBulk)
                    {
                        var bulk = _context.BulkItems.FirstOrDefault(b => b.Id == row.Pid);
                        if (bulk != null)
                        {
                            decimal discountedPrice = GetBulkDiscountedPrice(bulk.Price, row.Qty);
                            allItems.Add(new CartItem
                            {
                                Id = row.Crid,
                                Name = bulk.Name + " (Bulk)",
                                Price = discountedPrice,
                                Quantity = row.Qty,
                                ImageUrl = bulk.ImagePath ?? "/images/default-bulk.png",
                                IsBulk = true,
                                SpecialInstructions = row.SpecialInstruction
                            });
                        }
                    }
                    else
                    {
                        var food = _context.Foods.FirstOrDefault(f => f.Id == row.Pid);
                        if (food != null)
                        {
                            allItems.Add(new CartItem
                            {
                                Id = row.Crid,
                                Name = food.Name,
                                Price = food.Price,
                                Quantity = row.Qty,
                                ImageUrl = food.ImagePath ?? "/images/default-food.png",
                                IsBulk = false,
                                SpecialInstructions = row.SpecialInstruction
                            });
                        }
                    }
                }

                return View("Index", allItems);
            }
            catch (Exception ex)
            {
                // Log error and return empty cart or error view
                Console.WriteLine("Error loading cart: " + ex.Message);
                return View("Index", new List<CartItem>());
            }
        }
        [HttpGet]
        public IActionResult Checkout()
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (!uid.HasValue)
                return RedirectToAction("Login", "Auth");

            var cartRows = _context.Carttables
                .Where(c => c.Uid == uid.Value)
                .ToList();

            if (!cartRows.Any())
                return RedirectToAction("Index");

            decimal subtotal = 0m;
            int totalOrderCalories = 0;

            foreach (var c in cartRows)
            {
                if (c.IsBulk)
                {
                    var bulk = _context.BulkItems.FirstOrDefault(b => b.Id == c.Pid);
                    if (bulk != null)
                        subtotal += GetBulkDiscountedPrice(bulk.Price, c.Qty) * c.Qty;
                }
                else
                {
                    var food = _context.Foods.FirstOrDefault(f => f.Id == c.Pid);
                    if (food != null)
                    {
                        subtotal += food.Price * c.Qty;
                        totalOrderCalories += (food.Calories ?? 0) * c.Qty;
                    }
                }
            }

            decimal deliveryCharge = subtotal > 500 ? 0 : 40;
            
            // Apply Overall Bulk Discount from appsettings
            var thresholdAmountStr = _config["BulkDiscount:ThresholdAmount"];
            var discountPercentageStr = _config["BulkDiscount:DiscountPercentage"];
            
            decimal discountApplied = 0m;
            decimal subtotalAfterDiscount = subtotal;

            if (decimal.TryParse(thresholdAmountStr, out decimal thresholdAmount) && 
                decimal.TryParse(discountPercentageStr, out decimal discountPercent))
            {
                if (subtotal >= thresholdAmount)
                {
                    discountApplied = subtotal * (discountPercent / 100m);
                    subtotalAfterDiscount = subtotal - discountApplied;
                    ViewBag.DiscountApplied = true;
                    ViewBag.DiscountAmount = discountApplied;
                    ViewBag.DiscountPercentage = discountPercent;
                }
            }

            decimal gst = subtotalAfterDiscount * 0.05m;
            decimal totalAmount = subtotalAfterDiscount + deliveryCharge + gst;

            bool isBulkOrder = cartRows.Any(c => c.IsBulk) || discountApplied > 0;
            ViewBag.IsBulkOrder = isBulkOrder;

            // Phase 1: Enforce 50% frontend deposit if it's a Bulk Order
            if (isBulkOrder)
            {
                ViewBag.OriginalTotal = totalAmount; // Store original
                totalAmount = totalAmount / 2; // Ask for 50%
            }

            ViewBag.Subtotal = subtotal;
            ViewBag.SubtotalAfterDiscount = subtotalAfterDiscount;
            ViewBag.DeliveryCharge = deliveryCharge;
            ViewBag.GST = gst;
            ViewBag.TotalAmount = totalAmount;

            var user = _context.UserSignups.FirstOrDefault(u => u.Id == uid.Value);
            
            // Prefer dynamically selected location over profile address
            var sessionAddress = HttpContext.Session.GetString("UserAddress") ?? HttpContext.Request.Cookies["UserAddress"];
            ViewBag.SavedAddress = !string.IsNullOrEmpty(sessionAddress) ? sessionAddress : (user?.Address ?? "");

            // Calorie Warning Data
            var survey = _context.HealthSurveys.FirstOrDefault(h => h.UserId == uid.Value);
            int calorieLimit = survey?.RecommendedCalories ?? 2000;
            int consumedToday = _context.DailyCalorieEntries
                .Where(ce => ce.UserId == uid.Value && ce.Date.Date == DateTime.Today)
                .Sum(ce => ce.Calories);

            ViewBag.OrderCalories = totalOrderCalories;
            ViewBag.CaloriesConsumedToday = consumedToday;
            ViewBag.CalorieLimit = calorieLimit;

            var model = new CheckoutViewModel();
            model.User = new CheckoutUserViewModel
            {
                Name = user?.Name ?? "Customer",
                Address = !string.IsNullOrEmpty(sessionAddress) ? sessionAddress : user?.Address,
                Email = user?.Email,
                PhoneNumber = user?.Phone
            };

            foreach (var c in cartRows)
            {
                if (c.IsBulk)
                {
                    var bulk = _context.BulkItems.FirstOrDefault(b => b.Id == c.Pid);
                    if (bulk != null)
                    {
                        model.CartItems.Add(new CheckoutCartItem
                        {
                            FoodName = bulk.Name + " (Bulk)",
                            Price = GetBulkDiscountedPrice(bulk.Price, c.Qty),
                            Quantity = c.Qty,
                            ImageUrl = bulk.ImagePath
                        });
                    }
                }
                else
                {
                    var food = _context.Foods.FirstOrDefault(f => f.Id == c.Pid);
                    if (food != null)
                    {
                        model.CartItems.Add(new CheckoutCartItem
                        {
                            FoodName = food.Name,
                            Price = food.Price,
                            Quantity = c.Qty,
                            ImageUrl = food.ImagePath
                        });
                    }
                }
            }

            return View(model);
        }



        // ================= PREPARE RAZORPAY ORDER =================
        [HttpPost]
        public IActionResult CreateRazorpayOrder()
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (!uid.HasValue) return Json(new { success = false, message = "Please login" });

            try
            {
                var cartItems = _context.Carttables.Where(c => c.Uid == uid.Value).ToList();
                if (!cartItems.Any()) return Json(new { success = false, message = "Cart is empty" });

                decimal foodTotal = 0m;
                foreach (var item in cartItems)
                {
                    if (item.IsBulk)
                    {
                        var bulk = _context.BulkItems.FirstOrDefault(b => b.Id == item.Pid);
                        if (bulk != null) foodTotal += GetBulkDiscountedPrice(bulk.Price, item.Qty) * item.Qty;
                    }
                    else
                    {
                        var food = _context.Foods.FirstOrDefault(f => f.Id == item.Pid);
                        if (food != null) foodTotal += food.Price * item.Qty;
                    }
                }

                // Apply Overall Bulk Discount
                var thresholdAmountStr = _config["BulkDiscount:ThresholdAmount"];
                var discountPercentageStr = _config["BulkDiscount:DiscountPercentage"];
                decimal subtotalAfterDiscount = foodTotal;

                if (decimal.TryParse(thresholdAmountStr, out decimal thresholdAmount) && 
                    decimal.TryParse(discountPercentageStr, out decimal discountPercent))
                {
                    if (foodTotal >= thresholdAmount)
                    {
                        subtotalAfterDiscount = foodTotal - (foodTotal * (discountPercent / 100m));
                    }
                }

                decimal deliveryCharge = foodTotal > 500 ? 0 : 40;
                decimal gst = subtotalAfterDiscount * 0.05m;
                decimal finalTotal = subtotalAfterDiscount + deliveryCharge + gst;

                if (cartItems.Any(c => c.IsBulk) || subtotalAfterDiscount < foodTotal)
                {
                    finalTotal = finalTotal / 2; // 50% upfront for bulk
                }

                // Initialize Razorpay Client
                var keyId = _config["Razorpay:KeyId"];
                var keySecret = _config["Razorpay:KeySecret"];
                
                if (string.IsNullOrEmpty(keyId) || string.IsNullOrEmpty(keySecret))
                    return Json(new { success = false, message = "Payment configuration missing" });

                var client = new RazorpayClient(keyId, keySecret);
                
                Dictionary<string, object> options = new Dictionary<string, object>();
                options.Add("amount", (long)Math.Round(finalTotal * 100)); // Use long for large amounts and round correctly
                options.Add("currency", "INR");
                options.Add("receipt", "rcpt_" + Guid.NewGuid().ToString().Substring(0, 8));

                Razorpay.Api.Order order = client.Order.Create(options);
                string orderId = order["id"].ToString();

                _activityLogger.LogAsync("Razorpay Order Created", $"Order {orderId} created for User {uid} with amount {finalTotal}");

                return Json(new { 
                    success = true, 
                    razorpayOrderId = orderId, 
                    amount = finalTotal,
                    keyId = keyId
                });
            }
            catch (Exception ex)
            {
                _activityLogger.LogAsync("Razorpay Order Failed", $"Failed to create Razorpay order for User {uid}: {ex.Message}");
                return Json(new { success = false, message = "Could not initialize payment gateway. Please try again." });
            }
        }

        // ================= CONFIRM PAYMENT AND CREATE ORDER =================
        [HttpPost]
        public async Task<IActionResult> ConfirmPayment(string razorpayPaymentId, string razorpayOrderId, string razorpaySignature, string deliveryAddress, string deliveryNotes = "", string deliverySchedule = "One-time", bool clearCart = true)
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (!uid.HasValue) return Json(new { success = false, message = "Please login" });

            try
            {
                // Verify Signature
                var keyId = _config["Razorpay:KeyId"];
                var keySecret = _config["Razorpay:KeySecret"];
                
                if (string.IsNullOrEmpty(keyId) || string.IsNullOrEmpty(keySecret))
                    return Json(new { success = false, message = "Payment configuration missing" });

                // Initialize client to set static credentials for Utils
                var client = new RazorpayClient(keyId, keySecret);

                Dictionary<string, string> attributes = new Dictionary<string, string>();
                attributes.Add("razorpay_payment_id", razorpayPaymentId);
                attributes.Add("razorpay_order_id", razorpayOrderId);
                attributes.Add("razorpay_signature", razorpaySignature);

                try
                {
                    Utils.verifyPaymentSignature(attributes);
                }
                catch (Exception sigEx)
                {
                    _activityLogger.LogAsync("Payment Failed", $"Signature verification failed for Order {razorpayOrderId}: {sigEx.Message}");
                    return Json(new { success = false, message = "Payment verification failed. Please contact support." });
                }

                // Payment is valid, now create the internal order
                var user = _context.UserSignups.FirstOrDefault(u => u.Id == uid.Value);
                var cartItems = await _context.Carttables.Where(c => c.Uid == uid.Value).ToListAsync();

                if (!cartItems.Any()) return Json(new { success = false, message = "Cart empty during confirmation" });

                // Reuse the same logic as before to calculate totals
                int totalItems = 0;
                int totalCalories = 0;
                decimal foodTotal = 0m;

                foreach (var item in cartItems)
                {
                    if (item.IsBulk)
                    {
                        var bulk = _context.BulkItems.FirstOrDefault(b => b.Id == item.Pid);
                        if (bulk != null)
                        {
                            totalItems += item.Qty;
                            foodTotal += GetBulkDiscountedPrice(bulk.Price, item.Qty) * item.Qty;
                        }
                    }
                    else
                    {
                        var food = _context.Foods.FirstOrDefault(f => f.Id == item.Pid);
                        if (food != null)
                        {
                            totalItems += item.Qty;
                            totalCalories += (food.Calories ?? 0) * item.Qty;
                            foodTotal += food.Price * item.Qty;
                        }
                    }
                }

                decimal deliveryCharge = foodTotal > 500 ? 0 : 40;
                var thresholdAmountStr = _config["BulkDiscount:ThresholdAmount"];
                var discountPercentageStr = _config["BulkDiscount:DiscountPercentage"];
                decimal subtotalAfterDiscount = foodTotal;

                if (decimal.TryParse(thresholdAmountStr, out decimal thresholdAmount) && 
                    decimal.TryParse(discountPercentageStr, out decimal discountPercent))
                {
                    if (foodTotal >= thresholdAmount)
                        subtotalAfterDiscount = foodTotal - (foodTotal * (discountPercent / 100m));
                }

                decimal gst = subtotalAfterDiscount * 0.05m;
                decimal finalTotal = subtotalAfterDiscount + deliveryCharge + gst;
                bool isBulk = cartItems.Any(c => c.IsBulk) || subtotalAfterDiscount < foodTotal;
                
                if (isBulk) finalTotal = finalTotal / 2;

                var otp = new Random().Next(1234, 10000);

                var order = new OrderTable
                {
                    UserId = uid.Value,
                    CustomerName = user?.Name,
                    CustomerPhone = user?.Phone,
                    TotalItems = totalItems,
                    OrderType = isBulk ? $"Bulk Order (50% Deposit) - {deliverySchedule}" : $"Delivery - {deliverySchedule}",
                    DeliveryAddress = deliveryAddress,
                    DeliveryNotes = deliveryNotes,
                    DeliveryStatus = "Order Confirmed",
                    TotalCalories = totalCalories,
                    TotalAmount = finalTotal,
                    DeliveryCharge = deliveryCharge,
                    GST = gst,
                    PaymentStatus = "Completed",
                    Status = "Accepted",
                    TrackingProgress = 10,
                    CreatedAt = DateTime.Now,
                    DeliveryOTP = otp,
                    IsDelivered = false,
                    OrderStatus = "Preparing"
                };

                _context.OrderTables.Add(order);
                await _context.SaveChangesAsync();

                // Distribute payment to vendor and admin
                try
                {
                    await _distributionService.DistributePaymentAsync(order.OrderId);
                }
                catch (Exception distEx)
                {
                    // Log but don't fail the whole order creation
                    await _activityLogger.LogAsync("Distribution Error", $"Failed to distribute payment for order #{order.OrderId}: {distEx.Message}");
                }

                // CREATE PAYMENT RECORD (For Payment History)
                var payment = new Models.Payment
                {
                    OrderId = order.OrderId,
                    PaymentMode = "Razorpay",
                    Amount = finalTotal,
                    TransactionId = razorpayPaymentId,
                    IsRefunded = false,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                _context.Payments.Add(payment);

                // Add Items and Calories
                foreach (var item in cartItems)
                {
                    var orderItem = new OrderItem
                    {
                        OrderId = order.OrderId,
                        Quantity = item.Qty,
                        SpecialInstruction = item.SpecialInstruction,
                        CreatedAt = DateTime.Now
                    };

                    if (item.IsBulk)
                    {
                        var bulk = _context.BulkItems.FirstOrDefault(b => b.Id == item.Pid);
                        if (bulk != null)
                        {
                            orderItem.BulkItemId = bulk.Id;
                            orderItem.ItemName = bulk.Name + " (Bulk)";
                            orderItem.PricePerItem = GetBulkDiscountedPrice(bulk.Price, item.Qty);
                        }
                    }
                    else
                    {
                        var food = _context.Foods.FirstOrDefault(f => f.Id == item.Pid);
                        if (food != null)
                        {
                            orderItem.FoodId = food.Id;
                            orderItem.ItemName = food.Name;
                            orderItem.PricePerItem = food.Price;

                            _context.DailyCalorieEntries.Add(new DailyCalorieEntry
                            {
                                UserId = uid.Value,
                                Date = DateTime.Today,
                                FoodName = food.Name,
                                Calories = (food.Calories ?? 0) * item.Qty,
                                MealType = "Order",
                                OrderId = order.OrderId
                            });
                        }
                    }
                    _context.OrderItems.Add(orderItem);
                }

                // Clear Cart
                _context.Carttables.RemoveRange(cartItems);
                await _context.SaveChangesAsync();

                // Log activity
                await _activityLogger.LogAsync("Payment Confirmed", $"Payment {razorpayPaymentId} for order #{order.OrderId} successful.");

                // 📧 Send Confirmation Email (Contains Delivery OTP)
                try
                {
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
                    Console.WriteLine("Failed to send order confirmation email: " + ex.Message);
                }

                return Json(new { success = true, orderId = order.OrderId });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Confirmation error: " + ex.Message });
            }
        }

        // ================= SUCCESS PAGE =================
        public IActionResult Success()
        {
            return View();
        }
    }
}