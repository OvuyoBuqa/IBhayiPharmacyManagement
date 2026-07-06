using IBhayiPharmacyManagementSystem.Data;
using IBhayiPharmacyManagementSystem.Models;
using IBhayiPharmacyManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IBhayiPharmacyManagementSystem.Controllers
{
    [Authorize(Roles = "Customer")]
    public class CustomerActivityController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly ICustomerActivityService _activityService;
        private readonly ILogger<CustomerActivityController> _logger;

        public CustomerActivityController(
            AppDbContext context,
            UserManager<Users> userManager,
            ICustomerActivityService activityService,
            ILogger<CustomerActivityController> logger)
        {
            _context = context;
            _userManager = userManager;
            _activityService = activityService;
            _logger = logger;
        }

        // GET: CustomerActivity - Show activity history
        public async Task<IActionResult> Index(string searchTerm = "", int page = 1, int pageSize = 10)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

                if (customer == null)
                {
                    return Forbid();
                }

                // Build query for this customer's activities
                IQueryable<CustomerActivity> query = _context.CustomerActivities
                    .Where(ca => ca.CustomerId == customer.CustomerId);

                // Apply search filter if search term is provided (minimum 2 characters)
                if (!string.IsNullOrWhiteSpace(searchTerm) && searchTerm.Length >= 2)
                {
                    var searchTermLower = searchTerm.ToLower();
                    query = query.Where(a => 
                        a.ActivityType.ToLower().Contains(searchTermLower) ||
                        a.Description.ToLower().Contains(searchTermLower) ||
                        (a.IPAddress != null && a.IPAddress.ToLower().Contains(searchTermLower)) ||
                        (a.AdditionalData != null && a.AdditionalData.ToLower().Contains(searchTermLower)) ||
                        a.Timestamp.ToString().Contains(searchTermLower) ||
                        a.ActivityId.ToString().Contains(searchTermLower) ||
                        (a.EntityId.HasValue && a.EntityId.Value.ToString().Contains(searchTermLower)) ||
                        (a.EntityType != null && a.EntityType.ToLower().Contains(searchTermLower))
                    );
                }

                // Count total activities
                var totalActivities = await query.CountAsync();

                // Apply pagination
                var activities = await query
                    .OrderByDescending(ca => ca.Timestamp)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Pass pagination info to view
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = (int)Math.Ceiling((double)totalActivities / pageSize);
                ViewBag.TotalActivities = totalActivities;
                ViewBag.PageSize = pageSize;
                ViewBag.SearchTerm = searchTerm;

                return View(activities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customer activity history");
                TempData["ErrorMessage"] = "Error loading activity history.";
                return RedirectToAction("Index", "Home");
            }
        }

        // GET: CustomerActivity/MarkAsRead/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return Json(new { success = false, message = "Unauthorized" });
                }

                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

                if (customer == null)
                {
                    return Json(new { success = false, message = "Customer not found" });
                }

                var activity = await _context.CustomerActivities
                    .FirstOrDefaultAsync(ca => ca.ActivityId == id && ca.CustomerId == customer.CustomerId);

                if (activity == null)
                {
                    return Json(new { success = false, message = "Activity not found" });
                }

                activity.IsRead = true;
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking activity as read");
                return Json(new { success = false, message = "Error updating activity" });
            }
        }

        // GET: CustomerActivity/MarkAllAsRead
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead()
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return Json(new { success = false, message = "Unauthorized" });
                }

                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

                if (customer == null)
                {
                    return Json(new { success = false, message = "Customer not found" });
                }

                var activities = await _context.CustomerActivities
                    .Where(ca => ca.CustomerId == customer.CustomerId && !ca.IsRead)
                    .ToListAsync();

                foreach (var activity in activities)
                {
                    activity.IsRead = true;
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true, count = activities.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all activities as read");
                return Json(new { success = false, message = "Error updating activities" });
            }
        }

        // GET: CustomerActivity/GetUnreadCount
        public async Task<IActionResult> GetUnreadCount()
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return Json(new { count = 0 });
                }

                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

                if (customer == null)
                {
                    return Json(new { count = 0 });
                }

                var unreadCount = await _context.CustomerActivities
                    .Where(ca => ca.CustomerId == customer.CustomerId && !ca.IsRead)
                    .CountAsync();

                return Json(new { count = unreadCount });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread count");
                return Json(new { count = 0 });
            }
        }
    }
}

