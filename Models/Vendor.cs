using System;

namespace NUTRIBITE.Models
{
    public class Vendor
    {
        public int VendorId { get; set; }
        public required string VendorName { get; set; }
        public required string Email { get; set; }
        public bool IsApproved { get; set; }
        public bool IsRejected { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}