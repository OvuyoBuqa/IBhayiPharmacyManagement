
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IBhayiPharmacyManagementSystem.Models
{
    public class Pharmacist
    {
        [Key]
        public int PharmacistId { get; set; }
        [Required]
        [ForeignKey("User")]
        public string UserId { get; set; } = string.Empty;

        // Navigation property to Identity User
        public virtual Users User { get; set; } = null!;

        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Surname { get; set; } = string.Empty;
        [Required(ErrorMessage = "ID Number is required.")]
        [StringLength(13, MinimumLength = 13, ErrorMessage = "SA ID must be 13 digits")]
        [RegularExpression(@"^[0-9]*$", ErrorMessage = "Only numbers allowed")]
        [Display(Name = "ID Number")]
        public string IDNumber { get; set; } = string.Empty;

        public string RegistrationNumber { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Cell phone number is required.")]
        [RegularExpression(@"^[0-9]{10}$", ErrorMessage = "Phone number must be exactly 10 digits")]
        [Display(Name = "Cell Phone")]
        public string CellPhone { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}
