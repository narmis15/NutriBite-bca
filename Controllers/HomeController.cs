using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NUTRIBITE.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using NUTRIBITE.Filters;
using Microsoft.AspNetCore.Authorization; // <-- Add this using directive

namespace NUTRIBITE.Controllers
{
    [SessionAuthorize] // require signed-in session for Home pages
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _configuration;
        private const string AdminEmail = "Nutribite123@gmail.com";
        private const string AdminPassword = "NutriBite//26";

        public HomeController(ILogger<HomeController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            // Load vendor products with category into ViewBag.Foods
            var foods = new List<dynamic>();

            try
            {
                var cs = _configuration.GetConnectionString("DBCS");
                if (!string.IsNullOrWhiteSpace(cs))
                {
                    using var con = new SqlConnection(cs);
                    con.Open();

                    using var cmd = new SqlCommand(@"
                        SELECT p.Id, p.ProductName, p.Price, p.Calories, p.Image, c.CategoryName
                        FROM AddProduct p
                        JOIN AddCategory c ON p.CategoryId = c.Id
                        ORDER BY p.Id DESC
                    ", con);

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        dynamic d = new ExpandoObject();
                        d.Id = reader["Id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Id"]);
                        d.ProductName = reader["ProductName"] == DBNull.Value ? "" : reader["ProductName"].ToString();
                        d.Price = reader["Price"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["Price"]);
                        d.Calories = reader["Calories"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Calories"]);
                        d.Image = reader["Image"] == DBNull.Value ? "" : reader["Image"].ToString();
                        d.CategoryName = reader["CategoryName"] == DBNull.Value ? "" : reader["CategoryName"].ToString();
                        foods.Add(d);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log and fail gracefully; View will simply show no foods
                _logger.LogError(ex, "Failed to load foods for home page");
            }

            ViewBag.Foods = foods;

            var uid = HttpContext.Session.GetInt32("UserId");
            var userName = HttpContext.Session.GetString("UserName") ?? "";
            ViewBag.UserName = userName;

            if (!uid.HasValue)
                return View();

            var connectionString = _configuration.GetConnectionString("DBCS"); // <-- Renamed from 'cs'

            int calorieGoal = 1450;
            int totalCalories = 0;
            int totalProtein = 0;

            try
            {
                using var con = new SqlConnection(connectionString);
                con.Open();

                // Get recommended calories
                using (var cmdGoal = new SqlCommand(@"
        SELECT TOP 1 RecommendedCalories 
        FROM HealthSurveys 
        WHERE UserId = @u 
        ORDER BY CreatedAt DESC", con))
                {
                    cmdGoal.Parameters.AddWithValue("@u", uid.Value);
                    var res = cmdGoal.ExecuteScalar();
                    if (res != null && res != DBNull.Value)
                        calorieGoal = Convert.ToInt32(res);
                }

                // Get today's nutrition
                using (var cmdToday = new SqlCommand(@"
        SELECT 
            ISNULL(SUM(Calories),0) AS TotalCalories,
            ISNULL(SUM(Protein),0) AS TotalProtein
        FROM DailyCalorieEntry
        WHERE UserId = @u
        AND CAST(Date AS DATE) = CAST(GETDATE() AS DATE)", con))
                {
                    cmdToday.Parameters.AddWithValue("@u", uid.Value);

                    using var reader = cmdToday.ExecuteReader();
                    if (reader.Read())
                    {
                        totalCalories = Convert.ToInt32(reader["TotalCalories"]);
                        totalProtein = Convert.ToInt32(reader["TotalProtein"]);
                    }
                }
            }
            catch { }

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

        // Admin login helper left as-is (unchanged behavior).
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
            // Existing admin check kept intact.
            if (!ModelState.IsValid)
            {
                return View(model);
            }

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
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}