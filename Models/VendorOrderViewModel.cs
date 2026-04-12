namespace NUTRIBITE.Models
{
    public class VendorOrderViewModel
    {
        public int OrderId { get; set; }

        public string CustomerName { get; set; }

        public string? CustomerPhone { get; set; }

        public string? DeliveryAddress { get; set; }

        public string FoodItem { get; set; }

        public string? OrderType { get; set; }

        public int? Quantity { get; set; }

        public string? SpecialInstruction { get; set; }

        public decimal? Amount { get; set; }

        public string Status { get; set; }

        public DateTime? Date { get; set; }
    }
}