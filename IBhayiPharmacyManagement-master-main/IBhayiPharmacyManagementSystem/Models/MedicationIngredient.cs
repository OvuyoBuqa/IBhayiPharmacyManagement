using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace IBhayiPharmacyManagementSystem.Models
{
    public class MedicationIngredient
    {
        [Key]
        public int MedicationIngredientId { get; set; }

        [ForeignKey("Medication")]
        public int MedicationId { get; set; }
        public Medication Medication { get; set; }

        [ForeignKey("ActiveIngredient")]
        public int ActiveIngredientId { get; set; }
        public ActiveIngredients ActiveIngredient { get; set; }

        [Required]
        [StringLength(50)]
        public string Strength { get; set; }
    }
}
