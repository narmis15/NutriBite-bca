using Microsoft.AspNetCore.Mvc;
using NUTRIBITE.Models;
using System.Collections.Generic;

namespace NutriBite.Controllers
{
    public class CartController : Controller
    {
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
