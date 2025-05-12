

namespace LapTrinhWindows.Models
{
    public class Employee
    {
        [Key]
        public Guid EmployeeID { get; set; }

        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Address { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string HashPassword { get; set; } = string.Empty;
        [Required]
        public int RoleID { get; set; }

        [ForeignKey("RoleID")]
        public virtual EmployeeRole EmployeeRole { get; set; } = null!;
        // AvtUrl
        [Required]
        [MaxLength(100)]
        public string AvtKey { get; set; } = "https://www.gravatar.com/avatar/205e460b479e2e5b48aec07710c08d50";
        // status online/offline
        [Required]
        public EmployeeStatus Status { get; set; }
        // account status
        [Required]
        public bool AccountStatus { get; set; } = true;

        public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
        //InvoiceStatusHistories
        public virtual ICollection<InvoiceStatusHistory> InvoiceStatusHistories { get; set; } = new List<InvoiceStatusHistory>();
        
    }
    public enum EmployeeStatus
    {
        Online,
        Offline
    }
}