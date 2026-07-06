using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IBhayiPharmacyManagementSystem.Models
{
    public class StockOrderItem
    {
        [Key]
        public int StockOrderItemId { get; set; }

        [ForeignKey("StockOrder")]
        public int StockOrderId { get; set; }
        public StockOrder StockOrder { get; set; }

        [ForeignKey("Medication")]
        public int? MedicationId { get; set; }
        public Medication Medication { get; set; }

        [Range(1, int.MaxValue)]
        public int QuantityOrdered { get; set; }

        public string? Notes { get; set; }
    }
}
