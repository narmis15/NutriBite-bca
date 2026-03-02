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
}
