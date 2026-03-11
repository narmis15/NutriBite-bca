using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NUTRIBITE.Models;

public partial class Carttable
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Crid { get; set; }

    public int Pid { get; set; }

    public int Uid { get; set; }

    public int Qty { get; set; }

    public int? Amount { get; set; }

    public string? SpecialInstruction { get; set; }
    public string? Status { get; set; }

    public DateTime? Date { get; set; }

    public int? Servicecharge { get; set; }

    public int? Total { get; set; }
}