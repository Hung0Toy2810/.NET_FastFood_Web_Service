namespace LapTrinhWindows.Models
{
    public class ProductTag
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ProductTagID { get; set; }

        [Required]
        public int ProductID { get; set; }

        [ForeignKey("ProductID")]
        public virtual Product Product { get; set; } = null!;

        [Required]
        public int TagID { get; set; }

        [ForeignKey("TagID")]
        public virtual Tag Tag { get; set; } = null!;
    }
}