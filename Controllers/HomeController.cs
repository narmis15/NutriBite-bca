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

        // =========================
        // ✅ PUBLIC HOME PAGE
        // =========================
        [AllowAnonymous]
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

            // 🔹 Get latest recommended calories
            var survey = _context.HealthSurveys
                .Where(h => h.UserId == uid.Value)
                .OrderByDescending(h => h.CreatedAt)
                .FirstOrDefault();

            if (survey != null)
                calorieGoal = (int)survey.RecommendedCalories;

            var today = DateTime.Today;

            var todayEntries = _context.DailyCalorieEntries
                .Where(d => d.UserId == uid.Value && d.Date.Date == today)
                .ToList();

            int totalCalories = todayEntries.Sum(d => d.Calories);
            int totalProtein = (int)todayEntries.Sum(d => d.Protein);

            ViewBag.CalorieGoal = calorieGoal;
            ViewBag.TotalCalories = totalCalories;
            ViewBag.TotalProtein = totalProtein;
            ViewBag.RemainingCalories = calorieGoal - totalCalories;

            double progress = calorieGoal > 0
                ? (double)totalCalories / calorieGoal * 100
                : 0;

            ViewBag.Progress = progress > 100 ? 100 : progress;

            return View();
        }

        // =========================
        // ✅ PUBLIC ABOUT PAGE
        // =========================
        [AllowAnonymous]
        public IActionResult About()
        {
            return View();
        }

        // =========================
        // ✅ PUBLIC LOCATION PAGE
        // =========================
        [AllowAnonymous]
        public IActionResult Location()
        {
            return View();
        }

        // =========================
        // ADMIN LOGIN (Optional)
        // =========================
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

            var email = (model.Email ?? "").Trim();

            if (string.Equals(email, AdminEmail, StringComparison.OrdinalIgnoreCase)
                && string.Equals(model.Password ?? "", AdminPassword, StringComparison.Ordinal))
            {
                _logger.LogInformation("Admin login succeeded for {Email}", email);
                return Redirect("/Admin/Dashboard");
            }

            _logger.LogWarning("Invalid login attempt for {Email}", email);
            ModelState.AddModelError(string.Empty, "Invalid email or password");

            return View(model);
        }

        // =========================
        // ERROR PAGE
        // =========================
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
        [SessionAuthorize]
        public IActionResult MyProfile()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
                return RedirectToAction("Login", "Auth");

            var user = _context.UserSignups
                .FirstOrDefault(u => u.Id == userId.Value);

            if (user == null)
                return RedirectToAction("Login", "Auth");

            // Latest Health Survey
            var survey = _context.HealthSurveys
                .Where(h => h.UserId == userId.Value)
                .OrderByDescending(h => h.CreatedAt)
                .FirstOrDefault();

            ViewBag.User = user;
            ViewBag.Survey = survey;

            return View();
        }

        [SessionAuthorize]
        public IActionResult MyOrders()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (!userId.HasValue)
                return RedirectToAction("Login", "Auth");

            var orders = _context.OrderTables
                .Where(o => o.UserId == userId.Value)
                .OrderByDescending(o => o.CreatedAt)
                .ToList();

            return View(orders);
        }

        [SessionAuthorize]
        public IActionResult TrackOrder(int orderId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue) return RedirectToAction("Login", "Auth");

            var order = _context.OrderTables
                .FirstOrDefault(o => o.OrderId == orderId && o.UserId == userId.Value);

            if (order == null) return NotFound();

            return View(order);
        }
        [HttpGet]
        public IActionResult EditProfile()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
                return RedirectToAction("Login", "Auth");

            var user = _context.UserSignups
                .FirstOrDefault(u => u.Id == userId.Value);

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditProfile(UserSignup model)
        {
            var user = _context.UserSignups
                .FirstOrDefault(u => u.Id == model.Id);

            if (user == null)
                return RedirectToAction("MyProfile");

            user.Name = model.Name;
            user.Email = model.Email;

            _context.SaveChanges();

            HttpContext.Session.SetString("UserName", user.Name);

            return RedirectToAction("MyProfile");
        }

    }


}