using IBhayiPharmacyManagementSystem.Models;
using System.ComponentModel.DataAnnotations;

namespace IBhayiPharmacyManagementSystem.ViewModels
{
    public class RegisterWalkInCustomerViewModel
    {

        [Required]
        [Display(Name = "First Name")]
        public string Name { get; set; }

        [Required]
        [Display(Name = "Last Name")]
        public string Surname { get; set; }

        [Display(Name = "ID Number")]
        [StringLength(13, MinimumLength = 13, ErrorMessage = "SA ID must be 13 digits")]
        [RegularExpression(@"^[0-9]*$", ErrorMessage = "Only numbers allowed")]
        public string IDNumber { get; set; }

        [Display(Name = "Cell Phone Number")]
        [RegularExpression(@"^[0-9]{10}$", ErrorMessage = "Phone number must be exactly 10 digits")]
        public string CellPhoneNumber { get; set; }

        [EmailAddress]
        public string Email { get; set; }

        [Display(Name = "Street Address")]
        public string Street { get; set; }

        public string Suburb { get; set; }
        public string City { get; set; }
        public string Province { get; set; }

        [Display(Name = "Postal Code")]
        public string ZipCode { get; set; }

        public string Country { get; set; }

        public List<CustomerAllergyViewModel> Allergies { get; set; } = new List<CustomerAllergyViewModel>();

        // This will be used to populate the active ingredients dropdown
        [Display(Name = "Active Ingredients")]
        public List<ActiveIngredients> ActiveIngredients { get; set; }

    }
}
