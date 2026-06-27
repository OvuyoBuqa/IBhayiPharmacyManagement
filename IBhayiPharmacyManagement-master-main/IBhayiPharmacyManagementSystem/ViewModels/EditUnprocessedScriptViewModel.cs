using System.ComponentModel.DataAnnotations;

namespace IBhayiPharmacyManagementSystem.ViewModels
{
    public class EditUnprocessedScriptViewModel
    {
        public int UnploadId { get; set; }
        public int CustomerId { get; set; }
        public DateOnly UploadDate { get; set; }
        public string ScriptImagePath { get; set; } = string.Empty;
        
        // Database-stored file properties
        public byte[]? FileContent { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        
        public PrescriptionStatus Status { get; set; }
        public string? ProcessedById { get; set; }
        public DateTime? ProcessedDate { get; set; }

        [Display(Name = "Comments")]
        public string? Comments { get; set; }

        [Display(Name = "Request Dispensation")]
        public bool RequestDispensation { get; set; }

        [Display(Name = "New Prescription File")]
        public IFormFile? NewScriptImage { get; set; }

        public enum PrescriptionStatus
        {
            Pending,
            Processing,
            Rejected,
            Completed
        }
    }
}
