using Microsoft.AspNetCore.Mvc;
using IBhayiPharmacyManagementSystem.Models;
using IBhayiPharmacyManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using IBhayiPharmacyManagementSystem.Data;
using System;
using Microsoft.Extensions.Logging;

namespace IBhayiPharmacyManagementSystem.Controllers
{
    public class PharmacyManagerController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PharmacyManagerController> _logger;

        public PharmacyManagerController(AppDbContext context, ILogger<PharmacyManagerController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Dashboard(string timeframe = "This Week")
        {
            DateTime currentPeriodStartDate = GetStartDate(timeframe);
            DateTime previousPeriodStartDate = GetPreviousPeriodStartDate(timeframe);

            // Current Period Data
            var currentPeriodStockOrders = _context.StockOrders.Where(o => o.StockOrderDate >= currentPeriodStartDate);

            // Previous Period Data
            var previousPeriodStockOrders = _context.StockOrders.Where(o => o.StockOrderDate >= previousPeriodStartDate && o.StockOrderDate < currentPeriodStartDate);

            var viewModel = new PharmacyManagerDashboardViewModel
            {
                SelectedTimeframe = timeframe,
                TotalMedications = await _context.Medications.CountAsync(),
                LowStockItems = await _context.Medications.CountAsync(m => m.QuantityInStock <= m.MinStockLevel),
                PendingOrders = await currentPeriodStockOrders.CountAsync(o => o.StockOrderStatus == IBhayiPharmacyManagementSystem.Enums.StockOrderStatusEnum.Pending),
                ActivePharmacists = await _context.Pharmacists.CountAsync(p => p.IsActive),
                OrdersCount = await currentPeriodStockOrders.CountAsync(),
                InventoryValue = await _context.Medications.SumAsync(m => (decimal)m.Price * m.QuantityInStock),
                TotalItems = await _context.Medications.CountAsync(),
                
                StockOverview = await _context.Medications
                    .Include(m => m.DosageForm)
                    .GroupBy(m => m.DosageForm.Type)
                    .Select(g => new StockOverviewItemViewModel
                    {
                        DosageForm = g.Key,
                        ItemCount = g.Sum(m => m.QuantityInStock)
                    })
                    .ToListAsync(),

                NotificationsCount = await _context.NotificationP.Where(n => n.Timestamp >= currentPeriodStartDate).CountAsync(),
                Notifications = await _context.NotificationP
                    .Where(n => n.Timestamp >= currentPeriodStartDate)
                    .OrderByDescending(n => n.Timestamp)
                    .Take(5)
                    .Select(n => new NotificationViewModel
                    {
                        Title = n.Title, 
                        Message = n.Message, 
                        Timestamp = n.Timestamp, 
                        Type = (IBhayiPharmacyManagementSystem.Enums.NotificationType)n.Type, 
                        IsRead = n.IsRead, 
                        Link = n.Link 
                    }).ToListAsync(),
                
                RecentActivities = await _context.StockMovements
                    .Include(sm => sm.Medication)
                    .OrderByDescending(sm => sm.Timestamp)
                    .Take(5)
                    .Select(sm => new RecentActivityItemViewModel
                    {
                        Title = $"Stock {sm.MovementType} for {sm.Medication.Name}",
                        Description = $"{sm.QuantityChanged} units",
                        Timestamp = sm.Timestamp,
                        IconClass = sm.MovementType == "Received" || sm.MovementType == "Increment" ? "bi-arrow-up-circle-fill text-success" : "bi-arrow-down-circle-fill text-danger",
                        BackgroundClass = sm.MovementType == "Received" || sm.MovementType == "Increment" ? "bg-success-subtle" : "bg-danger-subtle",
                        Link = "#"
                    }).ToListAsync()
            };

            // Calculate Changes (for TotalItems, LowStockItems, OrdersCount, InventoryValue)
            int previousTotalMedications = await _context.Medications.CountAsync(); // Assuming total medications is not date-filtered for change
            int previousLowStockItems = await _context.Medications.CountAsync(m => m.QuantityInStock <= m.MinStockLevel); // Assuming low stock is not date-filtered for change
            int previousOrdersCount = await previousPeriodStockOrders.CountAsync();
            decimal previousInventoryValue = await _context.Medications.SumAsync(m => (decimal)m.Price * m.QuantityInStock); // Assuming inventory value is not date-filtered for change

            viewModel.TotalItemsChange = CalculateChangePercentage(previousTotalMedications, viewModel.TotalMedications);
            viewModel.LowStockChange = CalculateChangePercentage(previousLowStockItems, viewModel.LowStockItems, invert: true); // Invert for low stock
            viewModel.OrdersChange = CalculateChangePercentage(previousOrdersCount, viewModel.OrdersCount);
            viewModel.InventoryValueChange = CalculateChangePercentage(previousInventoryValue, viewModel.InventoryValue);
            
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(viewModel);
            }

            return View(viewModel);
        }

