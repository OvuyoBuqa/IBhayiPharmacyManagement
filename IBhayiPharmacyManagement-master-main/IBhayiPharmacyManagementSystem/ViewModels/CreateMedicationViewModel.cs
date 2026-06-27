using IBhayiPharmacyManagementSystem.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace IBhayiPharmacyManagementSystem.ViewModels
{
    public class CreateMedicationViewModel
    {
        [Required(ErrorMessage = "Medication Name is required.")]
        [StringLength(100, ErrorMessage = "Medication name cannot exceed 100 characters.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Schedule is required.")]
        [Range(0, int.MaxValue, ErrorMessage = "Schedule must be a non-negative number.")]
        public int Schedule { get; set; }

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Price is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be a positive number.")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "Min Stock Level is required.")]
        [Range(0, int.MaxValue, ErrorMessage = "Min Stock Level must be a non-negative number.")]
        public int MinStockLevel { get; set; }

        [Required(ErrorMessage = "Initial Stock Quantity is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Initial Stock Quantity must be at least 1.")]
        public int QuantityInStock { get; set; }

        [Required(ErrorMessage = "Dosage Form is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a Dosage Form.")]
        public int DosageFormId { get; set; }

        [Required(ErrorMessage = "Supplier is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a Supplier.")]
        public int SupplierId { get; set; }

        public List<MedicationIngredientViewModel> ActiveIngredients { get; set; } = new List<MedicationIngredientViewModel>();

        // Dropdown lists
        public List<SelectListItem> DosageForms { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Suppliers { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> AvailableActiveIngredients { get; set; } = new List<SelectListItem>();
    }

    public class MedicationIngredientViewModel
    {
        [Required(ErrorMessage = "Active Ingredient is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select an Active Ingredient.")]
        public int ActiveIngredientId { get; set; }

        [Required(ErrorMessage = "Strength is required.")]
        [StringLength(50, ErrorMessage = "Strength cannot exceed 50 characters.")]
        public string Strength { get; set; } = string.Empty;
    }
}
