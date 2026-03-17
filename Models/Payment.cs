using System.ComponentModel.DataAnnotations.Schema;

using System;
using System.Collections.Generic;

namespace NUTRIBITE.Models;
[Table("Payment")]

public partial class Payment
{
    public int Id { get; set; }

    public int? OrderId { get; set; }

    public string? PaymentMode { get; set; }

    public decimal? Amount { get; set; }

    public bool? IsRefunded { get; set; }

    public string? RefundMethod { get; set; }

    public string? RefundStatus { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual OrderTable? Order { get; set; }
}
