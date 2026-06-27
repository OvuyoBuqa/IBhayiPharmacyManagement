using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IBhayiPharmacyManagementSystem.Models
{
    public class Customer
    {
        [Key]
        public int CustomerId { get; set; }

        [Required]
        public string UserId { get; set; } // Link to Identity User

        [Required(ErrorMessage = "Name is required.")]
        [Display(Name = "First Name")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Surname is required.")]
        [Display(Name = "Last Name")]
        public string Surname { get; set; }

        [Required(ErrorMessage = "ID Number is required.")]
        [StringLength(13, MinimumLength = 13, ErrorMessage = "SA ID must be 13 digits")]
        [RegularExpression(@"^[0-9]*$", ErrorMessage = "Only numbers allowed")]
        [Display(Name = "ID Number")]
        public string IDNumber { get; set; }

        [Required(ErrorMessage = "Cell phone number is required.")]
        [RegularExpression(@"^[0-9]{10}$", ErrorMessage = "Phone number must be exactly 10 digits")]
        [Display(Name = "Cell Phone")]
        public string CellPhoneNumber { get; set; }

        public bool IsWalkInCustomer { get; set; } = false;

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        // Address fields
        public string? Street { get; set; }
        public string? Suburb { get; set; }
        public string? City { get; set; }
        public string? Province { get; set; }

        [Display(Name = "Postal Code")]
        public string? ZipCode { get; set; }

        public string? Country { get; set; } = "South Africa";

        public string? ProfileImagePath { get; set; } = "/images/default-profile.png";

        // Computed property for full name
        [NotMapped]
        public string FullName => $"{Name} {Surname}".Trim();

        public DateTime DateCreated { get; set; } = DateTime.Now;

        // Navigation property
        [ForeignKey("UserId")]
        public virtual Users User { get; set; }

        public virtual ICollection<CustomerAllergy> Allergies { get; set; } = new List<CustomerAllergy>();
        public virtual ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();

        public virtual MedicalInfo? MedicalInfo { get; set; }
    }
}