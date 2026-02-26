using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using NUTRIBITE.ViewModels;
using NUTRIBITE.Services;
using System;
using NUTRIBITE.Models;

namespace NUTRIBITE.Controllers
{
    public class HealthSurveyController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IHealthCalculationService _calc;

        public HealthSurveyController(IConfiguration configuration, IHealthCalculationService calc)
        {
            _configuration = configuration;
            _calc = calc;
        }

        // GET: /HealthSurvey
        [HttpGet]
        public IActionResult Index()
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (!uid.HasValue)
                return RedirectToAction("Login", "Auth");

            // Prevent revisiting if survey exists
            string cs = _configuration.GetConnectionString("DBCS");
            try
            {
                using var con = new SqlConnection(cs);
                con.Open();
                using var cmd = new SqlCommand("SELECT TOP 1 Id FROM HealthSurveys WHERE UserId = @u", con);
                cmd.Parameters.AddWithValue("@u", uid.Value);
                var existing = cmd.ExecuteScalar();
                if (existing != null)
                    return RedirectToAction("Result");
            }
            catch
            {
                // fail-open
            }

            var vm = new HealthSurveyViewModel
            {
                Age = 25,
                Gender = "Male",
                HeightCm = 170,
                WeightKg = 70,
                ActivityLevel = "Sedentary",
                Goal = "Maintain",
                DietaryPreference = "Vegetarian"
            };

            return View(vm);
        }

        // POST: /HealthSurvey
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(HealthSurveyViewModel model)
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (!uid.HasValue)
                return RedirectToAction("Login", "Auth");

            if (!ModelState.IsValid)
                return View(model);

            string cs = _configuration.GetConnectionString("DBCS");

            // Prevent duplicate
            try
            {
                using var conCheck = new SqlConnection(cs);
                conCheck.Open();
                using var cmdCheck = new SqlCommand("SELECT COUNT(1) FROM HealthSurveys WHERE UserId = @u", conCheck);
                cmdCheck.Parameters.AddWithValue("@u", uid.Value);
                int exists = Convert.ToInt32(cmdCheck.ExecuteScalar());
                if (exists > 0)
                    return RedirectToAction("Result");
            }
            catch
            {
                // ignore
            }

            // Run calculations
            var calcResult = _calc.Calculate(model);

            // Persist all fields
            try
            {
                using var con = new SqlConnection(cs);
                con.Open();
                using var cmd = new SqlCommand(@"
                    INSERT INTO HealthSurveys
                    (UserId, Age, Gender, HeightCm, WeightKg, ActivityLevel, Goal, ChronicDiseases, FoodAllergies, DietaryPreference, Smoking, Alcohol, BMI, BMR, RecommendedCalories, RecommendedProtein, CreatedAt)
                    VALUES
                    (@UserId, @Age, @Gender, @HeightCm, @WeightKg, @ActivityLevel, @Goal, @ChronicDiseases, @FoodAllergies, @DietaryPreference, @Smoking, @Alcohol, @BMI, @BMR, @RecommendedCalories, @RecommendedProtein, GETUTCDATE());
                ", con);

                cmd.Parameters.AddWithValue("@UserId", uid.Value);
                cmd.Parameters.AddWithValue("@Age", model.Age);
                cmd.Parameters.AddWithValue("@Gender", model.Gender ?? "");
                cmd.Parameters.AddWithValue("@HeightCm", model.HeightCm);
                cmd.Parameters.AddWithValue("@WeightKg", model.WeightKg);
                cmd.Parameters.AddWithValue("@ActivityLevel", model.ActivityLevel ?? "");
                cmd.Parameters.AddWithValue("@Goal", model.Goal ?? "");
                cmd.Parameters.AddWithValue("@ChronicDiseases", model.ChronicDiseases ?? "");
                cmd.Parameters.AddWithValue("@FoodAllergies", model.FoodAllergies ?? "");
                cmd.Parameters.AddWithValue("@DietaryPreference", model.DietaryPreference ?? "");
                cmd.Parameters.AddWithValue("@Smoking", model.Smoking);
                cmd.Parameters.AddWithValue("@Alcohol", model.Alcohol);
                cmd.Parameters.AddWithValue("@BMI", calcResult.BMI);
                cmd.Parameters.AddWithValue("@BMR", calcResult.BMR);
                cmd.Parameters.AddWithValue("@RecommendedCalories", calcResult.RecommendedCalories);
                cmd.Parameters.AddWithValue("@RecommendedProtein", calcResult.RecommendedProtein);

                cmd.ExecuteNonQuery();
            }
            catch
            {
                ModelState.AddModelError("", "Unable to save survey. Please try again later.");
                return View(model);
            }

            return RedirectToAction("Result");
        }

        // GET: /HealthSurvey/Result
        [HttpGet]
        public IActionResult Result()
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (!uid.HasValue)
                return RedirectToAction("Login", "Auth");

            string cs = _configuration.GetConnectionString("DBCS");
            HealthSurvey survey = null;

            try
            {
                using var con = new SqlConnection(cs);
                con.Open();
                using var cmd = new SqlCommand("SELECT TOP 1 * FROM HealthSurveys WHERE UserId = @u ORDER BY CreatedAt DESC", con);
                cmd.Parameters.AddWithValue("@u", uid.Value);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    survey = new HealthSurvey
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        UserId = Convert.ToInt32(reader["UserId"]),
                        Age = Convert.ToInt32(reader["Age"]),
                        Gender = reader["Gender"].ToString(),
                        HeightCm = Convert.ToDecimal(reader["HeightCm"]),
                        WeightKg = Convert.ToDecimal(reader["WeightKg"]),
                        ActivityLevel = reader["ActivityLevel"].ToString(),
                        Goal = reader["Goal"].ToString(),
                        ChronicDiseases = reader["ChronicDiseases"].ToString(),
                        FoodAllergies = reader["FoodAllergies"].ToString(),
                        DietaryPreference = reader["DietaryPreference"].ToString(),
                        Smoking = Convert.ToBoolean(reader["Smoking"]),
                        Alcohol = Convert.ToBoolean(reader["Alcohol"]),
                        BMI = reader["BMI"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["BMI"]),
                        BMR = reader["BMR"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["BMR"]),
                        RecommendedCalories = reader["RecommendedCalories"] == DBNull.Value ? 0 : Convert.ToInt32(reader["RecommendedCalories"]),
                        RecommendedProtein = reader["RecommendedProtein"] == DBNull.Value ? 0 : Convert.ToInt32(reader["RecommendedProtein"]),
                        CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                    };
                }
            }
            catch
            {
                // ignore
            }

            if (survey == null)
                return RedirectToAction("Index");

            // Suggest foods using service
            var suggestions = _calc.SuggestFoods(survey.DietaryPreference ?? "");

            ViewBag.FoodSuggestions = suggestions;
            return View(survey);
        }
    }
}