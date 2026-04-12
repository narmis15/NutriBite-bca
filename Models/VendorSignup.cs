using System;
using System.Collections.Generic;

namespace NUTRIBITE.Models;

public partial class VendorSignup
{
    public int VendorId { get; set; }

    public string VendorName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public bool IsApproved { get; set; }

    public DateTime? CreatedAt { get; set; }

    public bool IsRejected { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public string? Description { get; set; }

    public string? OpeningHours { get; set; }

    public string? ClosingHours { get; set; }

    public string? LogoPath { get; set; }

    public string? UpiId { get; set; }

    // Phase 3: Geofencing
    public double? MaxDeliveryRadiusKm { get; set; } = 5.0; // Default to 5km
}
