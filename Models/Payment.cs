using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace NUTRIBITE.Models
{
    [Table("Payment")]
    public class Payment
    {
        public int Id { get; set; }
        public int? OrderId { get; set; }
        public string? PaymentMode { get; set; }
        public decimal? Amount { get; set; }
        public bool? IsRefunded { get; set; }
        public string? RefundStatus { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}