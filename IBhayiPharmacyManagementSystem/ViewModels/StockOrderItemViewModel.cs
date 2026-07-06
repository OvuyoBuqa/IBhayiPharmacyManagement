using System.ComponentModel.DataAnnotations;

namespace IBhayiPharmacyManagementSystem.ViewModels
{
    public class StockOrderItemViewModel
    {
        public int StockOrderItemId { get; set; }

        [Required]
        [Display(Name = "Medication")]
        public int? MedicationId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        [Display(Name = "Quantity Ordered")]
        public int QuantityOrdered { get; set; }

        [Display(Name = "Notes")]
        [StringLength(200)]
        public string? Notes { get; set; }
    }
} 