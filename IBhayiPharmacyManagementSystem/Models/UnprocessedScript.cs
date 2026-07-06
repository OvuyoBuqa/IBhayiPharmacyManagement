using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection.Metadata;

namespace IBhayiPharmacyManagementSystem.Models
{
    public class UnprocessedScript
    {
        [Key]
        public int UnploadId { get; set; }

        [ForeignKey("Customer")]   
        public int CustomerId { get; set; }
        public virtual Customer Customer { get; set; }

        // Add these properties for Doctor relationship
        [ForeignKey("Doctor")]
        public int? DoctorId { get; set; }
        public virtual Doctor? Doctor { get; set; }

        public DateOnly UploadDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        public string ScriptImagePath { get; set; }

        // Store file content in database
        public byte[]? FileContent { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }

        [NotMapped] 
        [Required(ErrorMessage = "Please select a prescription file")]
        public IFormFile ScriptImage { get; set; }

      
        public string? Comments { get; set; }
        public PrescriptionStatus Status { get; set; } = PrescriptionStatus.Pending;
        public string? RejectionReason { get; set; }
        public string? ProcessingNotes { get; set; }
        public bool RequestDispensation { get; set; } = false;

        [ForeignKey("ProcessedBy")]
        public string? ProcessedById { get; set; }
        public virtual Users? ProcessedBy { get; set; }

        public DateTime? ProcessedDate { get; set; }


        public enum PrescriptionStatus
        {
            Pending,        // Initial state
            Processing,     // Currently being reviewed
            Rejected,       // Rejected with reason
            Completed       // Successfully processed
        }

        // Add this navigation property to Prescription
        public virtual Prescription? Prescription { get; set; }


    }
}
