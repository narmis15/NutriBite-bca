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
}
