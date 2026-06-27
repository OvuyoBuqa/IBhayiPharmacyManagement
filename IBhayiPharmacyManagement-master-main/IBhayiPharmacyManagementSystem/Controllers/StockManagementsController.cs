using IBhayiPharmacyManagementSystem.Data;
using IBhayiPharmacyManagementSystem.Models;
using IBhayiPharmacyManagementSystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using IBhayiPharmacyManagementSystem.Views.StockManagements;

namespace IBhayiPharmacyManagementSystem.Controllers
{
    public class StockManagementsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<StockManagementsController> _logger;

        public StockManagementsController(AppDbContext context, ILogger<StockManagementsController> logger)
        {
            _context = context;
            _logger = logger;
        }
        public IActionResult Index()
        {
            var medications = _context.Medications.ToList();

            // Removed the redirect. The view will now display "No inventory data found." message.
            // if (medications == null || !medications.Any())
            // {
            //     return RedirectToAction("Create", "StockOrders");
            // }

            return View(medications);
        }
        public async Task<IActionResult> LowStock()
        {
            var lowStockItems = await _context.Medications
                .Include(m => m.Supplier)
                .Include(m => m.ActiveIngredients)
                .ThenInclude(mi => mi.ActiveIngredient)
                .Where(m => m.QuantityInStock <= (m.MinStockLevel + 10))
                .ToListAsync();

            return View(lowStockItems);
        }

        // GET: StockManagements/AdjustStock/5
        public async Task<IActionResult> AdjustStock(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var medication = await _context.Medications.FindAsync(id);
            if (medication == null)
            {
                return NotFound();
            }

            var viewModel = new AdjustStockViewModel
            {
                MedicationId = medication.MedicationId,
                MedicationName = medication.Name,
                CurrentQuantity = medication.QuantityInStock,
                AdjustmentQuantity = 0, // Default to 0
                AdjustmentType = "Increment" // Default to increment
            };

            return View(viewModel);
        }

        // POST: StockManagements/AdjustStock
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdjustStock(AdjustStockViewModel model)
        {
            var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            if (!ModelState.IsValid)
            {
                if (isAjax)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    return Json(new { success = false, errors });
                }
                return View(model);
            }

            var medication = await _context.Medications.FindAsync(model.MedicationId);
            if (medication == null)
            {
                if (isAjax)
                {
                    return Json(new { success = false, errors = new[] { "Medication not found." } });
                }
                ModelState.AddModelError(string.Empty, "Medication not found.");
                return View(model);
            }