        private DateTime GetStartDate(string timeframe)
        {
            return timeframe switch
            {
                "Today" => DateTime.UtcNow.Date,
                "This Week" => DateTime.UtcNow.AddDays(-(int)DateTime.UtcNow.DayOfWeek),
                "Last Week" => DateTime.UtcNow.AddDays(-(int)DateTime.UtcNow.DayOfWeek - 7),
                "This Month" => new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1),
                "Last 3 Months" => DateTime.UtcNow.AddMonths(-3),
                "This Year" => new DateTime(DateTime.UtcNow.Year, 1, 1),
                _ => DateTime.UtcNow.AddDays(-(int)DateTime.UtcNow.DayOfWeek), // Default to This Week
            };
        }

        private DateTime GetPreviousPeriodStartDate(string timeframe)
        {
            DateTime currentStart = GetStartDate(timeframe);
            return timeframe switch
            {
                "Today" => currentStart.AddDays(-1),
                "This Week" => currentStart.AddDays(-7),
                "Last Week" => currentStart.AddDays(-7),
                "This Month" => currentStart.AddMonths(-1),
                "Last 3 Months" => currentStart.AddMonths(-3),
                "This Year" => currentStart.AddYears(-1),
                _ => currentStart.AddDays(-7), // Default to Last Week
            };
        }

        private string CalculateChangePercentage<T>(T previousValue, T currentValue, bool invert = false) where T : IComparable
        {
            if (Equals(previousValue, default(T)) && Equals(currentValue, default(T)))
            {
                return "0%";
            }

            dynamic prev = previousValue;
            dynamic curr = currentValue;

            if (prev == 0)
            {
                return curr > 0 ? (invert ? "-100%" : "+100%") : "0%";
            }

            decimal change = ((decimal)curr - (decimal)prev) / (decimal)prev * 100;

            if (invert)
            {
                change *= -1;
            }

            return $"{change:+0.##;-0.##;0}%";
        }

        [HttpGet]
        public async Task<IActionResult> ScanBarcode(int medicationId)
        {
            if (medicationId <= 0)
            {
                return Json(new { success = false, message = "Medication ID cannot be empty or invalid." });
            }

            var medication = await _context.Medications
                .Include(m => m.DosageForm)
                .Include(m => m.Supplier)
                .Include(m => m.ActiveIngredients)
                    .ThenInclude(mi => mi.ActiveIngredient)
                .FirstOrDefaultAsync(m => m.MedicationId == medicationId); // Searching by MedicationId

            if (medication == null)
            {
                return Json(new { success = false, message = "Medication not found for this ID." });
            }

            var medicationDetails = new
            {
                success = true,
                medication = new
                {
                    medication.Name,
                    medication.Description,
                    DosageFormDescription = medication.DosageForm?.Type ?? "N/A",
                    Strength = medication.ActiveIngredients != null && medication.ActiveIngredients.Any() 
                        ? string.Join(", ", medication.ActiveIngredients.Select(ai => $"{ai.ActiveIngredient.Name} {ai.Strength}")) 
                        : "N/A",
                    medication.Price,
                    medication.QuantityInStock,
                    medication.MinStockLevel,
                    Supplier = medication.Supplier?.Name ?? "N/A",
                    // Barcode = barcode // Removed Barcode from response
                }
            };

            return Json(medicationDetails);
        }

        [HttpGet]
        public IActionResult ScanQr()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ResolveQr(string code)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(code))
                {
                    return Json(new { success = false, message = "No QR payload provided." });
                }

                // Normalize payload
                var payload = code.Trim();

                _logger.LogInformation("Resolving QR code with payload: {Payload}", payload);

                // Strategy 1: Numeric payload -> MedicationId
                if (int.TryParse(payload, out var medicationId) && medicationId > 0)
                {
                    var medication = await _context.Medications
                        .Include(x => x.DosageForm)
                        .Include(x => x.Supplier)
                        .Include(x => x.ActiveIngredients)!.ThenInclude(ai => ai.ActiveIngredient)
                        .FirstOrDefaultAsync(x => x.MedicationId == medicationId);

                    if (medication != null)
                    {
                        var detailsUrl = Url.Action("EditMedication", "Medications", new { id = medication.MedicationId });
                        return Json(new
                        {
                            success = true,
                            type = "Medication",
                            medication = new
                            {
                                medication.MedicationId,
                                medication.Name,
                                medication.Description,
                                DosageFormDescription = medication.DosageForm?.Type ?? "N/A",
                                Strength = medication.ActiveIngredients != null && medication.ActiveIngredients.Any()
                                    ? string.Join(", ", medication.ActiveIngredients.Select(ai => $"{ai.ActiveIngredient.Name} {ai.Strength}"))
                                    : "N/A",
                                medication.Price,
                                medication.QuantityInStock,
                                medication.MinStockLevel,
                                Supplier = medication.Supplier?.Name ?? "N/A",
                                detailsUrl
                            }
                        });
                    }
                    else
                    {
                        return Json(new { success = false, message = $"No medication found with ID: {medicationId}" });
                    }
                }

                // Strategy 2: Text payload -> search by Name (exact match first, then contains)
                var query = _context.Medications
                    .Include(x => x.DosageForm)
                    .Include(x => x.Supplier)
                    .Include(x => x.ActiveIngredients)!.ThenInclude(ai => ai.ActiveIngredient)
                    .AsQueryable();

                // First try exact match (case-insensitive)
                var exactMatch = await query
                    .Where(x => x.Name.ToLower() == payload.ToLower())
                    .FirstOrDefaultAsync();

                if (exactMatch != null)
                {
                    var detailsUrl = Url.Action("EditMedication", "Medications", new { id = exactMatch.MedicationId });
                    return Json(new
                    {
                        success = true,
                        type = "Medication",
                        medication = new
                        {
                            exactMatch.MedicationId,
                            exactMatch.Name,
                            exactMatch.Description,
                            DosageFormDescription = exactMatch.DosageForm?.Type ?? "N/A",
                            Strength = exactMatch.ActiveIngredients != null && exactMatch.ActiveIngredients.Any()
                                ? string.Join(", ", exactMatch.ActiveIngredients.Select(ai => $"{ai.ActiveIngredient.Name} {ai.Strength}"))
                                : "N/A",
                            exactMatch.Price,
                            exactMatch.QuantityInStock,
                            exactMatch.MinStockLevel,
                            Supplier = exactMatch.Supplier?.Name ?? "N/A",
                            detailsUrl
                        }
                    });
                }

                // Then try contains match (case-insensitive)
                var matches = await query
                    .Where(x => x.Name.ToLower().Contains(payload.ToLower()))
                    .OrderBy(x => x.Name)
                    .Take(10)
                    .Select(m => new
                    {
                        m.MedicationId,
                        m.Name,
                        m.Description,
                        DosageFormDescription = m.DosageForm != null ? m.DosageForm.Type : "N/A",
                        Strength = m.ActiveIngredients != null && m.ActiveIngredients.Any()
                            ? string.Join(", ", m.ActiveIngredients.Select(ai => ai.ActiveIngredient.Name + " " + ai.Strength))
                            : "N/A",
                        m.Price,
                        m.QuantityInStock,
                        m.MinStockLevel,
                        Supplier = m.Supplier != null ? m.Supplier.Name : "N/A",
                        detailsUrl = Url.Action("EditMedication", "Medications", new { id = m.MedicationId })
                    })
                    .ToListAsync();

                if (matches.Count > 0)
                {
                    return Json(new { success = true, type = "Medication", matches });
                }

                // Strategy 3: Try searching by description (case-insensitive)
                var descriptionMatches = await query
                    .Where(x => x.Description != null && x.Description.ToLower().Contains(payload.ToLower()))
                    .OrderBy(x => x.Name)
                    .Take(5)
                    .Select(m => new
                    {
                        m.MedicationId,
                        m.Name,
                        m.Description,
                        DosageFormDescription = m.DosageForm != null ? m.DosageForm.Type : "N/A",
                        Strength = m.ActiveIngredients != null && m.ActiveIngredients.Any()
                            ? string.Join(", ", m.ActiveIngredients.Select(ai => ai.ActiveIngredient.Name + " " + ai.Strength))
                            : "N/A",
                        m.Price,
                        m.QuantityInStock,
                        m.MinStockLevel,
                        Supplier = m.Supplier != null ? m.Supplier.Name : "N/A",
                        detailsUrl = Url.Action("EditMedication", "Medications", new { id = m.MedicationId })
                    })
                    .ToListAsync();

                if (descriptionMatches.Count > 0)
                {
                    return Json(new { success = true, type = "Medication", matches = descriptionMatches });
                }

                return Json(new { success = false, message = $"No medication found matching '{payload}'. Try searching by medication name or ID." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving QR code with payload: {Payload}", code);
                return Json(new { success = false, message = "An error occurred while resolving the QR code. Please try again." });
            }
        }
    }
}

