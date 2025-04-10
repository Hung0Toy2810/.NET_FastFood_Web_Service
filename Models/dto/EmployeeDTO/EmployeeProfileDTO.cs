namespace LapTrinhWindows.Models.dto.EmployeeDTO
{
    public class EmployeeProfileDTO
    {
        public string FullName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? RoleName { get; set; } // Vai trò của nhân viên
        public string? AvatarUrl { get; set; } // URL của avatar
        public EmployeeStatus Status { get; set; } // Trạng thái online/offline
    }
}