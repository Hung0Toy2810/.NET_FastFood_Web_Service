using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace LapTrinhWindows.Models.DTO
{
    public class CreateProductDTO
    {
        [Required]
        [MaxLength(100)]
        public string ProductName { get; set; } = string.Empty;

        [Required]
        public int CategoryID { get; set; }

        [Required]
        [Range(0, 1, ErrorMessage = "Discount must be between 0 and 1.")]
        public double Discount { get; set; }

        [MaxLength(100)]
        public string ImageKey { get; set; } = "default.jpg"; // Ảnh chính mặc định

        public IFormFile? ImageFile { get; set; } // File ảnh chính để upload
    }
}