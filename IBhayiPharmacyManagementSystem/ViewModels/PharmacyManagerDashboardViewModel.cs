using IBhayiPharmacyManagementSystem.Models;

namespace IBhayiPharmacyManagementSystem.ViewModels
{
    public class PharmacyManagerDashboardViewModel
    {
        public string SelectedTimeframe { get; set; } = "This Week";
        public int TotalItems { get; set; }
        public string TotalItemsChange { get; set; }
        public int LowStockItems { get; set; }
        public string LowStockChange { get; set; }
        public int OrdersCount { get; set; }
        public string OrdersChange { get; set; }
        public decimal InventoryValue { get; set; }
        public string InventoryValueChange { get; set; }
        public List<Medication> Medications { get; set; }
        public int NotificationsCount { get; set; }
        public List<NotificationViewModel> Notifications { get; set; }
        public string CurrentUserFullName { get; set; }
        public int TotalMedications { get; internal set; }
        public int PendingOrders { get; internal set; }
        public int ActivePharmacists { get; internal set; }
        public List<StockOverviewItemViewModel> StockOverview { get; internal set; }
        public List<RecentActivityItemViewModel> RecentActivities { get; internal set; }
    }
}
