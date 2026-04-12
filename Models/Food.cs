using System;
using System.Collections.Generic;

namespace NUTRIBITE.Models;

public partial class Food
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public int? CategoryId { get; set; }

    public int? VendorId { get; set; }

    public string? ImagePath { get; set; }

    public int? Calories { get; set; }

    public string? PreparationTime { get; set; }

    public double? Rating { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }
    public string? FoodType { get; set; }

    public int? ProductCategoryId { get; set; }
    public int? MealCategoryId { get; set; }

    // Nutritionist Verification Fields
    public int? NutritionistId { get; set; }
    public bool IsVerified { get; set; } = false;

    // Additional Nutrition Details
    public double? Protein { get; set; } // in grams
    public double? Carbs { get; set; }   // in grams
    public double? Fat { get; set; }     // in grams

    // Phase 1: Daily Stock Limits
    public int? DailyLimit { get; set; }

    // Phase 2: Dietary Badges
    public bool IsGlutenFree { get; set; } = false;
    public bool IsVegan { get; set; } = false;
    public bool IsNutFree { get; set; } = false;

    public virtual Nutritionist? Nutritionist { get; set; }

    public virtual Recipe? Recipe { get; set; }
}
