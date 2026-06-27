using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IBhayiPharmacyManagementSystem.Models
{
    public class Notification
    {
        [Key]
        public int NotificationId { get; set; }

        [ForeignKey("Customer")]
        public int CustomerId { get; set; }
        public virtual Customer Customer { get; set; }

        [Required]
        [StringLength(500)]
        public string Message { get; set; }

        public DateTime DateSent { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;
        public string? RelatedEntityId { get; set; } // e.g., PrescriptionId, OrderId, RepeatRequestId
        public string? RelatedEntityType { get; set; } // e.g., "Prescription", "Order", "RepeatRequest"
    }
}
