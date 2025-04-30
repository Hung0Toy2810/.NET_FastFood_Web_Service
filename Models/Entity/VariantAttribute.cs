namespace LapTrinhWindows.Models
{
    public class VariantAttribute
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int VariantAttributeID { get; set; }

        [Required]
        public int VariantID { get; set; }

        [ForeignKey("VariantID")]
        public virtual Variant Variant { get; set; } = null!;

        [Required]
        public int AttributeID { get; set; }

        [ForeignKey("AttributeID")]
        public virtual Attribute Attribute { get; set; } = null!;

        [Required]
        public int AttributeValueID { get; set; }

        [ForeignKey("AttributeValueID")]
        public virtual AttributeValue AttributeValue { get; set; } = null!;
    }
}