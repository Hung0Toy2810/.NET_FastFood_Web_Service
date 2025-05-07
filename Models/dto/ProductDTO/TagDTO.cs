namespace LapTrinhWindows.Models.DTO
{
    public class UpsertProductTagsDTO
    {
        [Required]
        public int ProductID { get; set; }

        [Required]
        public List<string> TagNames { get; set; } = new List<string>();
    }
}