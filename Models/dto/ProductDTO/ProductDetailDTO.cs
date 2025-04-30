namespace LapTrinhWindows.Models.DTO
{
    public class ProductDetailDTO
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int CategoryID { get; set; }
        public double Discount { get; set; }
        public string? ImageUrl { get; set; }
        public List<string> AdditionalImageUrls { get; set; } = new(); // Thêm danh sách URL ảnh phụ
        public List<AttributeDTO> Attributes { get; set; } = new();
        public List<VariantDTO> Variants { get; set; } = new();
    }

    public class AttributeDTO
    {
        public string AttributeName { get; set; } = string.Empty;
        public List<string> Values { get; set; } = new();
    }

    public class VariantDTO
    {
        public int VariantID { get; set; }
        public string SKU { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public Dictionary<string, string> AttributeValues { get; set; } = new();
    }
}