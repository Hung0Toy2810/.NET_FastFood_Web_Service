namespace LapTrinhWindows.Models.dto
{
    public class PointRedemptionDTO
    {
        public int PointRedemptionID { get; set; }

        [Required]
        public string SKU { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string RedemptionName { get; set; } = string.Empty;

        [Required]
        [Range(1, int.MaxValue)]
        public int PointsRequired { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int AvailableQuantity { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        public PointRedemptionStatus Status { get; set; }
    }
}