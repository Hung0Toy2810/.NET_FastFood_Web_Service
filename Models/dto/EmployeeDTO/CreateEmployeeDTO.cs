namespace LapTrinhWindows.Models.dto.EmployeeDTO
{
    public class CreateEmployeeDTO
    {
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
        public string Password { get; set; } = string.Empty; 

        [Required]
        public int RoleID { get; set; }

        [MaxLength(100)]
        public string AvtKey { get; set; } = "https://www.gravatar.com/avatar/205e460b479e2e5b48aec07710c08d50";

        [Required]
        public EmployeeStatus Status { get; set; } = EmployeeStatus.Offline; 
        public IFormFile? AvtFile { get; set; } = null;
    }
}