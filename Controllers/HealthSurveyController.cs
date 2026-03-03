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

            var survey = _context.HealthSurveys
                .FirstOrDefault(h => h.UserId == uid.Value);

            // If survey exists → load data into form (EDIT MODE)
            if (survey != null)
            {
                var vm = new HealthSurveyViewModel
                {
                    Age = survey.Age,
                    Gender = survey.Gender,

                    HeightCm = survey.HeightCm ?? 0, // Fix: handle nullable decimal
                    WeightKg = survey.WeightKg ?? 0, // Fix: handle nullable decimal
                    ActivityLevel = survey.ActivityLevel,
                    Goal = survey.Goal,
                    ChronicDiseases = survey.ChronicDiseases,
                    FoodAllergies = survey.FoodAllergies,
                    DietaryPreference = survey.DietaryPreference,
                    Smoking = survey.Smoking,
                    Alcohol = survey.Alcohol
                };

                return View(vm);
            }

            // First time survey
            return View(new HealthSurveyViewModel());
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

            var survey = _context.HealthSurveys
                .FirstOrDefault(h => h.UserId == uid.Value);

            // Run calculations
            var calcResult = _calc.Calculate(model);

            if (survey == null)
            {
                // CREATE NEW
                survey = new HealthSurvey
                {
                    UserId = uid.Value,
                    CreatedAt = DateTime.UtcNow
                };

                _context.HealthSurveys.Add(survey);
            }

            // UPDATE VALUES
            survey.Age = model.Age;
            survey.Gender = model.Gender ?? "";
            survey.HeightCm = model.HeightCm;
            survey.WeightKg = model.WeightKg;
            survey.ActivityLevel = model.ActivityLevel ?? "";
            survey.Goal = model.Goal ?? "";
            survey.ChronicDiseases = model.ChronicDiseases ?? "";
            survey.FoodAllergies = model.FoodAllergies ?? "";
            survey.DietaryPreference = model.DietaryPreference ?? "";
            survey.Smoking = model.Smoking;
            survey.Alcohol = model.Alcohol;

            // Calculated fields
            survey.Bmi = calcResult.BMI;
            survey.Bmr = calcResult.BMR;
            survey.RecommendedCalories = calcResult.RecommendedCalories;
            survey.RecommendedProtein = calcResult.RecommendedProtein;

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
                .FirstOrDefault(h => h.UserId == uid.Value);

            if (survey == null)
                return RedirectToAction("Index");

            var suggestions = _calc.SuggestFoods(survey.DietaryPreference ?? "");

            ViewBag.FoodSuggestions = suggestions;
            return View(survey);
        }
    }
}