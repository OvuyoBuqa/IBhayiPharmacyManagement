using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IBhayiPharmacyManagementSystem.Models
{
    public class StockMovement
    {
        [Key]
        public int MovementId { get; set; }

        [Required]
        public int MedicationId { get; set; }
        [ForeignKey("MedicationId")]
        public Medication Medication { get; set; }

        [Required]
        [StringLength(50)]
        public string MovementType { get; set; } // e.g., "Increment", "Decrement", "Adjustment", "Initial"

        [Required]
        [Range(0, int.MaxValue)]
        public int QuantityChanged { get; set; }

        [Required]
        public DateTime Timestamp { get; set; }

        [StringLength(500)]
        public string? Reason { get; set; }

        // Optional: Reference to the user who made the adjustment
        public string? UserId { get; set; }
        [ForeignKey("UserId")]
        public Users? User { get; set; }
    }
}
