namespace LapTrinhWindows.Models
{
    public class Invoice
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int InvoiceID { get; set; }
        [Required]
        public Guid EmployeeID { get; set; }
        [Required]
        public Guid CustomerID { get; set; }
        [Required]
        public DateTime CreateAt { get; set; }
        [Required]
        public double Discount { get; set; }
        [Required]
        public PaymentMethods PaymentMethod { get; set; }
        [Required]
        public InvoiceStatus Status { get; set; }
        [Required]
        public double Total { get; set; }
        [ForeignKey("EmployeeID")]
        public virtual Employee Employee { get; set; } = null!;
        [ForeignKey("CustomerID")]
        public virtual Customer Customer { get; set; } = null!;
        //delivery address
        [Required]
        [MaxLength(100)]
        public string DeliveryAddress { get; set; } = string.Empty;
        //delivery status
        [Required]
        public DeliveryStatus DeliveryStatus { get; set; }
        // feedback
        [MaxLength(1000)]
        public string Feedback { get; set; } = string.Empty;
        // star of invoice
        // 1->5
        [Range(1, 5)]
        public int Star { get; set; }
        public virtual ICollection<InvoiceDetail> InvoiceDetails { get; set; } = new List<InvoiceDetail>();
    }
    public enum PaymentMethods
    {
        Cash,
        CreditCard,
        DebitCard
    }
    public enum InvoiceStatus
    {
        Pending,
        Paid,
        Cancelled
    }
    public enum DeliveryStatus
    {
        Pending,
        Delivered
    }
}
