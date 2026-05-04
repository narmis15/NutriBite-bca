using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NUTRIBITE.Models;

public partial class VendorSignup
{
    public int VendorId { get; set; }

    [Required(ErrorMessage = "Vendor name is required")]
    public string VendorName { get; set; } = null!;

    public string? OwnerName { get; set; }

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid Email Address")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 8)]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,15}$", ErrorMessage = "Password must be between 8 and 15 characters and contain one uppercase letter, one lowercase letter, one digit and one special character.")]
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