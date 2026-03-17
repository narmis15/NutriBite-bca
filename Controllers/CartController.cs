using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System;
using System.Collections.Generic;
using NUTRIBITE.Models;

namespace NUTRIBITE.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CartController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ================= ADD TO CART =================
        [HttpPost]
        public IActionResult AddToCart(int foodId)
        {
            var uid = HttpContext.Session.GetInt32("UserId");

            if (!uid.HasValue)
            {
                return Json(new { success = false, message = "Please login first" });
            }

            var food = _context.Foods.FirstOrDefault(f => f.Id == foodId);

            if (food == null)
            {
                return Json(new { success = false, message = "Food not found" });
            }

            var existingItem = _context.Carttables
                .FirstOrDefault(c => c.Uid == uid.Value && c.Pid == foodId);

            if (existingItem != null)
            {
                existingItem.Qty += 1;
            }
            else
            {
                var cartItem = new Carttable
                {
                    Uid = uid.Value,
                    Pid = foodId,
                    Qty = 1,
                    Date = DateTime.Now
                };

                _context.Carttables.Add(cartItem);
            }

            _context.SaveChanges();

            return Json(new { success = true });
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
        public IActionResult Index()
        {
            var uid = HttpContext.Session.GetInt32("UserId");

            if (!uid.HasValue)
                return RedirectToAction("Login", "Auth");

            var cartItems = (from cart in _context.Carttables
                             join food in _context.Foods
                             on cart.Pid equals food.Id
                             where cart.Uid == uid.Value
                             select new CartItem
                             {
                                 Id = cart.Crid,
                                 Name = food.Name,
                                 Price = food.Price,
                                 Quantity = cart.Qty,
                                 ImageUrl = food.ImagePath
                             }).ToList();

            return View(cartItems);
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
                    var food = _context.Foods.FirstOrDefault(f => f.Id == c.Pid);
                    if (food != null)
                        subtotal += (food.Price) * c.Qty;
            }

            ViewBag.TotalAmount = subtotal; // major units (e.g. 499.50)
            return View();
        }


        // ================= CHECKOUT =================
        [HttpPost]
        public IActionResult Checkout(string pickupSlot = "12:00 PM", string orderType = "Pickup", string deliveryAddress = "", string deliveryNotes = "", string paymentMethod = "Online", bool clearCart = true)
        {
            var uid = HttpContext.Session.GetInt32("UserId");

            if (!uid.HasValue)
                return RedirectToAction("Login", "Auth");

            var user = _context.UserSignups
                .FirstOrDefault(u => u.Id == uid.Value);

            var cartItems = _context.Carttables
                .Where(c => c.Uid == uid.Value)
                .ToList();

            if (!cartItems.Any())
                return RedirectToAction("Index");


            int totalItems = 0;
            int totalCalories = 0;


            foreach (var item in cartItems)
            {
                var food = _context.Foods.FirstOrDefault(f => f.Id == item.Pid);

                if (food == null)
                    continue;

                totalItems += item.Qty;
                totalCalories += (food.Calories ?? 0) * item.Qty;
            }


            var order = new OrderTable
            {
                UserId = uid.Value,
                CustomerName = user?.Name,
                CustomerPhone = user?.Phone,
                TotalItems = totalItems,
                PickupSlot = orderType == "Pickup" ? pickupSlot : null,
                OrderType = orderType,
                DeliveryAddress = orderType == "Delivery" ? deliveryAddress : null,
                DeliveryNotes = orderType == "Delivery" ? deliveryNotes : null,
                DeliveryStatus = orderType == "Delivery" ? "Pending Assignment" : null,
                TotalCalories = totalCalories,
                PaymentStatus = paymentMethod == "COD" ? "To be Paid (COD)" : "Pending",
                Status = paymentMethod == "COD" ? "Placed" : "Pending Payment",
                IsFlagged = false,
                CreatedAt = DateTime.Now
            };


            _context.OrderTables.Add(order);
            _context.SaveChanges();


            // ADD ORDER ITEMS + CALORIE TRACKING
            foreach (var item in cartItems)
            {
                var food = _context.Foods.FirstOrDefault(f => f.Id == item.Pid);

                if (food == null)
                    continue;

                _context.OrderItems.Add(new OrderItem
                {
                    OrderId = order.OrderId,
                    FoodId = food.Id, // Ensure FoodId is linked for vendor visibility
                    ItemName = food.Name,
                    Quantity = item.Qty,
                    SpecialInstruction = item.SpecialInstruction,
                    CreatedAt = DateTime.Now
                });

                _context.DailyCalorieEntries.Add(new DailyCalorieEntry
                {
                    UserId = uid.Value,
                    Date = DateTime.Today,
                    FoodName = food.Name,
                    Calories = (food.Calories ?? 0) * item.Qty,
                    Protein = 0,
                    Carbs = 0,
                    Fats = 0
                });
            }


            _context.SaveChanges();


            // CLEAR CART if requested
            if (clearCart)
            {
                _context.Carttables.RemoveRange(cartItems);
                _context.SaveChanges();
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true, orderId = order.OrderId });
            }

            return RedirectToAction("Success");
        }


        // ================= SUCCESS PAGE =================
        public IActionResult Success()
        {
            return View();
        }
    }
}