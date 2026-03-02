using System;
using System.Collections.Generic;

namespace NUTRIBITE.Models;

public partial class Location
{
    public int Id { get; set; }

    public string State { get; set; } = null!;

    public string City { get; set; } = null!;

    public string Region { get; set; } = null!;
}