            if (model.AdjustmentType == "Increment")
            {
                medication.QuantityInStock += model.AdjustmentQuantity;
            }
            else if (model.AdjustmentType == "Set")
            {
                medication.QuantityInStock = model.AdjustmentQuantity;
            }
            else if (model.AdjustmentType == "Decrement") // Explicitly handle decrement if it's an allowed type
            {
                if (medication.QuantityInStock - model.AdjustmentQuantity < 0)
                {
                    ModelState.AddModelError(string.Empty, "Quantity cannot go below zero.");
                    if (isAjax)
                    {
                        var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                        return Json(new { success = false, errors });
                    }
                    return View(model);
                }
                medication.QuantityInStock -= model.AdjustmentQuantity;
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Invalid adjustment type specified. Please choose 'Increment', 'Set', or 'Decrement'.");
                if (isAjax)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    return Json(new { success = false, errors });
                }
                return View(model);
            }

            // Ensure quantity doesn't go below zero (redundant if Decrement is handled, but good as a fail-safe)
            medication.QuantityInStock = Math.Max(0, medication.QuantityInStock);

            _context.Update(medication);

            var stockMovement = new StockMovement
            {
                MedicationId = medication.MedicationId,
                MovementType = model.AdjustmentType,
                QuantityChanged = model.AdjustmentQuantity,
                Timestamp = DateTime.UtcNow,
                Reason = string.IsNullOrWhiteSpace(model.Reason) ? "Manual adjustment by Pharmacy Manager" : model.Reason!.Trim(),
                UserId = User.FindFirstValue(ClaimTypes.NameIdentifier) // Get the ID of the current logged-in user
            };

            _context.StockMovements.Add(stockMovement);
            await _context.SaveChangesAsync();

            var successMessage = $"Stock for {medication.Name} adjusted successfully. New quantity: {medication.QuantityInStock}.";

            if (isAjax)
            {
                return Json(new { success = true, message = successMessage, newQuantity = medication.QuantityInStock });
            }

            TempData["SuccessMessage"] = successMessage;
            return RedirectToAction(nameof(Index));
        }

        // GET: StockManagements/StockMovementHistory
        public async Task<IActionResult> StockMovementHistory()
        {
            var stockMovements = await _context.StockMovements
                                                .Include(sm => sm.Medication)
                                                .Include(sm => sm.User)
                                                .OrderByDescending(sm => sm.Timestamp)
                                                .ToListAsync();
            return View(stockMovements);
        }

        // GET: StockManagements/GenerateStockReport
        public async Task<IActionResult> GenerateStockReport(string groupBy = "None")
        {
            var medications = await _context.Medications
                                    .Include(m => m.DosageForm)
                                    .Include(m => m.Supplier)
                                    .ToListAsync();

            // Ensure the groupBy parameter is valid based on the PharmacyReportGenerator's switch case
            string actualGroupBy = groupBy.ToLower();
            if (actualGroupBy != "dosageform" && actualGroupBy != "schedule" && actualGroupBy != "supplier")
            {
                actualGroupBy = "none"; // Default to no specific grouping or handle as needed
            }
            _logger.LogInformation("Generating stock report grouped by: {GroupBy}", actualGroupBy);
            var generator = new PharmacyReportGenerator();
            byte[] pdfBytes = generator.GenerateReport(medications, actualGroupBy);

            return File(pdfBytes, "application/pdf", $"StockReport_{actualGroupBy}_{DateTime.Now:yyyyMMddHHmmss}.pdf");
        }

        // GET: StockManagements/StockTake
        public IActionResult StockTake()
        {
            _logger.LogInformation("Accessed StockTake (GET) action to prepare for report generation.");
            var medications = _context.Medications.OrderBy(m => m.Name).ToList();
            var viewModel = new StockTakeViewModel { Medications = medications };
            return View(viewModel);
        }

        // POST: StockManagements/ProcessStockTake
        // This action will receive the counted quantities after a physical stock take
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessStockTake(List<StockTakeItemViewModel> items)
        {
            if (!ModelState.IsValid || !items.Any())
            {
                _logger.LogWarning("ProcessStockTake (POST) called with invalid model state or no items.");
                TempData["ErrorMessage"] = "Invalid data provided for stock take. Please ensure all fields are correctly filled.";
                return RedirectToAction(nameof(StockTake));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); // Get current user ID
            var timestamp = DateTime.UtcNow;
            var stockMovements = new List<StockMovement>();

            foreach (var item in items)
            {
                var medication = await _context.Medications.FindAsync(item.MedicationId);
                if (medication == null)
                {
                    _logger.LogWarning("Medication with ID {MedicationId} not found during stock take processing.", item.MedicationId);
                    continue; // Skip if medication not found
                }

                if (medication.QuantityInStock != item.CountedQuantity)
                {
                    var oldQuantity = medication.QuantityInStock;
                    medication.QuantityInStock = item.CountedQuantity;
                    _context.Update(medication);

                    stockMovements.Add(new StockMovement
                    {
                        MedicationId = medication.MedicationId,
                        MovementType = "StockTakeAdjustment",
                        QuantityChanged = item.CountedQuantity - oldQuantity, // Can be positive or negative
                        Timestamp = timestamp,
                        Reason = "Stock Take Adjustment",
                        UserId = userId
                    });
                }
            }

            if (stockMovements.Any())
            {
                _context.StockMovements.AddRange(stockMovements);
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Stock take processed successfully! Stock levels updated.";
            _logger.LogInformation("Stock take processed successfully by user {UserId}. {NumMovements} stock movements recorded.", userId, stockMovements.Count);
            return RedirectToAction(nameof(Index)); // Redirect to the main stock management page
        }
    }
}
