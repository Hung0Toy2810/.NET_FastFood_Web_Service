namespace LapTrinhWindows.Models
{
    public class GiftPromotion
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int GiftPromotionID { get; set; }
        [Required]
        public Guid CustomerID { get; set; }
        [ForeignKey("CustomerID")]
        public virtual Customer Customer { get; set; } = null!;
        [Required]
        [MaxLength(100)]
        public string GiftPromotionName { get; set; } = string.Empty;
        [Required]
        public int ProductID { get; set; }
        [Required]
        public int Quantity { get; set; }
        [Required]
        public GiftPromotionStatus Status { get; set; }
        [ForeignKey("ProductID")]
        public virtual Product Product { get; set; } = null!;
        //start day
        [Required]
        public DateTime StartDate { get; set; }
        //end day
        [Required]
        public DateTime EndDate { get; set; }
    }
    public enum GiftPromotionStatus
    {
        Active,
        Inactive
    }
}