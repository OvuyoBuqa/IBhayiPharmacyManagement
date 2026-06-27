using System.ComponentModel.DataAnnotations;

namespace IBhayiPharmacyManagementSystem.ViewModels
{
    public class AdjustStockViewModel
    {
        [Required]
        public int MedicationId { get; set; }

        [Display(Name = "Medication Name")]
        public string MedicationName { get; set; }

        [Display(Name = "Current Quantity")]
        public int CurrentQuantity { get; set; }

        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "Quantity must be zero or positive.")]
        [Display(Name = "Adjustment Quantity")]
        public int AdjustmentQuantity { get; set; }

        [Required]
        [RegularExpression("^(Increment|Set|Decrement)$", ErrorMessage = "Adjustment Type must be Increment, Set, or Decrement.")]
        [Display(Name = "Adjustment Type")]
        public string AdjustmentType { get; set; }

        [StringLength(500)]
        public string? Reason { get; set; }
    }
}
