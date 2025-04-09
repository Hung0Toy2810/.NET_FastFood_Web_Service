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
        public double Price { get; set; }
        [Required]
        public int AvailableQuantity { get; set; }
        [Required]
        public double Discount { get; set; }
        [MaxLength(100)]
        public string ImageKey { get; set; } = "default.jpg";

        public virtual ICollection<InvoiceDetail> InvoiceDetails { get; set; } = new List<InvoiceDetail>();
        public virtual ICollection<GiftPromotion> GiftPromotions { get; set; } = new List<GiftPromotion>();

    }
}