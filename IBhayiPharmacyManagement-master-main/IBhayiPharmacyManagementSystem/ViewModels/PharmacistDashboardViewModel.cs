using IBhayiPharmacyManagementSystem.Enums;
using static IBhayiPharmacyManagementSystem.Models.UnprocessedScript;

namespace IBhayiPharmacyManagementSystem.ViewModels
{
    public class PharmacistDashboardViewModel
    {
        // Prescription Statistics
        public int TotalPrescriptionsToday { get; set; }
        public int PendingPrescriptions { get; set; }
        public int ProcessingPrescriptions { get; set; }
        public int CompletedPrescriptionsToday { get; set; }
        public int RejectedPrescriptionsToday { get; set; }

        // Medication Statistics
        public int TotalMedications { get; set; }
        public int LowStockMedications { get; set; }
        public int OutOfStockMedications { get; set; }

        // Order Statistics
        public int TotalOrdersToday { get; set; }
        public int PendingOrders { get; set; }
        public int ProcessingOrders { get; set; }
        public int ReadyOrders { get; set; }
        public decimal TodaySalesTotal { get; set; }

        // Customer Statistics
        public int NewCustomersToday { get; set; }
        public int TotalCustomers { get; set; }
        public int WalkInCustomersToday { get; set; }

        // Recent Activity Lists
        public List<RecentPrescription> RecentPrescriptions { get; set; }
        public List<RecentOrder> RecentOrders { get; set; }
        public List<LowStockItem> LowStockItems { get; set; }

        // Charts Data
        public Dictionary<string, int> PrescriptionStatusDistribution { get; set; }
        public Dictionary<string, int> OrderStatusDistribution { get; set; }
        public Dictionary<string, decimal> WeeklySales { get; set; }

        public class RecentPrescription
        {
            public int PrescriptionId { get; set; }
            public string CustomerName { get; set; }
            public DateTime UploadDate { get; set; }
            public PrescriptionStatus Status { get; set; }
            public int ItemCount { get; set; }
        }

        public class RecentOrder
        {
            public int OrderId { get; set; }
            public string CustomerName { get; set; }
            public DateTime OrderDate { get; set; }
            public OrderStatusEnum Status { get; set; }
            public decimal TotalAmount { get; set; }
        }

        public class LowStockItem
        {
            public int MedicationId { get; set; }
            public string MedicationName { get; set; }
            public int CurrentStock { get; set; }
            public int MinimumStockLevel { get; set; }
        }
    }
}
