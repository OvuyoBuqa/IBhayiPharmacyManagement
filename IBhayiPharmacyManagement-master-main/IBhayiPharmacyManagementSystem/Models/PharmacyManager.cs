using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace IBhayiPharmacyManagementSystem.Models
{
    public class PharmacyManager
    {
        [Key]
        public int PharmacyManagerId { get; set; }
        // Foreign key to the User table
        [Required]
        [ForeignKey("User")]
        public string UserId { get; set; } = string.Empty;
        // Navigation property to Identity User
        public virtual Users User { get; set; } = null!;
        // Additional properties for the Pharmacy Manager
        public string Name { get; set; } = string.Empty;
        
        public string Surname { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        [RegularExpression(@"^[0-9]{10}$", ErrorMessage = "Contact number must be exactly 10 digits")]
        [Display(Name = "Contact Number")]
        public string ContactNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;

        public int PharmacyId { get; set; }
        [ForeignKey("PharmacyId")]
        public virtual Pharmacy Pharmacy { get; set; } = null!;
    }
}
