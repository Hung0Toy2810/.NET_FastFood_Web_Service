namespace LapTrinhWindows.Models
{
    public class Invoice
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int InvoiceID { get; set; }

        public Guid? CashierStaff { get; set; }

        [ForeignKey("CashierStaff")]
        public virtual Employee? Employee { get; set; }

        public Guid? CustomerID { get; set; } 

        [ForeignKey("CustomerID")]
        public virtual Customer? Customer { get; set; }

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

        
        [MaxLength(500)]
        public string DeliveryAddress { get; set; } = string.Empty;

        [Required]
        public DeliveryStatus DeliveryStatus { get; set; }

        [Required]
        public OrderType OrderType { get; set; }
        [Required]
        public bool IsAnonymous { get; set; }

        [MaxLength(1000)]
        public string Feedback { get; set; } = string.Empty;

        [Range(1, 5)]
        public int Star { get; set; }

        public virtual ICollection<InvoiceDetail> InvoiceDetails { get; set; } = new List<InvoiceDetail>();
        
        public virtual ICollection<InvoiceStatusHistory> InvoiceStatusHistories { get; set; } = new List<InvoiceStatusHistory>();
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
        NotDelivered,
        Pending,
        InTransit,
        Delivered
    }

    public enum OrderType
    {
        Online,
        Offline
    }
}