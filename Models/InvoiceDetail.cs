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
        public  Invoice Invoice { get; set; } = null!;
        [ForeignKey("ProductID")]
        public  Product Product { get; set; } = null!;
    }
    
}