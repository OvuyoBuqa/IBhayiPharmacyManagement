using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace IBhayiPharmacyManagementSystem.Models
{
    public class DosageForm
    {
        [Key]
        public int DosageFormId { get; set; }

        [Required]
        [StringLength(50)]
        public string? Type { get; set; }

        [StringLength(255)]
        public string? Description { get; set; }

        // Navigation property
        public virtual ICollection<Medication> Medications { get; set; } = new List<Medication>();

        public static class DosageFormTypes
        {
            public static readonly List<string> Types = new List<string>
            {
                "Tablet", "Capsule", "Syrup", "Injection", "Ointment",
                "Cream", "Suppository", "Drops", "Inhaler", "Patch",
                "Powder", "Solution", "Suspension", "Other"
            };
        }


    }
}