using IBhayiPharmacyManagementSystem.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.Contracts;

namespace IBhayiPharmacyManagementSystem.Models
{
    public class Order
    {
        [Key]
        public int OrderId { get; set; }

        [ForeignKey("Customer")]
        public int CustomerId { get; set; }
        public Customer Customer { get; set; }


        [DataType(DataType.DateTime)]
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        [Range(0, double.MaxValue)]
        public double TotalAmount { get; set; }

        public bool PaymentStatus { get; set; }

        public OrderStatusEnum OrderStatus { get; set; } = OrderStatusEnum.Pending;

        [DataType(DataType.DateTime)]
        public DateTime? CollectedDate { get; set; }

        [ForeignKey("Pharmacist")]
        public int? PharmacistId { get; set; }
        public Pharmacist? Pharmacist { get; set; }

       

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

        [NotMapped]
        public double CalculatedTotal => OrderItems.Sum(i => i.TotalPrice);

        public DateTime? LastUpdated { get; set; }
        public string? OrderNotes { get; set; }
    }
}

