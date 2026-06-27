using IBhayiPharmacyManagementSystem.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IBhayiPharmacyManagementSystem.Models
{
    public class OrderItem
    {
        [Key]
        public int OrderItemId { get; set; }

        [ForeignKey("Order")]
        public int OrderId { get; set; }
        public Order Order { get; set; }

        [ForeignKey("Medication")]
        public int MedicationId { get; set; }
        public Medication Medication { get; set; }

        [Range(1, int.MaxValue)]
        public int QuantityOrdered { get; set; }

        [Range(0, int.MaxValue)]
        public int QuantityDispensed { get; set; } = 0;

        [Range(0, double.MaxValue)]
        public double UnitPrice { get; set; }

        [NotMapped]
        public double TotalPrice => UnitPrice * QuantityOrdered;

        public DispensingStatusEnum DispensingStatus { get; set; } = DispensingStatusEnum.Pending;

        [ForeignKey("Pharmacist")]
        public int? DispensedBy { get; set; }
        public DateTime? DispensedDate { get; set; }

        public string? DispensingNotes { get; set; } = null; // Make nullable and initialize as null
    }
}
