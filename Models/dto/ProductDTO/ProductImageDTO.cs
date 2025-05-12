namespace  LapTrinhWindows.Models.DTO;
    public class ProductImageDTO
    {
        [Required]
        [MaxLength(100)]
        public string ImageUrl { get; set; } = string.Empty; 

        [Required]
        public int OrdinalNumbers { get; set; } = 0; 
    }
