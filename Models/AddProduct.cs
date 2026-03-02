using System;
using System.Collections.Generic;

namespace NUTRIBITE.Models;

public partial class AddProduct
{
    public int Pid { get; set; }

    public string Pname { get; set; } = null!;

    public int Pprice { get; set; }

    public int CatId { get; set; }

    public int Vid { get; set; }

    public string Pic { get; set; } = null!;

    public int Rating { get; set; }

    public string Description { get; set; } = null!;

    public string? Status { get; set; }

    public int? Dper { get; set; }
}
