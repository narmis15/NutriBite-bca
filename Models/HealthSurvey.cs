using System;
using System.Collections.Generic;

namespace NUTRIBITE.Models;

public partial class HealthSurvey
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int Age { get; set; }

    public string? Gender { get; set; }

    public decimal? HeightCm { get; set; }

    public decimal? WeightKg { get; set; }

    public string? ActivityLevel { get; set; }

    public string? Goal { get; set; }

    public decimal? Bmi { get; set; }

    public decimal? Bmr { get; set; }

    public int? RecommendedCalories { get; set; }

    public int? RecommendedProtein { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string? ChronicDiseases { get; set; }

    public string? FoodAllergies { get; set; }

    public string? DietaryPreference { get; set; }

    public bool Smoking { get; set; }

    public bool Alcohol { get; set; }
}
