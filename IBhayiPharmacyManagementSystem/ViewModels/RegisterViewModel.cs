using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using IBhayiPharmacyManagementSystem.Models;

namespace IBhayiPharmacyManagementSystem.ViewModels
{
    public class RegisterViewModel
    {
        // Personal Information
        [Required(ErrorMessage = "Name is required.")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Surname is required.")]
        public string Surname { get; set; }

        [Required(ErrorMessage = "ID Number is required.")]
        [StringLength(13, MinimumLength = 13, ErrorMessage = "SA ID must be 13 digits")]
        [RegularExpression(@"^[0-9]*$", ErrorMessage = "Only numbers allowed")]
        public string IDNumber { get; set; }

        [Required(ErrorMessage = "Cell phone number is required.")]
        [RegularExpression(@"^[0-9]{10}$", ErrorMessage = "Phone number must be exactly 10 digits")]
        public string CellPhoneNumber { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        public string Email { get; set; }

        // Address Information
        [Required(ErrorMessage = "Street is required.")]
        public string Street { get; set; }

        [Required(ErrorMessage = "Suburb is required.")]
        public string Suburb { get; set; }

        [Required(ErrorMessage = "City is required.")]
        public string City { get; set; }

        [Required(ErrorMessage = "Province is required.")]
        public string Province { get; set; }

        [Required(ErrorMessage = "Postal code is required.")]
        public string ZipCode { get; set; }

        public string Country { get; set; } = "South Africa";

        // Allergy Information
        [Required(ErrorMessage = "Allergy status is required.")]
        public string AllergyStatus { get; set; } = "NoAllergies";
        
        // Database allergies
        public List<int> SelectedAllergyIds { get; set; } = new List<int>();
        public List<string> AllergySeverities { get; set; } = new List<string>();
        public List<string> AllergyDescriptions { get; set; } = new List<string>();
        
        // Custom allergies
        public List<string> CustomAllergenNames { get; set; } = new List<string>();
        public List<string> CustomAllergySeverities { get; set; } = new List<string>();
        public List<string> CustomAllergyDescriptions { get; set; } = new List<string>();
        
        // Available active ingredients for selection
        public List<ActiveIngredients> AvailableActiveIngredients { get; set; } = new List<ActiveIngredients>();

        // Password
        [Required(ErrorMessage = "Password is required.")]
        [StringLength(40, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 40 characters.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required(ErrorMessage = "Confirm Password is required.")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; }
    }


}