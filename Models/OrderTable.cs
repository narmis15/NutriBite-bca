using System;
using System.Collections.Generic;

namespace NUTRIBITE.Models;

public partial class OrderTable
{
    public int OrderId { get; set; }

    public int? UserId { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    public int? TotalItems { get; set; }

    public string? PickupSlot { get; set; }

    public int? TotalCalories { get; set; }

    public string? PaymentStatus { get; set; }

    public string? Status { get; set; }

    public string? OrderType { get; set; } // Pickup or Delivery

    public string? DeliveryAddress { get; set; }

    public int? DeliveryPersonId { get; set; }

    public string? DeliveryStatus { get; set; }

    public string? DeliveryNotes { get; set; }

    public bool? IsFlagged { get; set; }

    public bool? IsResolved { get; set; }

    public string? FlagReason { get; set; }

    public string? AdminNotes { get; set; }

    public string? CancelReason { get; set; }

    public string? CancelledBy { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public decimal TotalAmount { get; set; } = 0.00m;

    public decimal CommissionAmount { get; set; } = 0.00m;

    public decimal VendorAmount { get; set; } = 0.00m;

    public decimal DeliveryCharge { get; set; } = 0.00m;

    public decimal GST { get; set; } = 0.00m;

    public int? VendorId { get; set; }

    public int? AdminId { get; set; }

    public int Version { get; set; } = 1;

    public int TrackingProgress { get; set; } = 0;

    public int? DeliveryOTP { get; set; }

    public bool? IsDelivered { get; set; } = false;

    public string? OrderStatus { get; set; } = "Placed";

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual UserSignup? User { get; set; }
}
