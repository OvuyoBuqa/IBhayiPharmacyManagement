using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IBhayiPharmacyManagementSystem.Models
{
    public class DispensationRequest
    {
        [Key]
        public int DispensationRequestId { get; set; }
        
        [ForeignKey("PrescriptionRepeat")]
        public int PrescriptionRepeatId { get; set; }
        public virtual PrescriptionRepeat PrescriptionRepeat { get; set; }
        
        [ForeignKey("Customer")]
        public int CustomerId { get; set; }
        public virtual Customer Customer { get; set; }
        
        public DateTime RequestDate { get; set; }
        public DispensationRequestStatus Status { get; set; }
        public string? Notes { get; set; }
        public DateTime? ProcessedDate { get; set; }
        public int? ProcessedByPharmacistId { get; set; }
    }

    public enum DispensationRequestStatus
    {
        Pending,
        Processing,
        Ready,
        Dispensed,
        Cancelled
    }
}

