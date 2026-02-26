using System;
using NUTRIBITE.ViewModels;

namespace NUTRIBITE.Services
{
    public class HealthCalculationService : IHealthCalculationService
    {
        public HealthCalculationResult Calculate(HealthSurveyViewModel input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            // BMI
            var heightM = (double)input.HeightCm / 100.0;
            var bmi = (decimal)((double)input.WeightKg / (heightM * heightM));
            bmi = Math.Round(bmi, 1);

            // BMR - Mifflin-St Jeor
            decimal s = 0m;
            var gender = (input.Gender ?? "").Trim().ToLowerInvariant();
            if (gender.StartsWith("m")) s = 5m;
            else if (gender.StartsWith("f")) s = -161m;
            else s = -78m;

            var bmr = 10m * input.WeightKg + 6.25m * input.HeightCm - 5m * input.Age + s;
            bmr = Math.Round(bmr, 1);

            // Activity multiplier
            var multiplier = GetActivityMultiplier(input.ActivityLevel);

            var maintenance = bmr * multiplier;

            // Goal-based adjustment
            var goal = (input.Goal ?? "").Trim().ToLowerInvariant();
            decimal adjusted = maintenance;
            if (goal.Contains("loss") || goal.Contains("lose") || goal.Contains("weight loss"))
            {
                adjusted = maintenance - 450m; // weight loss
            }
            else if (goal.Contains("muscle"))
            {
                adjusted = maintenance + 300m; // muscle gain
            }
            else if (goal.Contains("gain") || goal.Contains("weight gain"))
            {
                adjusted = maintenance + 350m; // general gain
            }
            else
            {
                adjusted = maintenance; // maintain
            }

            if (adjusted < 1000m) adjusted = 1000m;

            // Protein calculation
            decimal proteinPerKg = 1.1m;
            if (goal.Contains("muscle"))
                proteinPerKg = 1.8m;

            var proteinGrams = (int)Math.Round((double)(proteinPerKg * input.WeightKg));

            // Macro distribution (kcal): Carbs 50%, Protein 20%, Fats 30%
            var totalKcal = (int)Math.Round((double)adjusted);
            var carbsKcal = (int)Math.Round(totalKcal * 0.50);
            var proteinKcal = (int)Math.Round(totalKcal * 0.20);
            var fatsKcal = totalKcal - carbsKcal - proteinKcal;

            // Convert kcal -> grams (carbs & protein 4 kcal/g, fats 9 kcal/g)
            var carbsG = (int)Math.Round(carbsKcal / 4.0);
            var proteinG = (int)Math.Round(proteinKcal / 4.0);
            var fatsG = (int)Math.Round(fatsKcal / 9.0);

            return new HealthCalculationResult
            {
                BMI = bmi,
                BMR = bmr,
                ActivityMultiplier = Math.Round(multiplier, 2),
                RecommendedCalories = totalKcal,
                RecommendedProtein = proteinGrams,
                CarbsKcal = carbsKcal,
                ProteinKcal = proteinKcal,
                FatsKcal = fatsKcal,
                CarbsGr = carbsG,
                ProteinGr = proteinG,
                FatsGr = fatsG
            };
        }

        public string[] SuggestFoods(string dietaryPreference)
        {
            var pref = (dietaryPreference ?? "").Trim().ToLowerInvariant();
            if (pref.Contains("vegan"))
                return new[] { "Chickpeas", "Quinoa", "Lentils" };
            if (pref.Contains("veget"))
                return new[] { "Paneer", "Dal", "Tofu" };
            if (pref.Contains("egge") || pref.Contains("egg"))
                return new[] { "Eggs", "Oats", "Brown Rice" };
            // default non-veg
            return new[] { "Chicken Breast", "Fish", "Eggs" };
        }

        private decimal GetActivityMultiplier(string activityLevel)
        {
            var a = (activityLevel ?? "").Trim().ToLowerInvariant();
            return a switch
            {
                var x when x.Contains("sedentary") => 1.2m,
                var x when x.Contains("light") => 1.375m,
                var x when x.Contains("moderate") => 1.55m,
                var x when x.Contains("very") => 1.725m,
                _ => 1.2m
            };
        }
    }
}