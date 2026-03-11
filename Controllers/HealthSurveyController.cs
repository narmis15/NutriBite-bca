using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Collections.Generic;
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

            if (survey != null)
                return RedirectToAction("Result");

            return View(new HealthSurveyViewModel());
        }

        // =========================
        // GET: /HealthSurvey/Edit
        // =========================
        [HttpGet]
        public IActionResult Edit()
        {
            var uid = HttpContext.Session.GetInt32("UserId");

            if (!uid.HasValue)
                return RedirectToAction("Login", "Auth");

            var survey = _context.HealthSurveys
                .FirstOrDefault(h => h.UserId == uid.Value);

            if (survey == null)
                return RedirectToAction("Index");

            var vm = new HealthSurveyViewModel
            {
                Age = survey.Age,
                Gender = survey.Gender,
                HeightCm = survey.HeightCm ?? 0,
                WeightKg = survey.WeightKg ?? 0,
                ActivityLevel = survey.ActivityLevel,
                Goal = survey.Goal,
                ChronicDiseases = survey.ChronicDiseases,
                FoodAllergies = survey.FoodAllergies,
                DietaryPreference = survey.DietaryPreference,
                Smoking = survey.Smoking,
                Alcohol = survey.Alcohol
            };

            return View("Index", vm);
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

            var calcResult = _calc.Calculate(model);

            if (survey == null)
            {
                survey = new HealthSurvey
                {
                    UserId = uid.Value,
                    CreatedAt = DateTime.UtcNow
                };

                _context.HealthSurveys.Add(survey);
            }

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

            // =========================
            // GET TODAY CALORIES
            // =========================
            var today = DateTime.Today;

            int todayCalories = _context.DailyCalorieEntries
                .Where(d => d.UserId == uid.Value && d.Date.Date == today)
                .Sum(d => (int?)d.Calories) ?? 0;

            int recommendedCalories = (int)(survey.RecommendedCalories ?? 2000);

            int remainingCalories = recommendedCalories - todayCalories;

            // =========================
            // BASE FOOD QUERY
            // =========================
            var foodQuery = _context.Foods.AsQueryable();

            // =========================
            // DIETARY FILTER
            // =========================
            if (!string.IsNullOrEmpty(survey.DietaryPreference))
            {
                var diet = survey.DietaryPreference.ToLower();

                if (diet == "vegetarian" || diet == "vegan")
                {
                    foodQuery = foodQuery.Where(f => f.FoodType == "Vegetarian");
                }
                else if (diet == "eggetarian")
                {
                    foodQuery = foodQuery.Where(f => f.FoodType == "Vegetarian" || f.FoodType == "Eggetarian");
                }
                else if (diet == "non-vegetarian")
                {
                    foodQuery = foodQuery.Where(f => f.FoodType == "Non-Vegetarian"
                                                  || f.FoodType == "Vegetarian"
                                                  || f.FoodType == "Eggetarian");
                }
            }

            // =========================
            // GOAL BASED FILTER
            // =========================
            if (!string.IsNullOrEmpty(survey.Goal))
            {
                if (survey.Goal == "Weight Loss")
                {
                    foodQuery = foodQuery.Where(f => f.Calories <= 450);
                }
                else if (survey.Goal == "Muscle Gain")
                {
                    foodQuery = foodQuery.Where(f => f.Calories >= 500 && f.Calories <= 900);
                }
            }

            // =========================
            // SMART CALORIE FILTER
            // =========================
            if (remainingCalories > 0)
            {
                foodQuery = foodQuery.Where(f => f.Calories <= remainingCalories);
            }

            // =========================
            // FINAL FOOD LIST
            // =========================
            var foods = foodQuery
                .OrderBy(f => f.Calories)
                .Take(6)
                .ToList();

            ViewBag.FoodSuggestions = foods;

            ViewBag.TodayCalories = todayCalories;
            ViewBag.RecommendedCalories = recommendedCalories;
            ViewBag.RemainingCalories = remainingCalories;

            return View(survey);
        }
    }
}