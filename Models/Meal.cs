using System;

namespace NUTRIBITE.Models
{
    public partial class Meal
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public int FoodId { get; set; }

        public string Slot { get; set; } = "";

        public DateTime MealDate { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
