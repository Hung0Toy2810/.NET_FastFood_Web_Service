namespace LapTrinhWindows.Models
{
    public class Product
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ProductID { get; set; }

        [Required]
        [MaxLength(100)]
        public string ProductName { get; set; } = string.Empty;

        [Required]
        public int CategoryID { get; set; }

        [ForeignKey("CategoryID")]
        public virtual Category Category { get; set; } = null!;

        [Required]
        public double Discount { get; set; }

        [MaxLength(100)]
        public string ImageKey { get; set; } = "default.jpg";
        [MaxLength(500)]
        public string? ImageUrl { get; set; }

        public virtual ICollection<InvoiceDetail> InvoiceDetails { get; set; } = new List<InvoiceDetail>();
        public virtual ICollection<Variant> Variants { get; set; } = new List<Variant>();
        public virtual ICollection<ProductTag> ProductTags { get; set; } = new List<ProductTag>();
        public virtual ICollection<PointRedemption> PointRedemptions { get; set; } = new List<PointRedemption>(); 
        public virtual ICollection<ProductImage> AdditionalImages { get; set; } = new List<ProductImage>();
    }
}