using System;
using System.Collections.Generic;

namespace NUTRIBITE.Models;

public partial class Carttable
{
    public int Crid { get; set; }

    public int Pid { get; set; }

    public int Uid { get; set; }

    public int Qty { get; set; }

    public int? Amount { get; set; }

    public string? Status { get; set; }

    public DateTime? Date { get; set; }

    public int? Servicecharge { get; set; }

    public int? Total { get; set; }
}
