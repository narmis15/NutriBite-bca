using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace NUTRIBITE.Models
{
    [Table("OrderTable")]
    public class Order
    {
        public int OrderId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public int? TotalItems { get; set; }
        public string? PickupSlot { get; set; }
        public int? TotalCalories { get; set; }
        public string? PaymentStatus { get; set; }
        public string? Status { get; set; }
        public bool? IsFlagged { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? CancelReason { get; set; }
        public string? CancelledBy { get; set; }
        public string? AdminNotes { get; set; }

        [NotMapped]
        public List<OrderItem>? Items { get; set; }
    }
}