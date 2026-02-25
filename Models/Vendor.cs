using System;

namespace NUTRIBITE.Models
{
    public class Vendor
    {
        public int VendorId { get; set; }
        public string VendorName { get; set; }
        public string Email { get; set; }
        public bool IsApproved { get; set; }
        public bool IsRejected { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}