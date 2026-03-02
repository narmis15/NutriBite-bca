using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using NUTRIBITE.Models;
using System;
using System.Collections.Generic;

namespace NutriBite.Controllers
{
    public class CartController : Controller
    {
        private readonly IConfiguration _configuration;

        public CartController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public class AddToCartRequest
        {
            public int productId { get; set; }
        }

        [HttpPost]
        public IActionResult AddToCart([FromBody] AddToCartRequest req)
        {
            if (req == null || req.productId <= 0)
                return Json(new { success = false });

            var uid = HttpContext.Session.GetInt32("UserId");
            if (!uid.HasValue)
                return Json(new { success = false }); // not logged in

            try
            {
                var cs = _configuration.GetConnectionString("DBCS");
                if (string.IsNullOrWhiteSpace(cs))
                    return Json(new { success = false });

                using var con = new SqlConnection(cs);
                con.Open();

                // Optional: If you want to increment quantity if exists, implement CHECK -> UPDATE, else INSERT.
                // For now we insert a new row with Quantity = 1.
                using var cmd = new SqlCommand(@"
                    INSERT INTO Carttable (UserId, ProductId, Quantity)
                    VALUES (@uid, @pid, @qty);
                ", con);

                cmd.Parameters.AddWithValue("@uid", uid.Value);
                cmd.Parameters.AddWithValue("@pid", req.productId);
                cmd.Parameters.AddWithValue("@qty", 1);

                cmd.ExecuteNonQuery();
                return Json(new { success = true });
            }
            catch (Exception)
            {
                // Log if needed
                return Json(new { success = false });
            }
        }

        public IActionResult Index()
        {
            // Temporary dummy data
            var cartItems = new List<CartItem>
            {
                new CartItem { Id = 1, Name = "Paneer Thali", Price = 199, Quantity = 1, ImageUrl = "/images/food1.jpg" },
                new CartItem { Id = 2, Name = "Healthy Salad Bowl", Price = 149, Quantity = 2, ImageUrl = "/images/food2.jpg" }
            };

            return View(cartItems);
        }
    }
}
