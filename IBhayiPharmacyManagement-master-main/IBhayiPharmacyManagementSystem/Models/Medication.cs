using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace IBhayiPharmacyManagementSystem.Models
{
    public class Medication
    {
        [Key]
        public int MedicationId { get; set; }

        [Required]
        [StringLength(100)]
        public string? Name { get; set; }

        public int Schedule { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public double Price { get; set; }

        public int MinStockLevel { get; set; }
        public int QuantityInStock { get; set; }
        public bool IsNewMedication { get; set; } = false;

        // DosageForm relationship (Many-to-One)
        [ForeignKey("DosageFormId")]
        public virtual DosageForm? DosageForm { get; set; }
        public int DosageFormId { get; set; }

        // Supplier relationship (Many-to-One)
        [ForeignKey("SupplierId")]
        public virtual Supplier? Supplier { get; set; }
        public int SupplierId { get; set; }

        // Ingredients relationship (One-to-Many)
        public virtual ICollection<MedicationIngredient> ActiveIngredients { get; set; } = new List<MedicationIngredient>();
    }
}