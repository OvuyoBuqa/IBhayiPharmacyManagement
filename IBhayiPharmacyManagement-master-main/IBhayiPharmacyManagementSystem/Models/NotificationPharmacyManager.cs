using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IBhayiPharmacyManagementSystem.Enums;

namespace IBhayiPharmacyManagementSystem.Models
{
    public class NotificationPharmacyManager
    {
        [Key]
        public int NotificationId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [Required]
        public string Message { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public NotificationType Type { get; set; } = NotificationType.Info; // Enum for Info, Warning, Error, Success

        public bool IsRead { get; set; } = false;

        [StringLength(500)]
        public string? Link { get; set; }

        // Optional: UserId to link notification to a specific user
        public string? UserId { get; set; }
        [ForeignKey("UserId")]
        public Users? User { get; set; }
    }
}
