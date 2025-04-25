using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LapTrinhWindows.Models
{
    public class ProductImage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ProductImageID { get; set; }

        [Required]
        public int ProductID { get; set; }

        [ForeignKey("ProductID")]
        public virtual Product Product { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string ImageKey { get; set; } = string.Empty; // Đường dẫn hoặc key của ảnh phụ

        [Required]
        public int OrdinalNumbers { get; set; } = 0; 

    }
}