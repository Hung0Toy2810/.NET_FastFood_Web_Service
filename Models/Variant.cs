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
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "Stock must be non-negative.")]
        public int Stock { get; set; }

        public virtual ICollection<VariantAttribute> VariantAttributes { get; set; } = new List<VariantAttribute>(); // Liên kết với thuộc tính
    }
}