using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IBhayiPharmacyManagementSystem.Models
{
    public class PrescriptionRepeat
    {
        [Key]
        public int PrescriptionRepeatId { get; set; }

        [ForeignKey("PrescriptionLine")]
        public int PrescriptionLineId { get; set; }
        public virtual PrescriptionLine PrescriptionLine { get; set; }

        [ForeignKey("Customer")]
        public int CustomerId { get; set; }
        public virtual Customer Customer { get; set; }

        public int TotalRepeats { get; set; } = 0;
        public int RemainingRepeats { get; set; } = 0;
        public int QuantityPerRepeat { get; set; } = 0;
        public int DispensedCount { get; set; } = 0;
        
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? LastDispensedDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        // Navigation property to get the prescription
        public virtual Prescription Prescription => PrescriptionLine?.Prescription;
        public virtual Medication Medication => PrescriptionLine?.Medication;
    }
}

