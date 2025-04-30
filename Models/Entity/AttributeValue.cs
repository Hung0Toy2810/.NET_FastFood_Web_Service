namespace LapTrinhWindows.Models
{
    public class AttributeValue
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AttributeValueID { get; set; }

        [Required]
        public int AttributeID { get; set; }

        [ForeignKey("AttributeID")]
        public virtual Attribute Attribute { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string Value { get; set; } = string.Empty; // Giá trị (e.g., 8GB, 256GB)
        //VariantAttributes
        public virtual ICollection<VariantAttribute> VariantAttributes { get; set; } = new List<VariantAttribute>();
    }
}