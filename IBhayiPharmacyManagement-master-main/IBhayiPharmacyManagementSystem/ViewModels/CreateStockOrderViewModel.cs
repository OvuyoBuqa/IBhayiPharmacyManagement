using IBhayiPharmacyManagementSystem.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace IBhayiPharmacyManagementSystem.ViewModels
{
    public class CreateStockOrderViewModel
    {
        [Required]
        public int SupplierId { get; set; }

        public List<CreateStockOrderItemViewModel> StockOrderItems { get; set; } = new List<CreateStockOrderItemViewModel>();

        public List<SelectListItem> Suppliers { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Medications { get; set; } = new List<SelectListItem>();

        // Properties for adding a new medication
        public bool IsNewMedicationSelected { get; set; } = false;

        public string? NewMedicationName { get; set; }
        public int? NewMedicationSchedule { get; set; } = null;
        public string? NewMedicationDescription { get; set; }
        // Price removed for quote-based system
        // public decimal? NewMedicationPrice { get; set; } = null;
        public int? NewMedicationMinStockLevel { get; set; } = null;
        public int? NewMedicationQuantityInStock { get; set; } = null;
        public int? NewMedicationDosageFormId { get; set; } = null;
        public List<SelectListItem> DosageForms { get; set; } = new List<SelectListItem>();
    }

    public class CreateStockOrderItemViewModel
    {
        public int MedicationId { get; set; }
        public bool IsNewMedication { get; set; } = false;

        // Properties for New Medication (only if IsNewMedication is true)
        [Required(ErrorMessage = "Medication Name is required for new medications.")]
        public string? Name { get; set; }
        [Required(ErrorMessage = "Schedule is required for new medications.")]
        [Range(0, int.MaxValue, ErrorMessage = "Schedule must be a non-negative number.")]
        public int Schedule { get; set; }
        public string? Description { get; set; }
        // Price removed for quote-based system
        // [Required(ErrorMessage = "Price is required for new medications.")]
        // [Range(0.01, double.MaxValue, ErrorMessage = "Price must be a positive number.")]
        // public decimal Price { get; set; }
        [Required(ErrorMessage = "Min Stock Level is required for new medications.")]
        [Range(0, int.MaxValue, ErrorMessage = "Min Stock Level must be a non-negative number.")]
        public int MinStockLevel { get; set; }
        [Required(ErrorMessage = "Initial Stock Quantity is required for new medications.")]
        [Range(1, int.MaxValue, ErrorMessage = "Initial Stock Quantity must be at least 1.")]
        public int QuantityInStock { get; set; }
        [Required(ErrorMessage = "Dosage Form is required for new medications.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a Dosage Form for new medications.")]
        public int DosageFormId { get; set; }

        public List<ActiveIngredientViewModel> ActiveIngredients { get; set; } = new List<ActiveIngredientViewModel>();

        [Required(ErrorMessage = "Quantity is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int QuantityOrdered { get; set; }

        [Display(Name = "Notes")]
        [StringLength(200)]
        public string? Notes { get; set; }
    }

    public class ActiveIngredientViewModel
    {
        public int ActiveIngredientId { get; set; }
        [Required(ErrorMessage = "Strength is required for active ingredients.")]
        public string? Strength { get; set; }
    }
}
