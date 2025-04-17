namespace LapTrinhWindows.Models.dto.EmployeeDTO
{
    public class UpdateEmployeeProfileDTO
    {
        [Required]
        [MaxLength(100)]
        public string EmployeeName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Address { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string PhoneNumber { get; set; } = string.Empty;
        // email
        [Required]
        [MaxLength(100)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        

    }
}