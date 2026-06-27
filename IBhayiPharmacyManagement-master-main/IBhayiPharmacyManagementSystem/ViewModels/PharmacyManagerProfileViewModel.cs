using System.ComponentModel.DataAnnotations;

namespace IBhayiPharmacyManagementSystem.ViewModels
{
    public class PharmacyManagerProfileViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public int PharmacyManagerId { get; set; }

        [Required(ErrorMessage = "Name is required.")]
        [Display(Name = "First Name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Surname is required.")]
        [Display(Name = "Last Name")]
        public string Surname { get; set; } = string.Empty;

        [Required(ErrorMessage = "Contact Number is required.")]
        [RegularExpression(@"^[0-9]{10}$", ErrorMessage = "Contact number must be exactly 10 digits")]
        [Display(Name = "Contact Number")]
        public string ContactNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Branch Name")]
        public string BranchName { get; set; } = string.Empty;

        [Display(Name = "Pharmacy Name")]
        public string PharmacyName { get; set; } = string.Empty;
    }
}
