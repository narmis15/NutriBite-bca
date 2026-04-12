using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
                    foodQuery = foodQuery.Where(f => f.Calories <= 600); 
                }
                else if (survey.Goal == "Muscle Gain" || survey.Goal == "Weight Gain")
                {
                    // For muscle/weight gain, prioritize higher protein or higher calorie
                    foodQuery = foodQuery.Where(f => f.Calories >= 500); 
                }
                else if (survey.Goal == "Maintain")
                {
                    // Balanced maintenance constraint
                    foodQuery = foodQuery.Where(f => f.Calories >= 300 && f.Calories <= 800);
                }
            }

            // =========================
            // SMART CALORIE FILTER
            // =========================
            // Only apply strict remaining calorie filter if we have enough calories left
            if (remainingCalories > 300) 
            {
                foodQuery = foodQuery.Where(f => f.Calories <= remainingCalories);
            }

            // =========================
            // FINAL FOOD LIST
            // =========================
            var foods = foodQuery
                .OrderBy(f => f.Calories)
                .Take(12) // Show more options
                .ToList();

            // If no foods match strict goal, show general healthy options
            if (!foods.Any())
            {
                foods = _context.Foods
                    .Where(f => f.Calories <= 800)
                    .OrderBy(f => Guid.NewGuid()) // Random healthy suggestions
                    .Take(6)
                    .ToList();
            }

            ViewBag.FoodSuggestions = foods;

            ViewBag.TodayCalories = todayCalories;
            ViewBag.RecommendedCalories = recommendedCalories;
            ViewBag.RemainingCalories = remainingCalories;

            // =========================
            // ALIGN RECOMMENDATIONS WITH HOME PAGE
            // =========================
            var activeFoodsQuery = _context.Foods
                .Include(f => f.Nutritionist)
                .Where(f => f.Status == "Active" || f.Status == null);

            var allActiveFoods = activeFoodsQuery.ToList();

            bool IsBreakfast(Food f) => f.Name.Contains("Smoothie", StringComparison.OrdinalIgnoreCase) 
                                        || f.Name.Contains("Oats", StringComparison.OrdinalIgnoreCase) 
                                        || f.Name.Contains("Cheela", StringComparison.OrdinalIgnoreCase) 
                                        || f.Name.Contains("Idli", StringComparison.OrdinalIgnoreCase) 
                                        || f.Name.Contains("Upma", StringComparison.OrdinalIgnoreCase) 
                                        || f.Name.Contains("Paratha", StringComparison.OrdinalIgnoreCase)
                                        || f.Name.Contains("Apple", StringComparison.OrdinalIgnoreCase);
            bool IsLunch(Food f) => f.Name.Contains("Thali", StringComparison.OrdinalIgnoreCase) 
                                     || f.Name.Contains("Combo", StringComparison.OrdinalIgnoreCase) 
                                     || f.Name.Contains("Curry", StringComparison.OrdinalIgnoreCase) 
                                     || f.Name.Contains("Tehri", StringComparison.OrdinalIgnoreCase);
            bool IsDinner(Food f) => f.Name.Contains("Salad", StringComparison.OrdinalIgnoreCase) 
                                      || f.Name.Contains("Khichdi", StringComparison.OrdinalIgnoreCase) 
                                      || f.Name.Contains("Bowl", StringComparison.OrdinalIgnoreCase) 
                                      || f.Name.Contains("Dal", StringComparison.OrdinalIgnoreCase);

            var bfList = allActiveFoods.Where(IsBreakfast).ToList();
            var lList = allActiveFoods.Where(IsLunch).ToList();
            var dList = allActiveFoods.Where(IsDinner).ToList();

            Food breakfast = null, lunch = null, dinner = null;

            if (survey.Bmi > 25 || survey.Goal == "Weight Loss")
            {
                breakfast = bfList.Where(f => (f.Calories ?? 0) < 300).FirstOrDefault() ?? bfList.FirstOrDefault();
                lunch = lList.Where(f => (f.Calories ?? 0) < 500).FirstOrDefault() ?? lList.FirstOrDefault();
                dinner = dList.Where(f => (f.Calories ?? 0) < 400).FirstOrDefault() ?? dList.FirstOrDefault();
                ViewBag.RecommendationReason = "Focusing on weight management? We've designed a structured, nutrient-dense daily plan just for you.";
            }
            else if (survey.Goal == "Muscle Gain")
            {
                breakfast = bfList.OrderByDescending(f => f.Protein ?? 0).FirstOrDefault() ?? bfList.FirstOrDefault();
                lunch = lList.OrderByDescending(f => f.Protein ?? 0).FirstOrDefault() ?? lList.FirstOrDefault();
                dinner = dList.OrderByDescending(f => f.Protein ?? 0).FirstOrDefault() ?? dList.FirstOrDefault();
                ViewBag.RecommendationReason = "To support your muscle gain goals, this daily plan maximizes your protein intake.";
            }
            else if (survey.Age > 60)
            {
                breakfast = bfList.FirstOrDefault(f => f.FoodType == "Elderly") ?? bfList.FirstOrDefault();
                lunch = lList.FirstOrDefault(f => f.FoodType == "Elderly") ?? lList.FirstOrDefault();
                dinner = dList.FirstOrDefault(f => f.FoodType == "Elderly") ?? dList.FirstOrDefault();
                ViewBag.RecommendationReason = "Specially curated soft and nutritious meals for healthy aging.";
            }
            else
            {
                breakfast = bfList.FirstOrDefault();
                lunch = lList.FirstOrDefault();
                dinner = dList.FirstOrDefault();
                ViewBag.RecommendationReason = "Hand-picked balanced meals tailored to your health profile.";
            }

            ViewBag.Breakfast = breakfast;
            ViewBag.Lunch = lunch;
            ViewBag.Dinner = dinner;

            return View(survey);
        }
    }
}
