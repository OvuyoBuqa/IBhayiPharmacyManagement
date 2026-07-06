using IBhayiPharmacyManagementSystem.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace IBhayiPharmacyManagementSystem.ViewModels
{
    public class WalkInCustomerViewModel
    {
        // Customer Selection
        public int SelectedCustomerId { get; set; }
        public List<SelectListItem> Customers { get; set; } = new List<SelectListItem>();


        // Prescription Upload
        [Display(Name = "Prescription File")]
        public IFormFile? PrescriptionFile { get; set; }

        [Display(Name = "Comments")]
        public string? PrescriptionComments { get; set; }

        // New Customer Registration
        [Display(Name = "First Name")]
        public string? NewCustomerName { get; set; }

        [Display(Name = "Last Name")]
        public string? NewCustomerSurname { get; set; }

        [Display(Name = "ID Number")]
        [StringLength(13, MinimumLength = 13, ErrorMessage = "SA ID must be 13 digits")]
        [RegularExpression(@"^[0-9]*$", ErrorMessage = "Only numbers allowed")]
        public string? NewCustomerIDNumber { get; set; }

        [Display(Name = "Date of Birth")]
        [DataType(DataType.Date)]
        public DateTime? NewCustomerDateOfBirth { get; set; }

        [Display(Name = "Email")]
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        public string? NewCustomerEmail { get; set; }

        [Display(Name = "Phone Number")]
        [Required(ErrorMessage = "Phone number is required.")]
        [RegularExpression(@"^[0-9]{10}$", ErrorMessage = "Phone number must be exactly 10 digits")]
        public string? NewCustomerPhone { get; set; }

        [Display(Name = "Street Address")]
        public string? NewCustomerStreet { get; set; }

        public string? NewCustomerSuburb { get; set; }
        public string? NewCustomerCity { get; set; }
        public string? NewCustomerProvince { get; set; }

        [Display(Name = "Postal Code")]
        public string? NewCustomerZipCode { get; set; }

        // Allergies for new customer - simplified for form binding
        public List<NewCustomerAllergyViewModel> NewCustomerAllergies { get; set; } = new List<NewCustomerAllergyViewModel>();

        // Active Ingredients for allergy selection
        public List<ActiveIngredients> ActiveIngredients { get; set; } = new List<ActiveIngredients>();

        // Medication Display (for showing processed prescriptions)
        public List<MedicationViewModel> Medications { get; set; } = new List<MedicationViewModel>();

        // Current customer details (for display)
        public CustomerDetailsViewModel? CurrentCustomerDetails { get; set; }

        // Prescription Items (for local storage before submission)
        public string? PrescriptionItems { get; set; }
    }

    public class MedicationViewModel
    {
        public int MedicationId { get; set; }
        public string? Name { get; set; }
        public string? DosageForm { get; set; }
        public string? Strength { get; set; }
        public int Quantity { get; set; }
        public string? Information { get; set; }
        public bool Repeat { get; set; }
        public int RepeatsLeft { get; set; }
        public string? Instructions { get; set; }
    }

    // Simplified allergy model for new customer registration
    public class NewCustomerAllergyViewModel
    {
        public int ActiveIngredientId { get; set; }
        public string? Severity { get; set; }
        public string? Description { get; set; }
    }
}

// Prescription item model for form submission
public class PrescriptionItemViewModel
{
    public int MedicationId { get; set; }
    public int Quantity { get; set; }
    public string? Instructions { get; set; }
    public string? Frequency { get; set; }
    public int Repeats { get; set; }
}
