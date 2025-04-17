namespace LapTrinhWindows.Models.dto.EmployeeDTO
{
    public class EmployeeLoginDTO
    {
        [Required]
        [EmailAddress]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Password { get; set; } = string.Empty;
    }
}