using System.ComponentModel.DataAnnotations;

namespace IBhayiPharmacyManagementSystem.Models
{
    public class Supplier
    {
        [Key]
        public int SupplierId { get; set; }
        public string Name { get; set; }
        public string ContactPerson { get; set; }
        public string Email { get; set; }
    }
}
