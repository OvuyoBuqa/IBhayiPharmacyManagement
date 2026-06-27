using System.ComponentModel.DataAnnotations;

namespace IBhayiPharmacyManagementSystem.ViewModels
{
    
        public class StockOrderLineViewModel
        {
            [Required]
            public int MedicationId { get; set; }

            [Required]
            [Range(1, int.MaxValue)]
            public int Quantity { get; set; }

            [Required]
            [Range(0.01, double.MaxValue)]
            public double UnitPrice { get; set; }
        }
    }

