using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IBhayiPharmacyManagementSystem.Models
{
    public class CustomerAllergy
    {
        [Key]
        public int AllergyId { get; set; }

        [Required]
        [ForeignKey("Customer")]
        public int CustomerId { get; set; }
        public  Customer Customer { get; set; }  // Navigation property to Customer

        [Required]
        [ForeignKey("ActiveIngredient")]
        public int ActiveIngredientId { get; set; }
        public ActiveIngredients ActiveIngredient { get; set; } // Navigation property to ActiveIngredients

        [Required]
        public string Severity { get; set; }

        public string Description { get; set; }
    }
}