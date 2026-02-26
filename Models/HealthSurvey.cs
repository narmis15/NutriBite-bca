using System;

namespace NUTRIBITE.Models
{
    public class HealthSurvey
    {
        public int Id { get; set; }

        // Using numeric UserId (matches your session-based UserSignup.Id)
        public int UserId { get; set; }

        // Inputs
        public int Age { get; set; }
        public string Gender { get; set; } = "";
        public decimal HeightCm { get; set; }
        public decimal WeightKg { get; set; }
        public string ActivityLevel { get; set; } = "";
        public string Goal { get; set; } = "";
        public string ChronicDiseases { get; set; } = "";
        public string FoodAllergies { get; set; } = "";
        public string DietaryPreference { get; set; } = "";
        public bool Smoking { get; set; }
        public bool Alcohol { get; set; }

        // Calculated
        public decimal BMI { get; set; }
        public decimal BMR { get; set; }
        public int RecommendedCalories { get; set; }
        public int RecommendedProtein { get; set; } // grams

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}