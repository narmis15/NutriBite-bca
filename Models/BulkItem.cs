using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NUTRIBITE.Models
{
    [Table("BulkItems")]
    public class BulkItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        public string? Category { get; set; }

        public string? SubCategory { get; set; }

        public string? Description { get; set; }

        public int? VendorId { get; set; }

        public decimal Price { get; set; }

        public string? Weight { get; set; }

        public bool? IsVeg { get; set; }

        public int? MOQ { get; set; }

        public string? ImagePath { get; set; }

        public string? Status { get; set; }

        public DateTime? CreatedAt { get; set; }
    }
}