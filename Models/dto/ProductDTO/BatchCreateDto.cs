namespace LapTrinhWindows.Models.DTO
{
    public class BatchCreateDto
    {
        public string Sku { get; set; } = string.Empty;
        public DateTime? ExpirationDate { get; set; }
        public DateTime? ProductionDate { get; set; }
        public int AvailableQuantity { get; set; }
    }
}