namespace LapTrinhWindows.Models.dto.CustomerDTO
{
    public class ChangePasswordDTO
    {
        [Required]
        [MaxLength(100)]
        public required string OldPassword { get; set; }
        [Required]
        [MaxLength(100)]
        public required string NewPassword { get; set; }
    }
}