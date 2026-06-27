using IBhayiPharmacyManagementSystem.Enums;
using IBhayiPharmacyManagementSystem.Models;
using System.ComponentModel.DataAnnotations;

namespace IBhayiPharmacyManagementSystem.ViewModels
{
    public class StockOrderViewModel
    {
        public int StockOrderId { get; set; }

        [Required]
        [Display(Name = "Supplier")]
        public int SupplierId { get; set; }
        public IEnumerable<Supplier>? Suppliers { get; set; }

        [Display(Name = "Notes")]
        [StringLength(500)]
        public string? Notes { get; set; }

        public List<NewStockOrderItemViewModel> Items { get; set; } = new List<NewStockOrderItemViewModel>();
    }

    public class NewStockOrderItemViewModel
    {
        public int? StockOrderItemId { get; set; }
        public int? MedicationId { get; set; }
        public string? ExistingMedicationName { get; set; }

        [Required(ErrorMessage = "Quantity is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int QuantityOrdered { get; set; }

        // New Medication Fields
        public bool IsNewMedication { get; set; }

        [Display(Name = "Notes")]
        [StringLength(200)]
        public string? Notes { get; set; }
    }

    public class QuoteViewModel
    {
        public int StockOrderId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public List<QuoteItemViewModel> Items { get; set; } = new List<QuoteItemViewModel>();

        [Display(Name = "Quote Amount")]
        [Range(0, double.MaxValue, ErrorMessage = "Quote amount must be a positive value.")]
        public decimal? QuoteAmount { get; set; }

        [Display(Name = "Quote Notes")]
        [StringLength(500)]
        public string? QuoteNotes { get; set; }
    }

    public class QuoteItemViewModel
    {
        public int StockOrderItemId { get; set; }
        public string MedicationName { get; set; } = string.Empty;
        public int QuantityOrdered { get; set; }
        public string? Notes { get; set; }
    }
}
