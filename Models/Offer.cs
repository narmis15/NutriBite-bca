using System;
using System.Collections.Generic;

namespace NUTRIBITE.Models;

public partial class Offer
{
    public int Offerid { get; set; }

    public int PId { get; set; }

    public int VId { get; set; }

    public int Dprice { get; set; }

    public string? Status { get; set; }
}
