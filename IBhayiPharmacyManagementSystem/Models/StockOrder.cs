using IBhayiPharmacyManagementSystem.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IBhayiPharmacyManagementSystem.Models
{
    public class StockOrder
    {
        [Key]
        public int StockOrderId { get; set; }

        public DateTime StockOrderDate { get; set; } = DateTime.UtcNow;

        [Required]
        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; }

        public StockOrderStatusEnum StockOrderStatus { get; set; } = StockOrderStatusEnum.Pending;

        public string? Notes { get; set; }

        public DateTime? QuoteReceivedDate { get; set; }
        public decimal? QuoteAmount { get; set; }
        public string? QuoteNotes { get; set; }

        public ICollection<StockOrderItem> StockOrderItems { get; set; } = new List<StockOrderItem>();
    }
}
