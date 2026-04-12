namespace NUTRIBITE.Models
{
    public class CartItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string ImageUrl { get; set; }
        public bool IsBulk { get; set; }
        public string SpecialInstructions { get; set; }
    }
}
