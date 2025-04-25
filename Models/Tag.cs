using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LapTrinhWindows.Models
{
    public class Tag
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TagID { get; set; }

        [Required]
        [MaxLength(50)]
        public string TagName { get; set; } = string.Empty;

        public virtual ICollection<ProductTag> ProductTags { get; set; } = new List<ProductTag>();
    }
}