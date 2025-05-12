namespace LapTrinhWindows.Models.DTO
{
    public class ProductResponseDTO
    {
        // ID của sản phẩm (ProductID từ Entity)
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public double Discount { get; set; }
        
        
    }
}