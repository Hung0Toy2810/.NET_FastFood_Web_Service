using LapTrinhWindows.Models;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace LapTrinhWindows.DTO
{
    public class CreateOnlineInvoiceDTO
    {
        [Required]
        public PaymentMethods PaymentMethod { get; set; }
        [Required]
        [MaxLength(500)]
        public string DeliveryAddress { get; set; } = string.Empty;
        [Required]
        public List<CreateInvoiceDetailDTO> Details { get; set; } = new List<CreateInvoiceDetailDTO>();
    }

    public class CreateOfflineInvoiceDTO
    {
        [Required]
        public PaymentMethods PaymentMethod { get; set; }
        [Required]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;
        [Required]
        public List<CreateInvoiceDetailDTO> Details { get; set; } = new List<CreateInvoiceDetailDTO>();
    }

    public class CreateInvoiceDetailDTO
    {
        [Required]
        [MaxLength(50)]
        public string SKU { get; set; } = string.Empty;
        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; } = 1;
        public bool IsPointRedemption { get; set; }
        public int? PointRedemptionID { get; set; }
        [Required]
        public int ProductId { get; set; }
        [Required]
        public int BatchID { get; set; } // Required for all items
    }

    public class InvoiceResponseDTO
    {
        public int InvoiceID { get; set; }
        public Guid? CashierStaff { get; set; }
        public Guid? CustomerID { get; set; }
        public DateTime CreateAt { get; set; }
        public double Discount { get; set; }
        public PaymentMethods PaymentMethod { get; set; }
        public InvoiceStatus Status { get; set; }
        public double Total { get; set; }
        public string DeliveryAddress { get; set; } = string.Empty;
        public DeliveryStatus DeliveryStatus { get; set; }
        public OrderType OrderType { get; set; }
        public string Feedback { get; set; } = string.Empty;
        public int Star { get; set; }
        public bool IsAnonymous { get; set; }
        public List<InvoiceDetailDTO> InvoiceDetails { get; set; } = new List<InvoiceDetailDTO>();
    }

    public class InvoiceDetailDTO
    {
        public int InvoiceDetailID { get; set; }
        public string SKU { get; set; } = string.Empty;
        public int? BatchID { get; set; } // Nullable to match InvoiceDetail model
        public int Quantity { get; set; }
        public double Total { get; set; }
        public bool IsPointRedemption { get; set; }
        public int? PointRedemptionID { get; set; }
    }

    public class UpdateInvoiceDTO
    {
        [MaxLength(500)]
        public string? DeliveryAddress { get; set; }
        public InvoiceStatus? Status { get; set; }
        public double? Discount { get; set; }
        public DeliveryStatus? DeliveryStatus { get; set; }
        [MaxLength(1000)]
        public string? Feedback { get; set; }
        [Range(1, 5)]
        public int? Star { get; set; }
    }

    public class InvoiceFilterDTO
    {
        public InvoiceStatus? Status { get; set; }
        public OrderType? OrderType { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class FeedbackDTO
    {
        [Required]
        [MaxLength(1000)]
        public string Feedback { get; set; } = string.Empty;
        [Required]
        [Range(1, 5)]
        public int Star { get; set; }
    }

    public class StatisticsDTO
    {
        public int TotalInvoices { get; set; }
        public double TotalRevenue { get; set; }
        public Dictionary<InvoiceStatus, int> InvoicesByStatus { get; set; } = new Dictionary<InvoiceStatus, int>();
    }

    public class CustomerInvoiceSummaryDTO
    {
        public int TotalInvoices { get; set; }
        public double TotalSpent { get; set; }
        public int TotalPoints { get; set; }
    }
}