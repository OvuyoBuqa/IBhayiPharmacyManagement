using IBhayiPharmacyManagementSystem.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace IBhayiPharmacyManagementSystem.ViewModels
{
    public class PharmacyViewModel
    {
        public int PharmacyId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        [Display(Name = "Health Council Registration Number")]
        [StringLength(50)]
        public string HealthcareCouncilRegistrationNumber { get; set; }

        // Address fields
        [Required]
        [StringLength(200)]
        public string Street { get; set; }

        [Required]
        [StringLength(100)]
        public string Suburb { get; set; }

        [Required]
        [StringLength(100)]
        public string City { get; set; }

        [Required]
        [StringLength(100)]
        public string Province { get; set; }

        [Required]
        [Display(Name = "Zip Code")]
        [StringLength(10)]
        public string ZipCode { get; set; }

        [Required]
        [StringLength(100)]
        public string Country { get; set; }

        [Required]
        [Display(Name = "Contact Number")]
        [RegularExpression(@"^[0-9]{10}$", ErrorMessage = "Contact number must be exactly 10 digits")]
        public string ContactNumber { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Display(Name = "Website URL")]
        [Url(ErrorMessage = "Please enter a valid URL (e.g., https://www.example.com)")]
        [StringLength(500, ErrorMessage = "Website URL cannot exceed 500 characters")]
        public string? WebsiteURL { get; set; }

        [Required]
        [Display(Name = "Responsible Pharmacist")]
        public int PharmacistId { get; set; }

        public IEnumerable<SelectListItem>? Pharmacists { get; set; }
    }
}
