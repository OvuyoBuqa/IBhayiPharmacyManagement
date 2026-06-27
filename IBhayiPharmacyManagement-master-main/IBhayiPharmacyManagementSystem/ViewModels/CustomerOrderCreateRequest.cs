using System.ComponentModel.DataAnnotations;
using IBhayiPharmacyManagementSystem.ViewModels;

namespace IBhayiPharmacyManagementSystem.ViewModels
{
    public class CustomerOrderCreateRequest
    {
        [Required(ErrorMessage = "Please add at least one medication to your order")]
        public List<OrderItemRequest> OrderItems { get; set; } = new List<OrderItemRequest>();

        [Display(Name = "Order Notes")]
        public string? OrderNotes { get; set; }
    }
}
