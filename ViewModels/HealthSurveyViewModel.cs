using System.ComponentModel.DataAnnotations;

namespace NUTRIBITE.ViewModels
{
    public class HealthSurveyViewModel
    {
        [Required]
        [Range(10, 120)]
        public int Age { get; set; }

        [Required]
        public string Gender { get; set; } = "Male";

        [Required]
        [Range(50, 250, ErrorMessage = "Height must be in cm (50-250).")]
        public decimal HeightCm { get; set; }

        [Required]
        [Range(20, 500, ErrorMessage = "Weight must be in kg (20-500).")]
        public decimal WeightKg { get; set; }

        [Required]
        public string ActivityLevel { get; set; } = "Sedentary";

        [Required]
        public string Goal { get; set; } = "Maintain";

        [StringLength(1000)]
        public string ChronicDiseases { get; set; } = "";

        [StringLength(1000)]
        public string FoodAllergies { get; set; } = "";

        [Required]
        public string DietaryPreference { get; set; } = "Vegetarian";

        public bool Smoking { get; set; }
        public bool Alcohol { get; set; }
    }
}