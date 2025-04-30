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
        public int ProductID { get; set; }

        [ForeignKey("ProductID")]
        public virtual Product Product { get; set; } = null!;

        [Required]
        public int Quantity { get; set; }

        [Required]
        public double Total { get; set; } 

        public bool IsPointRedemption { get; set; } = false; 

        public int? PointRedemptionID { get; set; } 

        [ForeignKey("PointRedemptionID")]
        public virtual PointRedemption? PointRedemption { get; set; }
        
    }
}