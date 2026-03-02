using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Collections.Generic;
using NUTRIBITE.Models;

namespace NUTRIBITE.Controllers
{
    public class CalorieTrackerController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CalorieTrackerController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =============================
        // GET: /CalorieTracker
        // =============================
        [HttpGet]
        public IActionResult Index()
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (!uid.HasValue)
                return RedirectToAction("Login", "Auth");

            return View();
        }

        // =============================
        // POST: /CalorieTracker/AddEntry
        // =============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddEntry(string foodName, int calories, decimal protein = 0, decimal carbs = 0, decimal fats = 0)
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (!uid.HasValue)
                return Json(new { success = false, message = "Not authenticated" });

            if (string.IsNullOrWhiteSpace(foodName) || calories < 0)
                return Json(new { success = false, message = "Invalid input" });

            var entry = new DailyCalorieEntry
            {
                UserId = uid.Value,
                Date = DateOnly.FromDateTime(DateTime.Today),
                FoodName = foodName.Trim(),
                Calories = calories,
                Protein = protein,
                Carbs = carbs,
                Fats = fats
            };

            _context.DailyCalorieEntries.Add(entry);
            _context.SaveChanges();

            return Json(new { success = true });
        }

        // =============================
        // GET: /CalorieTracker/GetTodayEntries
        // =============================
        [HttpGet]
        public IActionResult GetTodayEntries()
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (!uid.HasValue)
                return Json(new { authenticated = false });

            var today = DateOnly.FromDateTime(DateTime.Today);

            var items = _context.DailyCalorieEntries
                .Where(d => d.UserId == uid.Value && d.Date == today)
                .OrderByDescending(d => d.Id)
                .Select(d => new
                {
                    id = d.Id,
                    food = d.FoodName,
                    calories = d.Calories,
                    protein = d.Protein ?? 0,
                    carbs = d.Carbs ?? 0,
                    fats = d.Fats ?? 0
                })
                .ToList();

            return Json(new { authenticated = true, items });
        }
    }
}