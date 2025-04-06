namespace LapTrinhWindows.Models.dto.CustomerDTO
{
    public class CustomerProfileDTO
    {

        [MaxLength(100)]
        public string CustomerName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Address { get; set; } = string.Empty;

        [MaxLength(100)]
        public string PhoneNumber { get; set; } = string.Empty;

        public byte[]? AvtFileData { get; set; }
    }
}