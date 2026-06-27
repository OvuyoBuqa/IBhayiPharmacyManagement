using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IBhayiPharmacyManagementSystem.Models
{
    public class Prescription
    {
        [Key]
        public int PrescriptionId { get; set; }


        [ForeignKey("Customer")]
        public int CustomerId { get; set; }
        public virtual Customer Customer { get; set; }

        [ForeignKey("Doctor")]
        public int? DoctorId { get; set; }
        public virtual Doctor Doctor { get; set; }


        [ForeignKey("Pharmacist")]
        public int? PharmacistId { get; set; }
        public virtual Pharmacist Pharmacist { get; set; }

        public int? UploadId { get; set; }

        public DateTime PrescriptionDate { get; set; } 

        // Inverse navigation to PrescriptionLines
        public virtual ICollection<PrescriptionLine> PrescriptionLines { get; set; } = new List<PrescriptionLine>();
    }
}
