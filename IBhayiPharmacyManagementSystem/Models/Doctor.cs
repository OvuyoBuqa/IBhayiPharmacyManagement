using System.ComponentModel.DataAnnotations;

namespace IBhayiPharmacyManagementSystem.Models
{
    public class Doctor
    {
        [Key]
        public int DoctorId { get; set; }
        public string? Name { get; set; }
        public string? Surname { get; set; }
        public string? Email { get; set; }
        [RegularExpression(@"^[0-9]{10,11}$", ErrorMessage = "Phone number must be 10 or 11 digits")]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        public int PracticeNumber { get; set; }

        // Computed property for full name
        public string FullName => $"{Name} {Surname}".Trim();

        public ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();
        public ICollection<UnprocessedScript> UnprocessedScripts { get; set; } = new List<UnprocessedScript>();
    }
}
