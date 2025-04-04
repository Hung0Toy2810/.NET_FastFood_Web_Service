namespace LapTrinhWindows.Models.dto.CustomerDTO
{
    public class LoginDTO
    {
        [Required]
        [MaxLength(100)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Password { get; set; } = string.Empty;
    }
}