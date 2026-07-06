namespace IBhayiPharmacyManagementSystem.ViewModels
{
    public class NotificationViewModel
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public Enums.NotificationType Type { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; }
        public string Link { get; set; }
    }
}
