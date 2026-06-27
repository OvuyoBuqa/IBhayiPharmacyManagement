using IBhayiPharmacyManagementSystem.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace IBhayiPharmacyManagementSystem.ViewModels
{
    public class CustomerAllergyViewModel
    {
        public int AllergyId { get; set; }

        [Required]
        [Display(Name = "Customer")]
        public int CustomerId { get; set; }
        public IEnumerable<Customer>? Customers { get; set; }

        [Required]
        [Display(Name = "Active Ingredient")]
        public int ActiveIngredientId { get; set; }
        public IEnumerable<ActiveIngredients>? ActiveIngredients { get; set; }

        [Required]
        public string? Severity { get; set; }

        public string? Description { get; set; }
    }
}