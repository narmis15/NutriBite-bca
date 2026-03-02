using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using NUTRIBITE.ViewModels;
using NUTRIBITE.Services;
using NUTRIBITE.Models;

namespace NUTRIBITE.Controllers
{
    public class HealthSurveyController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHealthCalculationService _calc;

        public HealthSurveyController(ApplicationDbContext context,
                                      IHealthCalculationService calc)
        {
            _context = context;
            _calc = calc;
        }

        // =========================
        // GET: /HealthSurvey
        // =========================
        [HttpGet]
        public IActionResult Index()
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (!uid.HasValue)
                return RedirectToAction("Login", "Auth");

            // Prevent revisiting if survey exists
            bool exists = _context.HealthSurveys
                .Any(h => h.UserId == uid.Value);

            if (exists)
                return RedirectToAction("Result");

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

        // =========================
        // POST: /HealthSurvey
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(HealthSurveyViewModel model)
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (!uid.HasValue)
                return RedirectToAction("Login", "Auth");

            if (!ModelState.IsValid)
                return View(model);

            // Prevent duplicate
            bool exists = _context.HealthSurveys
                .Any(h => h.UserId == uid.Value);

            if (exists)
                return RedirectToAction("Result");

            // Run calculations
            var calcResult = _calc.Calculate(model);

            var survey = new HealthSurvey
            {
                UserId = uid.Value,
                Age = model.Age,
                Gender = model.Gender ?? "",
                HeightCm = model.HeightCm,
                WeightKg = model.WeightKg,
                ActivityLevel = model.ActivityLevel ?? "",
                Goal = model.Goal ?? "",
                ChronicDiseases = model.ChronicDiseases ?? "",
                FoodAllergies = model.FoodAllergies ?? "",
                DietaryPreference = model.DietaryPreference ?? "",
                Smoking = model.Smoking,
                Alcohol = model.Alcohol,
                Bmi = calcResult.BMI,
                Bmr = calcResult.BMR,
                RecommendedCalories = calcResult.RecommendedCalories,
                RecommendedProtein = calcResult.RecommendedProtein,
                CreatedAt = DateTime.UtcNow
            };

            _context.HealthSurveys.Add(survey);
            _context.SaveChanges();

            return RedirectToAction("Result");
        }

        // =========================
        // GET: /HealthSurvey/Result
        // =========================
        [HttpGet]
        public IActionResult Result()
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (!uid.HasValue)
                return RedirectToAction("Login", "Auth");

            var survey = _context.HealthSurveys
                .Where(h => h.UserId == uid.Value)
                .OrderByDescending(h => h.CreatedAt)
                .FirstOrDefault();

            if (survey == null)
                return RedirectToAction("Index");

            var suggestions = _calc.SuggestFoods(survey.DietaryPreference ?? "");

            ViewBag.FoodSuggestions = suggestions;
            return View(survey);
        }
    }
}