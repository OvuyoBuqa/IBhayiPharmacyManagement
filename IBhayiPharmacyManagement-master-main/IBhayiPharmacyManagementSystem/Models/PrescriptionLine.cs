using IBhayiPharmacyManagementSystem.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IBhayiPharmacyManagementSystem.Models
{
    public class PrescriptionLine
    {
        [Key]
        public int PrescriptionLineId { get; set; }

        [ForeignKey("Prescription")]
        public int PrescriptionId { get; set; }
        public virtual Prescription? Prescription { get; set; }

        

        [ForeignKey("Medication")]
        public int MedicationId { get; set; }
        public virtual Medication? Medication { get; set; } 


        public int Quantity { get; set; }
        public string Instructions { get; set; } = string.Empty;
        public DosageFrequency Frequency { get; set; } // Uses the enum
        public int TotalRepeats { get; set; } = 0; // Total number of repeats allowed
        public int RepeatsRemaining { get; set; } = 0; // Remaining repeats
        
        // Soft delete properties
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
        
        // Navigation property for repeats - temporarily commented out
        // public virtual PrescriptionRepeat? PrescriptionRepeat { get; set; }
        // public virtual ICollection<DispensedPrescription> DispensedPrescriptions { get; set; } = new List<DispensedPrescription>();
    }
}
