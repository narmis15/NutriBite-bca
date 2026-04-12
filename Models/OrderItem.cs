using System;
using System.Collections.Generic;

namespace NUTRIBITE.Models;

public partial class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public int? FoodId { get; set; }

    public int? BulkItemId { get; set; }

    public string? ItemName { get; set; }

    public int? Quantity { get; set; }

    public string? Instructions { get; set; }

    public DateTime? CreatedAt { get; set; }

    public decimal PricePerItem { get; set; } = 0.00m;

    public string? SpecialInstruction { get; set; }
    public virtual OrderTable Order { get; set; } = null!;
    public virtual Food? Food { get; set; }
    public virtual BulkItem? BulkItemData { get; set; }
}
