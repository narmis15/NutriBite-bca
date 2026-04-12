using System;
using System.Collections.Generic;

namespace NUTRIBITE.Models;

public partial class UserSignup
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

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
