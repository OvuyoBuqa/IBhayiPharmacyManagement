using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IBhayiPharmacyManagementSystem.Models
{
    public class DispensedPrescription
    {
        [Key]
        public int DispensedPrescriptionId { get; set; }

        [ForeignKey("PrescriptionLine")]
        public int PrescriptionLineId { get; set; }
        public virtual PrescriptionLine PrescriptionLine { get; set; }

        [ForeignKey("Pharmacist")]
        public int PharmacistId { get; set; }
        public virtual Pharmacist Pharmacist { get; set; }

        public DateTime DispensedDate { get; set; } = DateTime.UtcNow;
        public int QuantityDispensed { get; set; }
        public decimal AmountDue { get; set; }
        public bool IsPaid { get; set; } = false;
        public DateTime? PaymentDate { get; set; }
        
        public string? DispensingNotes { get; set; }
        public string? PatientInstructions { get; set; }
        
        // Navigation properties
        public virtual Prescription Prescription => PrescriptionLine?.Prescription;
        public virtual Customer Customer => Prescription?.Customer;
        public virtual Medication Medication => PrescriptionLine?.Medication;
        public virtual Doctor Doctor => Prescription?.Doctor;
    }
}

