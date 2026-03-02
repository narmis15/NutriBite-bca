using System;
using System.Collections.Generic;

namespace NUTRIBITE.Models;

public partial class Coupon
{
    public int Id { get; set; }

    public string Code { get; set; } = null!;

    public int Discount { get; set; }

    public DateTime Startdate { get; set; }

    public DateTime Validtill { get; set; }
}
