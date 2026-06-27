namespace IBhayiPharmacyManagementSystem.ViewModels
{
    public class RecentActivityItemViewModel
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime Timestamp { get; set; }
        public string Link { get; set; }
        public string IconClass { get; set; } // e.g., "bi-receipt", "bi-people", "bi-capsule"
        public string BackgroundClass { get; set; } // e.g., "bg-info-subtle", "bg-success-subtle"
    }
}
