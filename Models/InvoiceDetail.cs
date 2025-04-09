namespace LapTrinhWindows.Models
{
    public class InvoiceDetail
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int InvoiceDetailID { get; set; }
        [Required]
        public int InvoiceID { get; set; }
        [Required]
        public int ProductID { get; set; }
        [Required]
        public int Quantity { get; set; }
        [Required]
        public double Total { get; set; }
        [ForeignKey("InvoiceID")]
        public virtual  Invoice Invoice { get; set; } = null!;
        [ForeignKey("ProductID")]
        public virtual Product Product { get; set; } = null!;
    }
    
}