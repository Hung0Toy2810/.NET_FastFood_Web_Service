using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LapTrinhWindows.Models
{
    public class InvoiceDetail
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int InvoiceDetailID { get; set; }

        [Required]
        public int InvoiceID { get; set; }

        [ForeignKey("InvoiceID")]
        public virtual Invoice Invoice { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string SKU { get; set; } = string.Empty;

        [ForeignKey("SKU")]
        public virtual Variant Variant { get; set; } = null!;

        [Required]
        public int BatchID { get; set; }

        [ForeignKey("BatchID")]
        public virtual Batch Batch { get; set; } = null!;

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        [Required]
        public double Total { get; set; }

        public bool IsPointRedemption { get; set; } = false;

        public int? PointRedemptionID { get; set; }

        [ForeignKey("PointRedemptionID")]
        public virtual PointRedemption? PointRedemption { get; set; }
    }
}