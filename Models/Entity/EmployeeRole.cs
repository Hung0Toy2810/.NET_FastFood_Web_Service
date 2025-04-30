namespace LapTrinhWindows.Models
{
    public class EmployeeRole
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RoleID { get; set; }
        [Required]
        [MaxLength(100)]
        public string RoleName { get; set; } = string.Empty;
        public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
    }
}