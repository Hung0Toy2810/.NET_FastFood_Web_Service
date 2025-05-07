namespace LapTrinhWindows.Models
{
    public class InvoiceStatusHistory
    {
        [Key]
        public int HistoryID { get; set; }
        public int InvoiceID { get; set; }
        [ForeignKey("InvoiceID")]
        public virtual Invoice Invoice { get; set; } = null!;
        public InvoiceStatus OldStatus { get; set; }
        public InvoiceStatus NewStatus { get; set; }
        public DateTime ChangedAt { get; set; }
        public Guid? ChangedBy { get; set; } 
    }

}