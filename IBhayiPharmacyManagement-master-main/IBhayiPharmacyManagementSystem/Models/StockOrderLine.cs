using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IBhayiPharmacyManagementSystem.Models
{
    public class StockOrderLine
    {
        [Key]
        public int StockOrderLineId { get; set; }


        [ForeignKey("StockOrder")]
        public int StockOrderId { get; set; }

        [ForeignKey("Medication")]
        public int MedicationId { get; set; }

        public int Quantity { get; set; }
        public double UnitPrice { get; set; }
    }
}
