namespace LapTrinhWindows.Models
{
    public class Variant
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int VariantID { get; set; }

        [Required]
        public int ProductID { get; set; }

        [ForeignKey("ProductID")]
        public virtual Product Product { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string SKU { get; set; } = string.Empty; // Mã SKU duy nhất cho biến thể (dùng mã vạch)

        [Required]
        public double Price { get; set; } // Giá cụ thể của biến thể

        [Required]
        public int AvailableQuantity { get; set; } // Số lượng tồn kho của biến thể

        public virtual ICollection<VariantAttribute> VariantAttributes { get; set; } = new List<VariantAttribute>(); // Liên kết với thuộc tính
    }
}