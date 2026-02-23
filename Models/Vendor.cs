using Microsoft.AspNetCore.Identity;

namespace NUTRIBITE.Models
{
    public class Vendor : IdentityUser
    {
        public string BusinessName { get; set; }
        public string Address { get; set; }
        public string PhoneNumber { get; set; }
        // Add more fields as needed, e.g., cuisine type
    }
}