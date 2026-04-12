using System.ComponentModel.DataAnnotations;

namespace NUTRIBITE.ViewModels
{
    public class UserProfileEditViewModel
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = "";

        [Required]
        [EmailAddress]
        [StringLength(200)]
        public string Email { get; set; } = "";

        [StringLength(20)]
        public string? Phone { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}