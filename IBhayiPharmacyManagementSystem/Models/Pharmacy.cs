using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IBhayiPharmacyManagementSystem.Models
{
    public class Pharmacy
    {
        [Key]
        public int PharmacyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string HealthcareCouncilRegistrationNumber { get; set; } = string.Empty;

        public int? AddressId { get; set; }
        [ForeignKey("AddressId")]
        public Address? Address { get; set; } = null;

        public string ContactNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string WebsiteURL { get; set; } = string.Empty;

        public int PharmacistId { get; set; }
        [ForeignKey("PharmacistId")]
        public Pharmacist Pharmacist { get; set; } = null!;
    }
}
