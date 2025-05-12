namespace LapTrinhWindows.Models
{
    public class Batch
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BatchID { get; set; }

        [Required]
        public string SKU { get; set; } = string.Empty;

        [ForeignKey("SKU")]
        public virtual Variant Variant { get; set; } = null!;

        public DateTime? ExpirationDate { get; set; }
        public DateTime? ProductionDate { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int AvailableQuantity { get; set; }
        
        public virtual ICollection<InvoiceDetail> InvoiceDetails { get; set; } = new List<InvoiceDetail>(); 
        
        public virtual ICollection<PointRedemption> PointRedemptions { get; set; } = new List<PointRedemption>();
    }
}