namespace LapTrinhWindows.Models
{
    public class Attribute
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AttributeID { get; set; }

        [Required]
        [MaxLength(50)]
        public string AttributeName { get; set; } = string.Empty; 

        public virtual ICollection<AttributeValue> AttributeValues { get; set; } = new List<AttributeValue>();
        //VariantAttributes
        public virtual ICollection<VariantAttribute> VariantAttributes { get; set; } = new List<VariantAttribute>();
    }
}