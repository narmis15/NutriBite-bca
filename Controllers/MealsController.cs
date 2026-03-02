using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Collections.Generic;
using NUTRIBITE.Models;

namespace NUTRIBITE.Controllers
{
    public class MealsController : Controller
    {
        private readonly ApplicationDbContext _context;

        private static readonly string[] ValidSlots =
            new[] { "Breakfast", "Lunch", "Dinner", "Snacks" };

        public MealsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =============================
        // POST /Meals/Add
        // =============================
        [HttpPost]
        public IActionResult Add(int foodId, string slot, string? date)
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (!uid.HasValue)
                return Json(new { success = false, authenticated = false });

            if (foodId <= 0)
                return Json(new { success = false, message = "Invalid food id." });

            if (string.IsNullOrWhiteSpace(slot) ||
                !ValidSlots.Contains(slot, StringComparer.OrdinalIgnoreCase))
                return Json(new { success = false, message = "Invalid slot." });

            DateTime mealDate;
            if (!string.IsNullOrWhiteSpace(date) &&
                DateTime.TryParse(date, out var parsed))
                mealDate = parsed.Date;
            else
                mealDate = DateTime.Today;

            bool exists = _context.Meals.Any(m =>
                m.UserId == uid.Value &&
                m.FoodId == foodId &&
                m.Slot == slot &&
                m.MealDate.Date == mealDate);

            if (exists)
            {
                return Json(new
                {
                    success = true,
                    added = false,
                    message = "Already added for this slot."
                });
            }

            var meal = new Meal
            {
                UserId = uid.Value,
                FoodId = foodId,
                Slot = slot,
                MealDate = mealDate,
                CreatedAt = DateTime.Now
            };

            _context.Meals.Add(meal);
            _context.SaveChanges();

            return Json(new
            {
                success = true,
                added = true,
                slot,
                date = mealDate.ToString("yyyy-MM-dd")
            });
        }

        // =============================
        // GET /Meals/Today
        // =============================
        [HttpGet]
        public IActionResult Today(string? date)
        {
            var uid = HttpContext.Session.GetInt32("UserId");

            DateTime mealDate;
            if (!string.IsNullOrWhiteSpace(date) &&
                DateTime.TryParse(date, out var parsed))
                mealDate = parsed.Date;
            else
                mealDate = DateTime.Today;

            if (!uid.HasValue)
            {
                return Json(new
                {
                    authenticated = false,
                    date = mealDate.ToString("yyyy-MM-dd"),
                    meals = new object[] { },
                    totalCalories = 0
                });
            }

            var meals = _context.Meals
                .Where(m => m.UserId == uid.Value &&
                            m.MealDate.Date == mealDate)
                .Join(_context.Foods,
                      m => m.FoodId,
                      f => f.Id,
                      (m, f) => new
                      {
                          id = m.Id,
                          foodId = f.Id,
                          name = f.Name,
                          calories = f.Calories ?? 0,
                          slot = m.Slot,
                          mealDate = m.MealDate
                      })
                .OrderBy(m => m.slot)
                .ToList();

            int totalCalories = meals.Sum(m => m.calories);

            return Json(new
            {
                authenticated = true,
                date = mealDate.ToString("yyyy-MM-dd"),
                meals,
                totalCalories
            });
        }

        // =============================
        // SEARCH
        // =============================
        [HttpGet]
        public IActionResult Search(string q)
        {
            if (string.IsNullOrWhiteSpace(q))
                return Json(new object[] { });

            var results = _context.Foods
                .Where(f => f.Name.Contains(q))
                .OrderBy(f => f.Name)
                .Take(50)
                .Select(f => new
                {
                    id = f.Id,
                    name = f.Name,
                    description = f.Description ?? "",
                    image = f.ImagePath ?? "/images/placeholder.png",
                    calories = f.Calories ?? 0
                })
                .ToList();

            return Json(results);
        }
    }
}