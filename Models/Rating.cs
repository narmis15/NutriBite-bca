using System;
using System.Collections.Generic;

namespace NUTRIBITE.Models;

public partial class Rating
{
    public int Rid { get; set; }

    public int Uid { get; set; }

    public int Vid { get; set; }

    public int? FoodId { get; set; }

    public string Message { get; set; } = null!;

    public int? Ratings { get; set; }

    public DateTime? Date { get; set; }

    public virtual UserSignup? User { get; set; }
    public virtual VendorSignup? Vendor { get; set; }
    public virtual Food? Food { get; set; }
}
