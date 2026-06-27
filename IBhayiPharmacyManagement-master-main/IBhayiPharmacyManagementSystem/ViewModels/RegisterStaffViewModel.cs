using System.ComponentModel.DataAnnotations;

namespace IBhayiPharmacyManagementSystem.ViewModels
{
    [ValidatePharmacistDetails]
    public class RegisterStaffViewModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        // Removed password fields as password will be auto-generated

        [Required]
        [Display(Name = "Role")]
        public string Role { get; set; } = string.Empty;

        // Pharmacist specific fields
        [Display(Name = "ID Number")]
        [StringLength(13, MinimumLength = 13, ErrorMessage = "SA ID must be 13 digits")]
        [RegularExpression(@"^[0-9]*$", ErrorMessage = "Only numbers allowed")]
        public string? IDNumber { get; set; }

        [Display(Name = "Cellphone Number")]
        [RegularExpression(@"^[0-9]{10}$", ErrorMessage = "Phone number must be exactly 10 digits")]
        public string? CellPhoneNumber { get; set; }

        [Display(Name = "Health Council Registration Number")]
        // [Required(ErrorMessage = "Registration Number is required for Pharmacists.", AllowEmptyStrings = false)]
        public string? RegistrationNumber { get; set; }

        // Holds available roles for the current user
        public List<string> AvailableRoles { get; set; } = new List<string>();
    }
}
