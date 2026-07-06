using IBhayiPharmacyManagementSystem.Data;
using IBhayiPharmacyManagementSystem.Models;
using IBhayiPharmacyManagementSystem.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace IBhayiPharmacyManagementSystem.Controllers
{
    public class NotificationsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;

        public NotificationsController(AppDbContext context, UserManager<Users> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

            if (customer == null) return Forbid();

            var notifications = await _context.Notifications
                .Where(n => n.CustomerId == customer.CustomerId)
                .OrderByDescending(n => n.DateSent)
                .ToListAsync();

            return View(notifications);
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Json(new { notifications = new List<object>(), count = 0 });

            var userRoles = await _userManager.GetRolesAsync(currentUser);
            var notifications = new List<object>();
            int totalCount = 0;

            if (userRoles.Contains("Customer"))
            {
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == currentUser.Id);
                if (customer != null)
                {
                    var customerNotifications = await GetCustomerNotifications(customer.CustomerId);
                    notifications.AddRange(customerNotifications);
                    totalCount += customerNotifications.Count;
                }
            }
            else if (userRoles.Contains("Pharmacist"))
            {
                var pharmacistNotifications = await GetPharmacistNotifications(currentUser.Id);
                notifications.AddRange(pharmacistNotifications);
                totalCount += pharmacistNotifications.Count;
            }
            else if (userRoles.Contains("PharmacyManager"))
            {
                var managerNotifications = await GetPharmacyManagerNotifications(currentUser.Id);
                notifications.AddRange(managerNotifications);
                totalCount += managerNotifications.Count;
            }

            return Json(new { notifications, count = totalCount });
        }

        private async Task<List<object>> GetCustomerNotifications(int customerId)
        {
            var notifications = new List<object>();

            // Check for processed scripts
            var processedScripts = await _context.UnprocessedScripts
                .Where(s => s.CustomerId == customerId && s.Status == UnprocessedScript.PrescriptionStatus.Completed)
                .CountAsync();

            if (processedScripts > 0)
            {
                notifications.Add(new
                {
                    icon = "fas fa-check-circle",
                    message = $"{processedScripts} prescription(s) have been processed and are ready for collection",
                    time = "Just now",
                    link = Url.Action("ProcessedScripts", "UnprocessedScripts")
                });
            }

            // Check for ready orders
            var readyOrders = await _context.Orders
                .Where(o => o.CustomerId == customerId && o.OrderStatus == OrderStatusEnum.Ready)
                .CountAsync();

            if (readyOrders > 0)
            {
                notifications.Add(new
                {
                    icon = "fas fa-shopping-bag",
                    message = $"{readyOrders} order(s) are ready for collection",
                    time = "Just now",
                    link = Url.Action("Index", "Orders")
                });
            }

            // Check for dispensed medications
            var dispensedMedications = await _context.DispensedPrescriptions
                .Include(dp => dp.PrescriptionLine)
                    .ThenInclude(pl => pl.Prescription)
                .Where(dp => dp.PrescriptionLine.Prescription.CustomerId == customerId && !dp.IsPaid)
                .CountAsync();

            if (dispensedMedications > 0)
            {
                notifications.Add(new
                {
                    icon = "fas fa-pills",
                    message = $"{dispensedMedications} medication(s) have been dispensed and are ready for collection",
                    time = "Just now",
                    link = Url.Action("DispensedPrescriptions", "Prescriptions")
                });
            }

            return notifications;
        }

        private async Task<List<object>> GetPharmacistNotifications(string userId)
        {
            var notifications = new List<object>();

            // Check for new orders
            var newOrders = await _context.Orders
                .Where(o => o.OrderStatus == OrderStatusEnum.Pending)
                .CountAsync();

            if (newOrders > 0)
            {
                notifications.Add(new
                {
                    icon = "fas fa-shopping-cart",
                    message = $"{newOrders} new order(s) require processing",
                    time = "Just now",
                    link = Url.Action("Review", "Orders")
                });
            }

            // Check for new prescriptions
            var newPrescriptions = await _context.UnprocessedScripts
                .Where(s => s.Status == UnprocessedScript.PrescriptionStatus.Pending)
                .CountAsync();

            if (newPrescriptions > 0)
            {
                notifications.Add(new
                {
                    icon = "fas fa-file-prescription",
                    message = $"{newPrescriptions} new prescription(s) require processing",
                    time = "Just now",
                    link = Url.Action("PendingScripts", "Prescriptions")
                });
            }

            // Check for repeat requests
            var repeatRequests = await _context.DispensationRequests
                .Where(dr => dr.Status == DispensationRequestStatus.Pending)
                .CountAsync();

            if (repeatRequests > 0)
            {
                notifications.Add(new
                {
                    icon = "fas fa-redo",
                    message = $"{repeatRequests} repeat request(s) require processing",
                    time = "Just now",
                    link = Url.Action("Index", "DispensationRequests")
                });
            }

            return notifications;
        }

        private async Task<List<object>> GetPharmacyManagerNotifications(string userId)
        {
            var notifications = new List<object>();

            // Check for low stock medications
            var lowStockMedications = await _context.Medications
                .Where(m => m.QuantityInStock <= m.MinStockLevel)
                .CountAsync();

            if (lowStockMedications > 0)
            {
                notifications.Add(new
                {
                    icon = "fas fa-exclamation-triangle",
                    message = $"{lowStockMedications} medication(s) are running low on stock",
                    time = "Just now",
                    link = Url.Action("LowStock", "StockManagements")
                });
            }

            // Check for pending stock orders
            var pendingStockOrders = await _context.StockOrders
                .Where(so => so.StockOrderStatus == StockOrderStatusEnum.Pending)
                .CountAsync();

            if (pendingStockOrders > 0)
            {
                notifications.Add(new
                {
                    icon = "fas fa-boxes",
                    message = $"{pendingStockOrders} stock order(s) require attention",
                    time = "Just now",
                    link = Url.Action("Index", "StockOrders")
                });
            }

            // Check for new medications that need approval
            var newMedications = await _context.Medications
                .Where(m => m.IsNewMedication)
                .CountAsync();

            if (newMedications > 0)
            {
                notifications.Add(new
                {
                    icon = "fas fa-plus-circle",
                    message = $"{newMedications} new medication(s) require approval",
                    time = "Just now",
                    link = Url.Action("MedicationHistory", "Medications")
                });
            }

            return notifications;
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int notificationId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

            if (customer == null) return Forbid();

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.CustomerId == customer.CustomerId);

            if (notification == null) return NotFound();

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            // For now, we'll just return success since we're using dynamic notifications
            // In a real implementation, you might want to store read status in a separate table
            return Json(new { success = true });
        }
    }
}
