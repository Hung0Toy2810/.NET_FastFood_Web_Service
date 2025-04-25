namespace LapTrinhWindows.Models
{
    public class Attribute
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AttributeID { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty; 

        public int? CategoryID { get; set; } 

        [ForeignKey("CategoryID")]
        public virtual Category? Category { get; set; }

        public virtual ICollection<AttributeValue> AttributeValues { get; set; } = new List<AttributeValue>();
        //VariantAttributes
        public virtual ICollection<VariantAttribute> VariantAttributes { get; set; } = new List<VariantAttribute>();
    }
}