using System;

namespace NUTRIBITE.Models
{
    public class DailyCalorieEntry
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime Date { get; set; }
        public required string FoodName { get; set; }
        public int Calories { get; set; }
        public required string MealType { get; set; } = "Other";
        public decimal? Protein { get; set; }
        public decimal? Carbs { get; set; }
        public decimal? Fats { get; set; }
        public int? OrderId { get; set; }
    }
}