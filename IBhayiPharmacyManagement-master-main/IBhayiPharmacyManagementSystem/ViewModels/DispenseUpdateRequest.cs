using IBhayiPharmacyManagementSystem.Enums;
using System.ComponentModel.DataAnnotations;

namespace IBhayiPharmacyManagementSystem.ViewModels
{
    public class DispenseUpdateRequest
    {
        [Required]
        public int OrderItemId { get; set; }

        [Required]
        public DispensingStatusEnum DispensingStatus { get; set; }
    }
}


