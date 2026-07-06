using System.ComponentModel.DataAnnotations;

namespace IBhayiPharmacyManagementSystem.ViewModels
{
    public class StockTakeItemViewModel
    {
        public int MedicationId { get; set; }

        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "Counted quantity must be a non-negative number.")]
        public int CountedQuantity { get; set; }
    }
}
