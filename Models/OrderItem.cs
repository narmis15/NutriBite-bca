using System.ComponentModel.DataAnnotations.Schema;

namespace NUTRIBITE.Models
{
    [Table("OrderItems")]
    public class OrderItem
    {
        public int Id { get; set; }
        public int? OrderId { get; set; }
        public string? ItemName { get; set; }
        public int? Quantity { get; set; }
        public string? Instructions { get; set; }
    }
}