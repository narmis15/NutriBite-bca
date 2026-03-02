using System;
using System.Collections.Generic;

namespace NUTRIBITE.Models;

public partial class DailyCalorieEntry
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public DateTime Date { get; set; }

    public string FoodName { get; set; } = null!;

    public int Calories { get; set; }

    public decimal? Protein { get; set; }

    public decimal? Carbs { get; set; }

    public decimal? Fats { get; set; }
}
