namespace LapTrinhWindows.Models
{
    public class Customer
    {
        [Key]
        public Guid CustomerID { get; set; } 

        [Required]
        [MaxLength(100)]
        public string CustomerName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Address { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string HashPassword { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string AvtKey { get; set; } = "https://www.gravatar.com/avatar/205e460b479e2e5b48aec07710c08d50";

        public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
        public ICollection<GiftPromotion> GiftPromotions { get; set; } = new List<GiftPromotion>();
    }
}