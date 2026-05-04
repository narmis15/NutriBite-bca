using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NUTRIBITE.Models;

public partial class UserSignup
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Name is required")]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid Email Address")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 8)]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,15}$", ErrorMessage = "Password must be between 8 and 15 characters and contain one uppercase letter, one lowercase letter, one digit and one special character.")]
    public string Password { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }
    public string? Phone { get; set; }
    public string? Status { get; set; }
    public int? CalorieGoal { get; set; }
    public string? Role { get; set; } = "User";
    public string? ProfilePictureUrl { get; set; }
    public string? Address { get; set; }

    public virtual ICollection<OrderTable> OrderTables { get; set; } = new List<OrderTable>();
}