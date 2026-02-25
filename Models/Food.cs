namespace NUTRIBITE.Models
{
    public class Food
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int CategoryId { get; set; }
        public int? Calories { get; set; }
        public string? PreparationTime { get; set; }
        public string? ImagePath { get; set; }
        public int VendorId { get; set; }
    }
}