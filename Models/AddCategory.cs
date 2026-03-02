using System;
using System.Collections.Generic;

namespace NUTRIBITE.Models;

public partial class AddCategory
{
    public int Cid { get; set; }

    public string ProductCategory { get; set; } = null!;

    public string ProductPic { get; set; } = null!;

    public string? MealCategory { get; set; }

    public string? ImagePath { get; set; }

    public string? MealPic { get; set; }

    public DateTime? CreatedAt { get; set; }
}
