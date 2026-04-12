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

namespace NUTRIBITE.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IActivityLogger _activityLogger;
        private readonly IEmailService _emailService;

        public CartController(ApplicationDbContext context, IActivityLogger activityLogger, IEmailService emailService)
        {
            _context = context;
            _activityLogger = activityLogger;
            _emailService = emailService;
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

                return Json(new { success = true });
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
                            allItems.Add(new CartItem
                            {
                                Id = row.Crid,
                                Name = bulk.Name + " (Bulk)",
                                Price = bulk.Price,
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

            foreach (var c in cartRows)
            {
                if (c.IsBulk)
                {
                    var bulk = _context.BulkItems.FirstOrDefault(b => b.Id == c.Pid);
                    if (bulk != null)
                        subtotal += bulk.Price * c.Qty;
                }
                else
                {
                    var food = _context.Foods.FirstOrDefault(f => f.Id == c.Pid);
                    if (food != null)
                        subtotal += food.Price * c.Qty;
                }
            }

            decimal deliveryCharge = subtotal > 500 ? 0 : 40;
            decimal gst = subtotal * 0.05m;
            decimal totalAmount = subtotal + deliveryCharge + gst;

            bool isBulkOrder = cartRows.Any(c => c.IsBulk);
            ViewBag.IsBulkOrder = isBulkOrder;

            // Phase 1: Enforce 50% frontend deposit if it's a Bulk Order
            if (isBulkOrder)
            {
                ViewBag.OriginalTotal = totalAmount; // Store original
                totalAmount = totalAmount / 2; // Ask for 50%
            }

            ViewBag.Subtotal = subtotal;
            ViewBag.DeliveryCharge = deliveryCharge;
            ViewBag.GST = gst;
            ViewBag.TotalAmount = totalAmount; 

            return View();
        }


        // ================= CHECKOUT =================
        [HttpPost]
        public async Task<IActionResult> Checkout(string deliveryAddress = "", string deliveryNotes = "", string paymentMethod = "Online", bool clearCart = true, double? latitude = null, double? longitude = null)
        {
            var uid = HttpContext.Session.GetInt32("UserId");

            if (!uid.HasValue)
                return RedirectToAction("Login", "Auth");

            try
            {
                var user = _context.UserSignups
                    .FirstOrDefault(u => u.Id == uid.Value);

                var cartItems = _context.Carttables
                    .Where(c => c.Uid == uid.Value)
                    .ToList();

                if (!cartItems.Any())
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        return Json(new { success = false, message = "Your cart is empty." });
                    return RedirectToAction("Index");
                }

                // Phase 3: Geofencing Check
                // Attempt to get vendor tied to this cart
                var firstCartItem = cartItems.FirstOrDefault(c => !c.IsBulk);
                if (firstCartItem != null && latitude.HasValue && longitude.HasValue)
                {
                    var food = _context.Foods.FirstOrDefault(f => f.Id == firstCartItem.Pid);
                    if (food != null && food.VendorId.HasValue)
                    {
                        var vendor = _context.VendorSignups.FirstOrDefault(v => v.VendorId == food.VendorId.Value);
                        if (vendor != null && vendor.MaxDeliveryRadiusKm.HasValue)
                        {
                            // Mock Coordinates for the Vendor Kitchen (Central Lucknow)
                            double vendorLat = 26.8467;
                            double vendorLng = 80.9462;
                            
                            // Haversine mock calculation
                            double dLat = (latitude.Value - vendorLat) * Math.PI / 180.0;
                            double dLon = (longitude.Value - vendorLng) * Math.PI / 180.0;
                            double a = Math.Sin(dLat/2) * Math.Sin(dLat/2) + Math.Cos(vendorLat * Math.PI / 180.0) * Math.Cos(latitude.Value * Math.PI / 180.0) * Math.Sin(dLon/2) * Math.Sin(dLon/2);
                            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1-a));
                            double distanceKm = 6371 * c;

                            if (distanceKm > vendor.MaxDeliveryRadiusKm.Value)
                            {
                                return Json(new { success = false, message = $"Delivery not available! You are {distanceKm:F1}km away. This vendor only serves within {vendor.MaxDeliveryRadiusKm.Value}km." });
                            }
                        }
                    }
                }

                int totalItems = 0;
                int totalCalories = 0;
                decimal foodTotal = 0m;

                foreach (var item in cartItems)
                {
                    if (item.IsBulk)
                    {
                        var bulk = _context.BulkItems.FirstOrDefault(b => b.Id == item.Pid);
                        if (bulk == null) continue;

                        totalItems += item.Qty;
                        totalCalories += 0; 
                        foodTotal += bulk.Price * item.Qty;
                    }
                    else
                    {
                        var food = _context.Foods.FirstOrDefault(f => f.Id == item.Pid);
                        if (food == null) continue;

                        totalItems += item.Qty;
                        totalCalories += (food.Calories ?? 0) * item.Qty;
                        foodTotal += food.Price * item.Qty;
                    }
                }

                // Calculations for realism
                decimal deliveryCharge = foodTotal > 500 ? 0 : 40;
                decimal gst = foodTotal * 0.05m; // 5% GST
                decimal finalTotal = foodTotal + deliveryCharge + gst;

                bool isBulk = cartItems.Any(c => c.IsBulk);
                string orderType = isBulk ? "Bulk Order (50% Deposit Paid)" : "Delivery";
                
                if (isBulk)
                {
                    finalTotal = finalTotal / 2; // For Bulk, charge 50% upfront
                }

                var otp = new Random().Next(1234, 10000);

                var order = new OrderTable
                {
                    UserId = uid.Value,
                    CustomerName = user?.Name,
                    CustomerPhone = user?.Phone,
                    TotalItems = totalItems,
                    PickupSlot = null,
                    OrderType = orderType,
                    DeliveryAddress = deliveryAddress,
                    DeliveryNotes = deliveryNotes,
                    DeliveryStatus = "Pending Assignment",
                    TotalCalories = totalCalories,
                    TotalAmount = finalTotal,
                    DeliveryCharge = deliveryCharge,
                    GST = gst,
                    PaymentStatus = "Pending",
                    Status = "Pending Payment",
                    TrackingProgress = 0,
                    IsFlagged = false,
                    CreatedAt = DateTime.Now,
                    DeliveryOTP = otp,
                    IsDelivered = false,
                    OrderStatus = "Pending Payment"
                };

                _context.OrderTables.Add(order);
                await _context.SaveChangesAsync();

                // Send Order Confirmation Email with OTP
                if (user != null && !string.IsNullOrEmpty(user.Email))
                {
                    var emailBody = $@"
                        <div style='font-family: sans-serif; padding: 20px; border: 1px solid #eee; border-radius: 10px;'>
                            <h2 style='color: #2d6a4f;'>Order Confirmed!</h2>
                            <p>Hello {user.Name}, your order <b>#{order.OrderId}</b> has been placed successfully.</p>
                            <hr>
                            <h3>Delivery OTP: <span style='color: #2d6a4f; font-size: 24px;'>{otp}</span></h3>
                            <p style='color: #666;'>Please share this OTP with our delivery partner only when you receive your meal.</p>
                            <hr>
                            <h4>Order Summary:</h4>
                            <p>Food Total: ₹{foodTotal:N2}</p>
                            <p>Delivery Charge: ₹{deliveryCharge:N2}</p>
                            <p>GST (5%): ₹{gst:N2}</p>
                            <p style='font-size: 18px; font-weight: bold;'>Total Amount: ₹{finalTotal:N2}</p>
                            <p>Delivery Address: {deliveryAddress}</p>
                            <br>
                            <p>Thank you for choosing NutriBite!</p>
                        </div>";
                    
                    var result = await _emailService.SendEmailAsync(user.Email, $"Order Confirmed - NutriBite #{order.OrderId}", emailBody);
                    if (!result)
                    {
                        var emailError = _emailService.GetLastError();
                        Console.WriteLine($"[ORDER EMAIL FAIL] Order #{order.OrderId}: {emailError}");
                        await _activityLogger.LogAsync("Email Failed", $"Failed to send order confirmation for #{order.OrderId}. Error: {emailError}");
                    }
                }

                // Log order activity for dashboard alerts
                await _activityLogger.LogAsync("Order Placed", $"New order #{order.OrderId} placed by {order.CustomerName} for ₹{order.TotalAmount:N2}");

                // ADD ORDER ITEMS + CALORIE TRACKING
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
                            orderItem.PricePerItem = bulk.Price;
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
                                Protein = 0,
                                Carbs = 0,
                                Fats = 0,
                                OrderId = order.OrderId
                            });
                        }
                    }

                    _context.OrderItems.Add(orderItem);
                }

                await _context.SaveChangesAsync();

                // CLEAR CART if requested
                if (clearCart)
                {
                    _context.Carttables.RemoveRange(cartItems);
                    await _context.SaveChangesAsync();
                }

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, orderId = order.OrderId });
                }

                return RedirectToAction("Success");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Checkout Error: " + ex.Message);
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Failed to create order: " + ex.Message });
                }
                return View("Error");
            }
        }


        // ================= SUCCESS PAGE =================
        public IActionResult Success()
        {
            return View();
        }
    }
}