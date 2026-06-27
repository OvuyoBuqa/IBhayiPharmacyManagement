using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IBhayiPharmacyManagementSystem.Models
{
    public class ActiveIngredients
    {
        [Key]
        public int ActiveIngredientId { get; set; }

        [Required]
        [MaxLength(100)]
        public string? Name { get; set; }

        [Required]
        public string? Description{ get; set; }

        [Required]
        [StringLength(50)]
        public string? Strength { get; set; }

        // Soft delete properties
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }

        public ICollection<MedicationIngredient> MedicationIngredients { get; set; } = new List<MedicationIngredient>();
        public ICollection<CustomerAllergy> CustomerAllergies { get; set; } = new List<CustomerAllergy>();
    }
}
