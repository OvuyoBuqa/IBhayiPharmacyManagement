using System.ComponentModel.DataAnnotations;
using IBhayiPharmacyManagementSystem.Models;

namespace IBhayiPharmacyManagementSystem.ViewModels
{
    public class ProfileViewModel
    {
        [Required(ErrorMessage = "Name is required.")]
        public string? Name { get; set; }
        public string? Surname { get; set; }
        public string FullName => $"{Name} {Surname}";
        public string IDNumber { get; set; }

        public string CellPhoneNumber { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        public string Email { get; set; }
        public string Street { get; set; }
        public string Suburb { get; set; }

        public string Province { get; set; }


        public string City { get; set; }

        public string Country { get; set; }
        public string ZipCode { get; set; }

        public string FullAddress { get; set; } // add this

        // Keep the computed logic for use in controller
        public string GenerateFullAddress()
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(Street)) parts.Add($"Street: {Street}");
            if (!string.IsNullOrEmpty(Suburb)) parts.Add($"Suburb: {Suburb}");

            var cityParts = new List<string>();
            if (!string.IsNullOrEmpty(City)) cityParts.Add(City);
            if (!string.IsNullOrEmpty(Province)) cityParts.Add(Province);
            if (!string.IsNullOrEmpty(ZipCode)) cityParts.Add(ZipCode);

            if (cityParts.Any()) parts.Add($"Location: {string.Join(", ", cityParts)}");
            if (!string.IsNullOrEmpty(Country)) parts.Add($"Country: {Country}");

            return string.Join(Environment.NewLine, parts); // each line is a field
        }



        public DateTime DateCreated { get; set; }


        [Required(ErrorMessage = "Password is required.")]
        [StringLength(40, MinimumLength = 8, ErrorMessage = "The {0} must be at {2} and at max {1} characters long.")]
        [DataType(DataType.Password)]
        [Compare("ConfirmPassword", ErrorMessage = "Password does not match.")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Confirm Password is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; }

        public string ProfileImagePath { get; set; }

        // Medical Information
        public string? ChronicConditions { get; set; }
        public string? MedicalNotes { get; set; }
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactPhone { get; set; }
        public string? BloodType { get; set; }
        public string? MedicalAidNumber { get; set; }
        public string? MedicalAidScheme { get; set; }
        
        // Allergies
        public List<CustomerAllergy> Allergies { get; set; } = new List<CustomerAllergy>();

        // For allergy management
        public int CustomerId { get; set; } // Added CustomerId to view model
        public List<ActiveIngredients>? AvailableActiveIngredients { get; set; }
        public List<int>? SelectedAllergyIds { get; set; }
        public List<string>? AllergySeverities { get; set; }
        public List<string>? AllergyDescriptions { get; set; }
        public List<string>? CustomAllergenNames { get; set; }
        public List<string>? CustomAllergySeverities { get; set; }
        public List<string>? CustomAllergyDescriptions { get; set; }

        public List<int>? ExistingAllergyIds { get; set; }
        public List<int>? ExistingActiveIngredientIds { get; set; }
        public List<string>? ExistingAllergySeverities { get; set; }
        public List<string>? ExistingAllergyDescriptions { get; set; }

        public List<int>? NewSelectedAllergyIds { get; set; }
        public List<string>? NewAllergySeverities { get; set; }
        public List<string>? NewAllergyDescriptions { get; set; }

        public List<int>? RemovedAllergyIds { get; set; }
    }
}
