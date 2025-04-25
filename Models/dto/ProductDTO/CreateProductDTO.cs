using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace LapTrinhWindows.Models.DTO
{
    public class CreateProductDTO
    {
        [Required, MaxLength(100)]
        public string ProductName { get; set; } = string.Empty;

        [Required]
        public int CategoryID { get; set; }

        [Required, Range(0, 1)]
        public double Discount { get; set; }

        [MaxLength(100)]
        public string ImageKey { get; set; } = "default.jpg";

        public IFormFile? ImageFile { get; set; }

        [Required]
        public List<CreateAttributeDTO> Attributes { get; set; } = new List<CreateAttributeDTO>(); 

        [Required]
        public List<CreateVariantDTO> Variants { get; set; } = new List<CreateVariantDTO>(); 
    }

    public class CreateAttributeDTO
    {
        [Required, MaxLength(50)]
        public string AttributeName { get; set; } = string.Empty; 

        [Required]
        public List<string> Values { get; set; } = new List<string>(); 
    }

    public class CreateVariantDTO
    {
        [Required, MaxLength(50)]
        public string SKU { get; set; } = string.Empty;
        [Required, Range(0.01, double.MaxValue)]
        public decimal Price { get; set; } 

        [Required, Range(0, int.MaxValue)]
        public int Stock { get; set; }

        [Required]
        public Dictionary<string, string> AttributeValues { get; set; } = new Dictionary<string, string>(); 
    }
    public class UpdateProductDTO
    {
        public string ProductName { get; set; } = string.Empty;
        public int CategoryID { get; set; }
        public double Discount { get; set; }
        public string ImageKey { get; set; } = "default.jpg";
        public List<AttributeDTO> Attributes { get; set; } = new();
        public List<VariantDTO> Variants { get; set; } = new();
    }
}