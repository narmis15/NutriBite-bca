using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;

namespace NUTRIBITE.Controllers
{
    public class CalorieTrackerController : Controller
    {
        private readonly IConfiguration _configuration;
        public CalorieTrackerController(IConfiguration configuration) => _configuration = configuration;

        // GET: /CalorieTracker
        [HttpGet]
        public IActionResult Index()
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (!uid.HasValue) return RedirectToAction("Login", "Auth");

            return View();
        }

        // POST: /CalorieTracker/AddEntry
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddEntry(string foodName, int calories, decimal protein = 0, decimal carbs = 0, decimal fats = 0)
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (!uid.HasValue) return Json(new { success = false, message = "Not authenticated" });

            if (string.IsNullOrWhiteSpace(foodName) || calories < 0)
                return Json(new { success = false, message = "Invalid input" });

            var cs = _configuration.GetConnectionString("DBCS");
            try
            {
                using var con = new SqlConnection(cs);
                con.Open();
                using var cmd = new SqlCommand(@"
                    INSERT INTO DailyCalorieEntry (UserId, Date, FoodName, Calories, Protein, Carbs, Fats)
                    VALUES (@u, CONVERT(date, GETDATE()), @f, @c, @p, @carb, @fat)
                ", con);
                cmd.Parameters.AddWithValue("@u", uid.Value);
                cmd.Parameters.AddWithValue("@f", foodName.Trim());
                cmd.Parameters.AddWithValue("@c", calories);
                cmd.Parameters.AddWithValue("@p", protein);
                cmd.Parameters.AddWithValue("@carb", carbs);
                cmd.Parameters.AddWithValue("@fat", fats);
                cmd.ExecuteNonQuery();
                return Json(new { success = true });
            }
            catch
            {
                return Json(new { success = false, message = "Unable to add entry." });
            }
        }

        // GET: /CalorieTracker/GetTodayEntries
        [HttpGet]
        public IActionResult GetTodayEntries()
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (!uid.HasValue) return Json(new { authenticated = false });

            var cs = _configuration.GetConnectionString("DBCS");
            var list = new List<object>();
            try
            {
                using var con = new SqlConnection(cs);
                con.Open();
                using var cmd = new SqlCommand("SELECT Id, FoodName, Calories, Protein, Carbs, Fats FROM DailyCalorieEntry WHERE UserId = @u AND Date = CONVERT(date, GETDATE()) ORDER BY Id DESC", con);
                cmd.Parameters.AddWithValue("@u", uid.Value);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new
                    {
                        id = Convert.ToInt32(r["Id"]),
                        food = r["FoodName"] as string ?? "",
                        calories = Convert.ToInt32(r["Calories"]),
                        protein = Convert.ToDecimal(r["Protein"]),
                        carbs = Convert.ToDecimal(r["Carbs"]),
                        fats = Convert.ToDecimal(r["Fats"])
                    });
                }
            }
            catch
            {
                // ignore
            }

            return Json(new { authenticated = true, items = list });
        }
    }
}