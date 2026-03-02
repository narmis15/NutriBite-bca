using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System;
using System.Collections.Generic;
using NUTRIBITE.Models;

namespace NutriBite.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CartController(ApplicationDbContext context)
        {
            _context = context;
        }

        public class AddToCartRequest
        {
            public int productId { get; set; }
        }

        // ================= ADD TO CART =================
        [HttpPost]
        public IActionResult AddToCart([FromBody] AddToCartRequest req)
        {
            var uid = HttpContext.Session.GetInt32("UserId");

            if (uid == null || req == null || req.productId <= 0)
                return Json(new { success = false });

            var existingItem = _context.Carttables
                .FirstOrDefault(c => c.Uid == uid.Value && c.Pid == req.productId);

            if (existingItem != null)
            {
                existingItem.Qty += 1;
            }
            else
            {
                var cartItem = new Carttable
                {
                    Uid = uid.Value,
                    Pid = req.productId,
                    Qty = 1,
                    Date = DateTime.Now
                };

                _context.Carttables.Add(cartItem);
            }

            _context.SaveChanges();

            var count = _context.Carttables
                .Where(c => c.Uid == uid.Value)
                .Sum(c => c.Qty);

            return Json(new { success = true, count });
        }

        // ================= REMOVE ITEM =================
        [HttpPost]
        public IActionResult Remove(int id)
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (uid == null) return RedirectToAction("Login", "Auth");

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
        public IActionResult UpdateQuantity(int id, int qty)
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (uid == null) return RedirectToAction("Login", "Auth");

            var item = _context.Carttables
                .FirstOrDefault(c => c.Crid == id && c.Uid == uid.Value);

            if (item != null && qty > 0)
            {
                item.Qty = qty;
                _context.SaveChanges();
            }

            return RedirectToAction("Index");
        }

        // ================= GET CART COUNT =================
        [HttpGet]
        public IActionResult GetCartCount()
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (uid == null)
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

            if (uid == null)
                return RedirectToAction("Login", "Auth");

            var cartItems = _context.Carttables
                .Where(c => c.Uid == uid.Value)
                .Join(_context.Foods,
                      cart => cart.Pid,
                      food => food.Id,
                      (cart, food) => new CartItem
                      {
                          Id = cart.Crid,
                          Name = food.Name,
                          Price = food.Price,
                          Quantity = cart.Qty,
                          ImageUrl = food.ImagePath
                      })
                .ToList();

            return View(cartItems);
        }
        [HttpPost]
        public IActionResult Checkout(string pickupSlot = "12:00 PM")
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (uid == null)
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

                totalItems += item.Qty;
                totalCalories += (food?.Calories ?? 0) * item.Qty;
            }

            var order = new OrderTable
            {
                UserId = uid.Value,
                CustomerName = user?.Name,
                CustomerPhone = user?.Phone,
                TotalItems = totalItems,
                PickupSlot = pickupSlot,
                TotalCalories = totalCalories,
                PaymentStatus = "Pending",
                Status = "Placed",
                IsFlagged = false,
                CreatedAt = DateTime.Now
            };

            _context.OrderTables.Add(order);
            _context.SaveChanges();

            // 🔥 Add Order Items
            foreach (var item in cartItems)
            {
                var food = _context.Foods.FirstOrDefault(f => f.Id == item.Pid);

                _context.OrderItems.Add(new OrderItem
                {
                    OrderId = order.OrderId,
                    ItemName = food?.Name,
                    Quantity = item.Qty,
                    CreatedAt = DateTime.Now
                });
            }

            _context.SaveChanges();

            // 🔥 Clear cart after order
            _context.Carttables.RemoveRange(cartItems);
            _context.SaveChanges();

            return RedirectToAction("Success");
        }
        public IActionResult Success()
        {
            return View();
        }
    }
}