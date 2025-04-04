using System.ComponentModel.DataAnnotations;

namespace LapTrinhWindows.Models.dto.CustomerDTO
{
    public class CreateCustomerDTO
    {
        [Required]
        [MaxLength(100)]
        public string CustomerName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Address { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Password { get; set; } = string.Empty;

        [MaxLength(100)]
        public string AvtKey { get; set; } = "https://www.gravatar.com/avatar/205e460b479e2e5b48aec07710c08d50";
        
        public IFormFile? AvtFile { get; set; } = null;
    }
}