using IBhayiPharmacyManagementSystem.Models;
using IBhayiPharmacyManagementSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using IBhayiPharmacyManagementSystem.ViewModels;
using IBhayiPharmacyManagementSystem.Enums;

namespace IBhayiPharmacyManagementSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;

        public HomeController(ILogger<HomeController> logger, AppDbContext context, UserManager<Users> userManager)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            if (User.Identity.IsAuthenticated)
            {
                if (User.IsInRole("Admin"))
                    return RedirectToAction("Admin", "Home");
                else if (User.IsInRole("PharmacyManager"))
                    return RedirectToAction("PharmacyManager", "Home");
                else if (User.IsInRole("Pharmacist"))
                    return RedirectToAction("Pharmacist", "Home");
                else if (User.IsInRole("Customer"))
                    return RedirectToAction("Customer", "Home");
                else
                    return RedirectToAction("Login", "Account");
            }

            //Show Landing page onlt for non-autheticated users
            return View();
        }
        
        [Authorize]
        public IActionResult Privacy()
        {
            return View();
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Admin()
        {
            return View();
        }

        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> Customer()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.UserId == user.Id);

                if (customer == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                // Get real-time data for dashboard
                var activePrescriptions = await _context.UnprocessedScripts
                    .Include(u => u.Customer)
                    .Where(u => u.CustomerId == customer.CustomerId && u.Status == UnprocessedScript.PrescriptionStatus.Completed)
                    .CountAsync();

                var pendingRepeats = await _context.PrescriptionRepeats
                    .Include(pr => pr.PrescriptionLine)
                        .ThenInclude(pl => pl.Medication)
                    .Where(pr => pr.CustomerId == customer.CustomerId && pr.RemainingRepeats > 0)
                    .CountAsync();

                var readyForCollection = await _context.DispensationRequests
                    .Include(dr => dr.PrescriptionRepeat)
                        .ThenInclude(pr => pr.PrescriptionLine)
                            .ThenInclude(pl => pl.Medication)
                    .Where(dr => dr.CustomerId == customer.CustomerId && dr.Status == DispensationRequestStatus.Ready)
                    .CountAsync();

                // Debug logging to check what data exists
                _logger.LogInformation($"Debug - Customer ID: {customer.CustomerId}");
                _logger.LogInformation($"Debug - Total UnprocessedScripts for customer: {await _context.UnprocessedScripts.Where(u => u.CustomerId == customer.CustomerId).CountAsync()}");
                _logger.LogInformation($"Debug - Total PrescriptionRepeats for customer: {await _context.PrescriptionRepeats.Where(pr => pr.CustomerId == customer.CustomerId).CountAsync()}");
                _logger.LogInformation($"Debug - Total DispensationRequests for customer: {await _context.DispensationRequests.Where(dr => dr.CustomerId == customer.CustomerId).CountAsync()}");
                _logger.LogInformation($"Debug - Total Orders for customer: {await _context.Orders.Where(o => o.CustomerId == customer.CustomerId).CountAsync()}");

                var recentPrescriptions = await _context.UnprocessedScripts
                    .Include(u => u.Customer)
                    .Include(u => u.Doctor)
                    .Where(u => u.CustomerId == customer.CustomerId)
                    .OrderByDescending(u => u.UploadDate)
                    .Take(5)
                    .Select(u => new
                    {
                        PrescriptionId = u.UnploadId,
                        PrescriptionDate = u.UploadDate,
                        DoctorName = u.Doctor != null ? $"{u.Doctor.Name} {u.Doctor.Surname}" : "Unknown Doctor",
                        Status = u.Status.ToString(),
                        MedicationCount = 0 // We'll need to get this from PrescriptionLines if needed
                    })
                    .ToListAsync();

                // Get recent activity (last action performed by customer)
                var lastActivity = await GetLastCustomerActivity(customer.CustomerId);

                var customerAllergies = await _context.CustomerAllergies
                    .Include(ca => ca.ActiveIngredient)
                    .Where(ca => ca.CustomerId == customer.CustomerId)
                    .Select(ca => ca.ActiveIngredient.Name)
                    .ToListAsync();

                // Get additional data for dashboard
                var totalOrders = await _context.Orders
                    .Where(o => o.CustomerId == customer.CustomerId)
                    .CountAsync();

                var totalRepeatsAvailable = await _context.PrescriptionRepeats
                    .Where(pr => pr.CustomerId == customer.CustomerId)
                    .SumAsync(pr => pr.RemainingRepeats);

                var totalRepeatsUsed = await _context.PrescriptionRepeats
                    .Where(pr => pr.CustomerId == customer.CustomerId)
                    .SumAsync(pr => pr.DispensedCount);

                var recentRepeatRequests = await _context.DispensationRequests
                    .Include(dr => dr.PrescriptionRepeat)
                        .ThenInclude(pr => pr.PrescriptionLine)
                            .ThenInclude(pl => pl.Medication)
                    .Where(dr => dr.CustomerId == customer.CustomerId)
                    .OrderByDescending(dr => dr.RequestDate)
                    .Take(5)
                    .Select(dr => new
                    {
                        dr.DispensationRequestId,
                        dr.RequestDate,
                        dr.Status,
                        MedicationName = dr.PrescriptionRepeat.PrescriptionLine.Medication.Name,
                        RemainingRepeats = dr.PrescriptionRepeat.RemainingRepeats
                    })
                    .ToListAsync();

                var dashboardData = new
                {
                    ActivePrescriptions = activePrescriptions,
                    PendingRepeats = pendingRepeats,
                    ReadyForCollection = readyForCollection,
                    TotalOrders = totalOrders,
                    TotalRepeatsAvailable = totalRepeatsAvailable,
                    TotalRepeatsUsed = totalRepeatsUsed,
                    RecentRepeatRequests = recentRepeatRequests,
                    RecentPrescriptions = recentPrescriptions,
                    CustomerAllergies = customerAllergies,
                    CustomerName = $"{customer.Name} {customer.Surname}",
                    LastActivity = lastActivity
                };

                // Log dashboard data for debugging
                _logger.LogInformation($"Customer Dashboard Data for Customer {customer.CustomerId}: " +
                    $"ActivePrescriptions={activePrescriptions}, PendingRepeats={pendingRepeats}, " +
                    $"ReadyForCollection={readyForCollection}, TotalOrders={totalOrders}, " +
                    $"TotalRepeatsAvailable={totalRepeatsAvailable}, TotalRepeatsUsed={totalRepeatsUsed}");

                ViewBag.DashboardData = dashboardData;
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customer dashboard data");
                TempData["ErrorMessage"] = "An error occurred while loading your dashboard. Please try again.";
                return View();
            }
        }

        [Authorize(Roles = "Pharmacist")]
        public IActionResult Pharmacist()
        {
            return View();
        }

        [Authorize(Roles = "PharmacyManager")]
        public async Task<IActionResult> PharmacyManager()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var viewModel = new PharmacyManagerDashboardViewModel();
            // You would populate this view model with actual data here.
            // For now, it's just initialized to prevent NullReferenceException.
            return View(viewModel);
        }

        [HttpGet]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> GetCustomerDashboardData()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not authenticated" });
                }

                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.UserId == user.Id);

                if (customer == null)
                {
                    return Json(new { success = false, message = "Customer not found" });
                }

                // Get real-time data for dashboard
                var activePrescriptions = await _context.UnprocessedScripts
                    .Where(u => u.CustomerId == customer.CustomerId && u.Status == UnprocessedScript.PrescriptionStatus.Completed)
                    .CountAsync();

                var pendingRepeats = await _context.PrescriptionRepeats
                    .Where(pr => pr.CustomerId == customer.CustomerId && pr.RemainingRepeats > 0)
                    .CountAsync();

                var readyForCollection = await _context.DispensationRequests
                    .Where(dr => dr.CustomerId == customer.CustomerId && dr.Status == DispensationRequestStatus.Ready)
                    .CountAsync();

                var totalOrders = await _context.Orders
                    .Where(o => o.CustomerId == customer.CustomerId)
                    .CountAsync();

                // Debug logging for AJAX endpoint
                _logger.LogInformation($"AJAX Debug - Customer ID: {customer.CustomerId}");
                _logger.LogInformation($"AJAX Debug - Active Prescriptions: {activePrescriptions}");
                _logger.LogInformation($"AJAX Debug - Pending Repeats: {pendingRepeats}");
                _logger.LogInformation($"AJAX Debug - Ready for Collection: {readyForCollection}");
                _logger.LogInformation($"AJAX Debug - Total Orders: {totalOrders}");

                // Get detailed repeat information for real-time updates
                var totalRepeatsAvailable = await _context.PrescriptionRepeats
                    .Where(pr => pr.CustomerId == customer.CustomerId)
                    .SumAsync(pr => pr.RemainingRepeats);

                var totalRepeatsUsed = await _context.PrescriptionRepeats
                    .Where(pr => pr.CustomerId == customer.CustomerId)
                    .SumAsync(pr => pr.DispensedCount);

                var recentRepeatRequests = await _context.DispensationRequests
                    .Include(dr => dr.PrescriptionRepeat)
                        .ThenInclude(pr => pr.PrescriptionLine)
                            .ThenInclude(pl => pl.Medication)
                    .Where(dr => dr.CustomerId == customer.CustomerId)
                    .OrderByDescending(dr => dr.RequestDate)
                    .Take(5)
                    .Select(dr => new
                    {
                        dr.DispensationRequestId,
                        dr.RequestDate,
                        dr.Status,
                        MedicationName = dr.PrescriptionRepeat.PrescriptionLine.Medication.Name,
                        RemainingRepeats = dr.PrescriptionRepeat.RemainingRepeats
                    })
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        ActivePrescriptions = activePrescriptions,
                        PendingRepeats = pendingRepeats,
                        ReadyForCollection = readyForCollection,
                        TotalOrders = totalOrders,
                        TotalRepeatsAvailable = totalRepeatsAvailable,
                        TotalRepeatsUsed = totalRepeatsUsed,
                        RecentRepeatRequests = recentRepeatRequests,
                        LastUpdated = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting customer dashboard data");
                return Json(new { success = false, message = "An error occurred while loading dashboard data" });
            }
        }

        private async Task<object> GetLastCustomerActivity(int customerId)
        {
            try
            {
                // Get the most recent activity from various sources
                var lastPrescriptionUpload = await _context.UnprocessedScripts
                    .Where(u => u.CustomerId == customerId)
                    .OrderByDescending(u => u.UploadDate)
                    .FirstOrDefaultAsync();

                var lastOrder = await _context.Orders
                    .Where(o => o.CustomerId == customerId)
                    .OrderByDescending(o => o.OrderDate)
                    .FirstOrDefaultAsync();

                var lastRepeatRequest = await _context.DispensationRequests
                    .Where(dr => dr.CustomerId == customerId)
                    .OrderByDescending(dr => dr.RequestDate)
                    .FirstOrDefaultAsync();

                // Determine the most recent activity
                var activities = new List<(DateTime Date, string Activity, string Description)>();

                if (lastPrescriptionUpload != null)
                {
                    activities.Add((lastPrescriptionUpload.UploadDate.ToDateTime(TimeOnly.MinValue), 
                        "Uploaded Prescription", 
                        $"Prescription #{lastPrescriptionUpload.UnploadId} uploaded"));
                }

                if (lastOrder != null)
                {
                    activities.Add((lastOrder.OrderDate, 
                        "Created Order", 
                        $"Order #{lastOrder.OrderId} created"));
                }

                if (lastRepeatRequest != null)
                {
                    activities.Add((lastRepeatRequest.RequestDate, 
                        "Requested Repeat", 
                        $"Repeat request #{lastRepeatRequest.DispensationRequestId} submitted"));
                }

                if (activities.Any())
                {
                    var mostRecent = activities.OrderByDescending(a => a.Date).First();
                    return new
                    {
                        Activity = mostRecent.Activity,
                        Description = mostRecent.Description,
                        Date = mostRecent.Date,
                        HasActivity = true
                    };
                }

                return new
                {
                    Activity = "Welcome!",
                    Description = "You haven't performed any actions yet. Upload a prescription or create an order to get started.",
                    Date = DateTime.UtcNow,
                    HasActivity = false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting last customer activity");
                return new
                {
                    Activity = "Loading...",
                    Description = "Unable to load recent activity",
                    Date = DateTime.UtcNow,
                    HasActivity = false
                };
            }
        }

        [HttpGet]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> TestCustomerData()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not authenticated" });
                }

                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.UserId == user.Id);

                if (customer == null)
                {
                    return Json(new { success = false, message = "Customer not found" });
                }

                // Get all data counts for debugging
                var totalCustomers = await _context.Customers.CountAsync();
                var totalUnprocessedScripts = await _context.UnprocessedScripts.CountAsync();
                var totalPrescriptionRepeats = await _context.PrescriptionRepeats.CountAsync();
                var totalDispensationRequests = await _context.DispensationRequests.CountAsync();
                var totalOrders = await _context.Orders.CountAsync();

                var customerUnprocessedScripts = await _context.UnprocessedScripts
                    .Where(u => u.CustomerId == customer.CustomerId)
                    .ToListAsync();

                var customerPrescriptionRepeats = await _context.PrescriptionRepeats
                    .Where(pr => pr.CustomerId == customer.CustomerId)
                    .ToListAsync();

                var customerDispensationRequests = await _context.DispensationRequests
                    .Where(dr => dr.CustomerId == customer.CustomerId)
                    .ToListAsync();

                var customerOrders = await _context.Orders
                    .Where(o => o.CustomerId == customer.CustomerId)
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        CustomerId = customer.CustomerId,
                        CustomerName = $"{customer.Name} {customer.Surname}",
                        CustomerEmail = customer.Email,
                        TotalCustomers = totalCustomers,
                        TotalUnprocessedScripts = totalUnprocessedScripts,
                        TotalPrescriptionRepeats = totalPrescriptionRepeats,
                        TotalDispensationRequests = totalDispensationRequests,
                        TotalOrders = totalOrders,
                        CustomerUnprocessedScripts = customerUnprocessedScripts.Select(u => new { u.UnploadId, u.Status, u.CustomerId }),
                        CustomerPrescriptionRepeats = customerPrescriptionRepeats.Select(pr => new { pr.PrescriptionRepeatId, pr.RemainingRepeats, pr.CustomerId }),
                        CustomerDispensationRequests = customerDispensationRequests.Select(dr => new { dr.DispensationRequestId, dr.Status, dr.CustomerId }),
                        CustomerOrders = customerOrders.Select(o => new { o.OrderId, o.OrderStatus, o.CustomerId })
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing customer data");
                return Json(new { success = false, message = "An error occurred while testing data" });
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
