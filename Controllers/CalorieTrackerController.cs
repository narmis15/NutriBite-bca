using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Collections.Generic;
using global::NUTRIBITE.Models;
using Microsoft.AspNetCore.SignalR;
using global::NUTRIBITE.Hubs;
using System.Threading.Tasks;

namespace NUTRIBITE.Controllers
{
    public class CalorieTrackerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<AnalyticsHub> _hubContext;

        public CalorieTrackerController(ApplicationDbContext context, IHubContext<AnalyticsHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
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

            var today = DateTime.Today;

            // Get survey for recommended calories
            var survey = _context.HealthSurveys
                .FirstOrDefault(h => h.UserId == uid.Value);

            int recommendedCalories = survey?.RecommendedCalories ?? 2000;

            // Calculate today's calories
            int todayCalories = _context.DailyCalorieEntries
                .Where(d => d.UserId == uid.Value && d.Date == today)
                .Sum(d => d.Calories);

            ViewBag.RecommendedCalories = recommendedCalories;
            ViewBag.TodayCalories = todayCalories;
            ViewBag.RemainingCalories = recommendedCalories - todayCalories;

            return View();
        }

        // POST: /CalorieTracker/AddEntry
        // =============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddEntry(string foodName, int calories, string mealType = "Other", decimal protein = 0, decimal carbs = 0, decimal fats = 0)
        {
            var uid = HttpContext.Session.GetInt32("UserId");

            if (!uid.HasValue)
                return Json(new { success = false, message = "User not logged in" });

            if (string.IsNullOrWhiteSpace(foodName) || calories <= 0)
                return Json(new { success = false, message = "Invalid food entry" });

            var entry = new DailyCalorieEntry
            {
                UserId = uid.Value,
                Date = DateTime.Today,
                FoodName = foodName.Trim(),
                Calories = calories,
                MealType = mealType,
                Protein = protein,
                Carbs = carbs,
                Fats = fats
            };

            _context.DailyCalorieEntries.Add(entry);
            _context.SaveChanges();

            // Trigger real-time update for admin analytics
            await _hubContext.Clients.All.SendAsync("ReceiveUpdate", $"New calorie entry: {foodName} ({calories} kcal)");

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

            var today = DateTime.Today;

            var entries = _context.DailyCalorieEntries
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

            return Json(new
            {
                authenticated = true,
                items = entries
            });
        }

        // =============================
        // GET: /CalorieTracker/GetWeeklyData
        // =============================
        [HttpGet]
        public IActionResult GetWeeklyData()
        {
            var uid = HttpContext.Session.GetInt32("UserId");

            if (!uid.HasValue)
                return Json(new { authenticated = false });

            var startDate = DateTime.Today.AddDays(-6);

            var weekly = _context.DailyCalorieEntries
                .Where(d => d.UserId == uid.Value && d.Date >= startDate)
                .GroupBy(d => d.Date)
                .Select(g => new
                {
                    date = g.Key,
                    calories = g.Sum(x => x.Calories)
                })
                .OrderBy(g => g.date)
                .ToList();

            return Json(new
            {
                authenticated = true,
                data = weekly
            });
        }
        [HttpGet]
        public IActionResult GetAnalytics(string fromDate, string toDate)
        {
            var uid = HttpContext.Session.GetInt32("UserId");

            if (!uid.HasValue)
                return Json(null);

            var today = DateTime.Today;

            var todayCalories = _context.DailyCalorieEntries
                .Where(d => d.UserId == uid.Value && d.Date.Date == today)
                .Sum(d => (int?)d.Calories) ?? 0;

            var survey = _context.HealthSurveys
                .Where(h => h.UserId == uid.Value)
                .OrderByDescending(h => h.CreatedAt)
                .FirstOrDefault();

            int recommended = survey != null ? (int)survey.RecommendedCalories : 2000;

            var query = _context.DailyCalorieEntries.Where(d => d.UserId == uid.Value);

            if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, out var fromDt))
            {
                query = query.Where(d => d.Date >= fromDt.Date);
            }
            if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, out var toDt))
            {
                query = query.Where(d => d.Date <= toDt.Date);
            }

            var dailyData = query
                .GroupBy(d => d.Date.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Calories = g.Sum(x => x.Calories)
                })
                .OrderBy(x => x.Date)
                .ToList();

            int average = dailyData.Any()
                ? (int)dailyData.Average(x => x.Calories)
                : 0;

            int peak = dailyData.Any()
                ? dailyData.Max(x => x.Calories)
                : 0;

            double comparison = recommended > 0
                ? (double)todayCalories / recommended * 100
                : 0;

            var trend = dailyData.Select(d => new
            {
                label = d.Date.ToString("dd MMM"),
                calories = d.Calories
            });

            return Json(new
            {
                userName = HttpContext.Session.GetString("UserName"),
                averageDaily = average,
                peakDaily = peak,
                recommendedDaily = recommended,
                comparisonPercent = Math.Round(comparison, 1),
                todayCalories = todayCalories,
                trend = trend
            });
        }
    }
}