using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IBhayiPharmacyManagementSystem.Models
{
    public class CustomerActivity
    {
        [Key]
        public int ActivityId { get; set; }

        [Required]
        [ForeignKey("Customer")]
        public int CustomerId { get; set; }
        public virtual Customer Customer { get; set; }

        [Required]
        [StringLength(100)]
        public string ActivityType { get; set; } // e.g., "Login", "PrescriptionUploaded", "OrderCreated", "RepeatRequested", "ProfileUpdated"

        [Required]
        [StringLength(500)]
        public string Description { get; set; }

        [StringLength(100)]
        public string? EntityType { get; set; } // e.g., "Prescription", "Order", "PrescriptionRepeat"

        public int? EntityId { get; set; }

        public DateTime Timestamp { get; set; }

        [StringLength(45)]
        public string? IPAddress { get; set; }

        [StringLength(500)]
        public string? AdditionalData { get; set; } // JSON for extra details

        public bool IsRead { get; set; } = false;
    }
}

