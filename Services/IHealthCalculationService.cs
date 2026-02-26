using NUTRIBITE.ViewModels;

namespace NUTRIBITE.Services
{
    public interface IHealthCalculationService
    {
        HealthCalculationResult Calculate(HealthSurveyViewModel input);
        string[] SuggestFoods(string dietaryPreference);
    }

    public class HealthCalculationResult
    {
        public decimal BMI { get; set; }
        public decimal BMR { get; set; }
        public decimal ActivityMultiplier { get; set; }
        public int RecommendedCalories { get; set; }
        public int RecommendedProtein { get; set; } // grams

        // Macro distribution in kcal and grams
        public int CarbsKcal { get; set; }
        public int ProteinKcal { get; set; }
        public int FatsKcal { get; set; }

        public int CarbsGr { get; set; }
        public int ProteinGr { get; set; }
        public int FatsGr { get; set; }
    }
}