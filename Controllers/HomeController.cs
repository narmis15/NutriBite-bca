using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NUTRIBITE.Models;
using NUTRIBITE.Filters;

namespace NUTRIBITE.Controllers
{
    [SessionAuthorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        private const string AdminEmail = "Nutribite123@gmail.com";
        private const string AdminPassword = "NutriBite//26";

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            var userName = HttpContext.Session.GetString("UserName") ?? "";
            ViewBag.UserName = userName;

            // 🔹 Load vendor foods (only active ones)
            var foods = _context.Foods
                .Where(f => f.Status == "Active" || f.Status == null)
                .OrderByDescending(f => f.Id)
                .ToList();

            ViewBag.Foods = foods;

            if (!uid.HasValue)
                return View();

            int calorieGoal = 1450;
            int totalCalories = 0;
            int totalProtein = 0;

            // 🔹 Get latest recommended calories
            var survey = _context.HealthSurveys
                .Where(h => h.UserId == uid.Value)
                .OrderByDescending(h => h.CreatedAt)
                .FirstOrDefault();

            if (survey != null)
                calorieGoal =(int) survey.RecommendedCalories;

            // 🔹 Get today's nutrition
            var today = DateTime.Today;
            var todayDateOnly = DateOnly.FromDateTime(today);

            var todayEntries = _context.DailyCalorieEntries
                .Where(d => d.UserId == uid.Value &&
                            d.Date == todayDateOnly)
                .ToList();

            totalCalories = todayEntries.Sum(d => d.Calories);
            totalProtein = (int)todayEntries.Sum(d => d.Protein);

            ViewBag.CalorieGoal = calorieGoal;
            ViewBag.TotalCalories = totalCalories;
            ViewBag.TotalProtein = totalProtein;
            ViewBag.RemainingCalories = calorieGoal - totalCalories;

            double progress = (double)totalCalories / calorieGoal * 100;
            ViewBag.Progress = progress > 100 ? 100 : progress;

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login()
        {
            return RedirectToAction("Login", "Auth");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public IActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var email = (model.Email ?? string.Empty).Trim();

            if (string.Equals(email, AdminEmail, StringComparison.OrdinalIgnoreCase)
                && string.Equals(model.Password ?? string.Empty, AdminPassword, StringComparison.Ordinal))
            {
                _logger.LogInformation("Admin login succeeded for {Email}", email);
                return Redirect("/Admin/Dashboard");
            }

            _logger.LogWarning("Invalid login attempt for {Email}", email);
            ModelState.AddModelError(string.Empty, "Invalid email or password");
            return View(model);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}