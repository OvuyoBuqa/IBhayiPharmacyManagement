using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IBhayiPharmacyManagementSystem.Models
{
    public class MedicalInfo
    {
        [Key]
        public int MedicalInfoId { get; set; }

        [Required]
        public int CustomerId { get; set; }

        [ForeignKey("CustomerId")]
        public Customer Customer { get; set; }

        [StringLength(500)]
        public string? ChronicConditions { get; set; }

        [StringLength(1000)]
        public string? MedicalNotes { get; set; }

        [StringLength(100)]
        public string? EmergencyContactName { get; set; }

        [StringLength(20)]
        [RegularExpression(@"^[0-9]{10}$", ErrorMessage = "Emergency contact phone number must be exactly 10 digits")]
        [Display(Name = "Emergency Contact Phone")]
        public string? EmergencyContactPhone { get; set; }

        [StringLength(5)]
        public string? BloodType { get; set; }

        [StringLength(50)]
        public string? MedicalAidNumber { get; set; }

        [StringLength(100)]
        public string? MedicalAidScheme { get; set; }

        public DateTime DateCreated { get; set; } = DateTime.Now;

        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}
