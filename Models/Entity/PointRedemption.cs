namespace LapTrinhWindows.Models
{
    public class PointRedemption
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PointRedemptionID { get; set; }

        [Required]
        public string SKU { get; set; } = string.Empty;

        [ForeignKey("SKU")]
        public virtual Variant Variant { get; set; } = null!;
        [Required]
        public int BatchID { get; set; }
        [ForeignKey("BatchID")]
        public virtual Batch Batch { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string RedemptionName { get; set; } = string.Empty;

        [Required]
        public int PointsRequired { get; set; }

        [Required]
        public int AvailableQuantity { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }
        [Required]
        public PointRedemptionStatus Status { get; set; }

        public virtual ICollection<InvoiceDetail> InvoiceDetails { get; set; } = new List<InvoiceDetail>(); 
    }
    public enum PointRedemptionStatus
    {
        Active,
        Inactive,
    }
}