using IBhayiPharmacyManagementSystem.Data;
using IBhayiPharmacyManagementSystem.Enums;
using IBhayiPharmacyManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace IBhayiPharmacyManagementSystem.Controllers
{
    [Authorize]
    public class OrderTrackingController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly ILogger<OrderTrackingController> _logger;

        public OrderTrackingController(
            AppDbContext context,
            UserManager<Users> userManager,
            ILogger<OrderTrackingController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: OrderTracking - Main tracking dashboard
        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            // Get all orders with their related data
            var orders = await _context.Orders
                .Include(o => o.Customer)
                    .ThenInclude(c => c.User)
                .Include(o => o.Pharmacist)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Medication)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            // Group orders by status for tracking
            var trackingData = new OrderTrackingViewModel
            {
                TotalOrders = orders.Count,
                PendingOrders = orders.Where(o => o.OrderStatus == OrderStatusEnum.Pending).ToList(),
                ProcessingOrders = orders.Where(o => o.OrderStatus == OrderStatusEnum.Processing).ToList(),
                // Exclude already collected orders from Ready list
                ReadyOrders = orders
                    .Where(o => o.OrderStatus == OrderStatusEnum.Ready)
                    .ToList(),
                CancelledOrders = orders.Where(o => o.OrderStatus == OrderStatusEnum.Cancelled).ToList(),
                AllOrders = orders
            };

            return View(trackingData);
        }

        // GET: OrderTracking/ReadyForCollection - Orders ready for pickup
        public async Task<IActionResult> ReadyForCollection()
        {
            var readyOrders = await _context.Orders
                .Include(o => o.Customer)
                    .ThenInclude(c => c.User)
                .Include(o => o.Pharmacist)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Medication)
                // Only show orders that are Ready
                .Where(o => o.OrderStatus == OrderStatusEnum.Ready)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(readyOrders);
        }

        // GET: OrderTracking/Collected - Orders that have been collected
        public async Task<IActionResult> Collected()
        {
            // Since we don't have a "Collected" status, we'll use a custom approach
            // We'll track this through OrderNotes or create a separate tracking mechanism
            var collectedOrders = await _context.Orders
                .Include(o => o.Customer)
                    .ThenInclude(c => c.User)
                .Include(o => o.Pharmacist)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Medication)
                .Where(o => o.OrderStatus == OrderStatusEnum.Collected)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(collectedOrders);
        }

        // POST: OrderTracking/MarkAsCollected - Mark an order as collected
        [HttpPost]
        public async Task<IActionResult> MarkAsCollected(int orderId)
        {
            try
            {
                var order = await _context.Orders.FindAsync(orderId);
                if (order == null)
                {
                    return Json(new { success = false, message = "Order not found" });
                }

                if (order.OrderStatus != OrderStatusEnum.Ready)
                {
                    return Json(new { success = false, message = "Order must be ready before marking as collected" });
                }

                // Mark as collected by updating status and collected date
                order.OrderStatus = OrderStatusEnum.Collected;
                order.CollectedDate = DateTime.UtcNow;
                order.LastUpdated = DateTime.Now;

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Order marked as collected successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking order {OrderId} as collected", orderId);
                return Json(new { success = false, message = "An error occurred while updating the order" });
            }
        }

        // POST: OrderTracking/MarkAsReady - Mark an order as ready for collection
        [HttpPost]
        public async Task<IActionResult> MarkAsReady(int orderId)
        {
            try
            {
                var order = await _context.Orders.FindAsync(orderId);
                if (order == null)
                {
                    return Json(new { success = false, message = "Order not found" });
                }

                if (order.OrderStatus != OrderStatusEnum.Processing)
                {
                    return Json(new { success = false, message = "Order must be processing before marking as ready" });
                }

                order.OrderStatus = OrderStatusEnum.Ready;
                order.LastUpdated = DateTime.Now;

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Order marked as ready for collection" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking order {OrderId} as ready", orderId);
                return Json(new { success = false, message = "An error occurred while updating the order" });
            }
        }

        // GET: OrderTracking/Details/{id} - Detailed view of an order for tracking
        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Customer)
                    .ThenInclude(c => c.User)
                .Include(o => o.Pharmacist)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Medication)
                        .ThenInclude(m => m.DosageForm)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        // GET: OrderTracking/Statistics - Order tracking statistics
        public async Task<IActionResult> Statistics()
        {
            var orders = await _context.Orders.ToListAsync();
            
            var stats = new OrderTrackingStatsViewModel
            {
                TotalOrders = orders.Count,
                PendingCount = orders.Count(o => o.OrderStatus == OrderStatusEnum.Pending),
                ProcessingCount = orders.Count(o => o.OrderStatus == OrderStatusEnum.Processing),
                // Exclude collected orders from Ready count
                ReadyCount = orders.Count(o => o.OrderStatus == OrderStatusEnum.Ready),
                CancelledCount = orders.Count(o => o.OrderStatus == OrderStatusEnum.Cancelled),
                CollectedCount = orders.Count(o => o.OrderStatus == OrderStatusEnum.Collected),
                AverageProcessingTime = CalculateAverageProcessingTime(orders),
                OrdersByDay = GetOrdersByDay(orders)
            };

            return View(stats);
        }

        private double CalculateAverageProcessingTime(List<Order> orders)
        {
            var processingOrders = orders.Where(o => o.OrderStatus == OrderStatusEnum.Ready || 
                                                   o.OrderStatus == OrderStatusEnum.Collected).ToList();
            
            if (!processingOrders.Any()) return 0;

            var totalHours = processingOrders.Sum(o => 
            {
                var readyTime = o.LastUpdated ?? o.OrderDate;
                var processingTime = readyTime - o.OrderDate;
                return processingTime.TotalHours;
            });

            return totalHours / processingOrders.Count;
        }

        private Dictionary<string, int> GetOrdersByDay(List<Order> orders)
        {
            return orders
                .GroupBy(o => o.OrderDate.Date)
                .OrderByDescending(g => g.Key)
                .Take(30) // Last 30 days
                .ToDictionary(g => g.Key.ToString("yyyy-MM-dd"), g => g.Count());
        }
    }

    // View Models for Order Tracking
    public class OrderTrackingViewModel
    {
        public int TotalOrders { get; set; }
        public List<Order> PendingOrders { get; set; } = new List<Order>();
        public List<Order> ProcessingOrders { get; set; } = new List<Order>();
        public List<Order> ReadyOrders { get; set; } = new List<Order>();
        public List<Order> CancelledOrders { get; set; } = new List<Order>();
        public List<Order> AllOrders { get; set; } = new List<Order>();
    }

    public class OrderTrackingStatsViewModel
    {
        public int TotalOrders { get; set; }
        public int PendingCount { get; set; }
        public int ProcessingCount { get; set; }
        public int ReadyCount { get; set; }
        public int CancelledCount { get; set; }
        public int CollectedCount { get; set; }
        public double AverageProcessingTime { get; set; }
        public Dictionary<string, int> OrdersByDay { get; set; } = new Dictionary<string, int>();
    }
}
