using IBhayiPharmacyManagementSystem.Data;
using IBhayiPharmacyManagementSystem.Models;
using IBhayiPharmacyManagementSystem.Services;
using IBhayiPharmacyManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace IBhayiPharmacyManagementSystem.Controllers
{
    [Authorize(Roles = "Customer,Pharmacist")]
    public class PrescriptionRepeatsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly ILogger<PrescriptionRepeatsController> _logger;
        private readonly ICustomerActivityService _activityService;

        public PrescriptionRepeatsController(
            AppDbContext context,
            UserManager<Users> userManager,
            ILogger<PrescriptionRepeatsController> logger,
            ICustomerActivityService activityService)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _activityService = activityService;
        }

        // GET: PrescriptionRepeats - Show prescription repeats
        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var prescriptionRepeats = new List<PrescriptionRepeat>();

            if (User.IsInRole("Customer"))
            {
                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);
                if (customer == null) return Forbid();

                prescriptionRepeats = await _context.PrescriptionRepeats
                    .Include(pr => pr.PrescriptionLine)
                        .ThenInclude(pl => pl.Prescription)
                            .ThenInclude(p => p.Doctor)
                    .Include(pr => pr.PrescriptionLine)
                        .ThenInclude(pl => pl.Medication)
                    .Include(pr => pr.PrescriptionLine)
                        .ThenInclude(pl => pl.Prescription)
                            .ThenInclude(p => p.Customer)
                    .Where(pr => pr.PrescriptionLine != null && 
                                pr.PrescriptionLine.Prescription != null && 
                                pr.PrescriptionLine.Prescription.CustomerId == customer.CustomerId)
                    .Where(pr => pr.IsActive && pr.RemainingRepeats > 0)
                    .OrderByDescending(pr => pr.PrescriptionLine!.Prescription!.PrescriptionDate)
                    .ToListAsync();
            }
            else if (User.IsInRole("Pharmacist"))
            {
                // Pharmacists can see all prescription repeats
                prescriptionRepeats = await _context.PrescriptionRepeats
                    .Include(pr => pr.PrescriptionLine)
                        .ThenInclude(pl => pl.Prescription)
                            .ThenInclude(p => p.Doctor)
                    .Include(pr => pr.PrescriptionLine)
                        .ThenInclude(pl => pl.Medication)
                    .Include(pr => pr.PrescriptionLine)
                        .ThenInclude(pl => pl.Prescription)
                            .ThenInclude(p => p.Customer)
                    .Where(pr => pr.IsActive && pr.RemainingRepeats > 0)
                    .OrderByDescending(pr => pr.PrescriptionLine!.Prescription!.PrescriptionDate)
                    .ToListAsync();
            }

            _logger.LogInformation($"PrescriptionRepeats/Index returning {prescriptionRepeats.Count} prescription repeats for user: {currentUser.Email}");
            return View(prescriptionRepeats);
        }

        // GET: PrescriptionRepeats/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) 
            {
                _logger.LogWarning("PrescriptionRepeats/Details called without ID parameter");
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) 
            {
                _logger.LogWarning("PrescriptionRepeats/Details called without authenticated user");
                return Challenge();
            }

            _logger.LogInformation($"PrescriptionRepeats/Details called with ID: {id} by user: {currentUser.Email}");

            var prescriptionRepeat = await _context.PrescriptionRepeats
                .Include(pr => pr.PrescriptionLine)
                    .ThenInclude(pl => pl.Prescription)
                        .ThenInclude(p => p.Doctor)
                .Include(pr => pr.PrescriptionLine)
                    .ThenInclude(pl => pl.Medication)
                .Include(pr => pr.PrescriptionLine)
                    .ThenInclude(pl => pl.Prescription)
                        .ThenInclude(p => p.Customer)
                // .Include(pr => pr.PrescriptionLine)
                //     .ThenInclude(pl => pl.DispensedPrescriptions)
                .FirstOrDefaultAsync(pr => pr.PrescriptionRepeatId == id);

            if (prescriptionRepeat == null) return NotFound();

            // Role-based access control
            if (User.IsInRole("Customer"))
            {
                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);
                if (customer == null) return Forbid();

                // Verify customer owns this prescription
                if (prescriptionRepeat.PrescriptionLine?.Prescription?.CustomerId != customer.CustomerId)
                    return Forbid();
            }
            // Pharmacists can access any prescription repeat

            return View(prescriptionRepeat);
        }

        // POST: PrescriptionRepeats/RequestDispensation/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestDispensation(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);
            if (customer == null) return Forbid();

            var prescriptionRepeat = await _context.PrescriptionRepeats
                .Include(pr => pr.PrescriptionLine)
                    .ThenInclude(pl => pl.Prescription)
                .FirstOrDefaultAsync(pr => pr.PrescriptionRepeatId == id);

            if (prescriptionRepeat == null) return NotFound();

            // Verify customer owns this prescription
            if (prescriptionRepeat.PrescriptionLine?.Prescription?.CustomerId != customer.CustomerId)
                return Forbid();

            // Check if repeats are available
            if (prescriptionRepeat.RemainingRepeats <= 0)
            {
                TempData["ErrorMessage"] = "No repeats remaining for this prescription.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // Decrement remaining repeats immediately
                prescriptionRepeat.RemainingRepeats--;
                prescriptionRepeat.DispensedCount++;
                prescriptionRepeat.LastDispensedDate = DateTime.UtcNow;

                // Also update the PrescriptionLine's RepeatsRemaining to keep it in sync
                if (prescriptionRepeat.PrescriptionLine != null)
                {
                    prescriptionRepeat.PrescriptionLine.RepeatsRemaining = prescriptionRepeat.RemainingRepeats;
                }

                // Create dispensation request
                var dispensationRequest = new DispensationRequest
                {
                    PrescriptionRepeatId = prescriptionRepeat.PrescriptionRepeatId,
                    CustomerId = customer.CustomerId,
                    RequestDate = DateTime.UtcNow,
                    Status = DispensationRequestStatus.Pending,
                    Notes = $"Repeat request for {prescriptionRepeat.PrescriptionLine?.Medication?.Name ?? "Unknown Medication"}"
                };

                _context.DispensationRequests.Add(dispensationRequest);
                await _context.SaveChangesAsync();

                // Log activity
                var medicationName = prescriptionRepeat.PrescriptionLine?.Medication?.Name ?? "Unknown Medication";
                await _activityService.LogActivityAsync(
                    customer.CustomerId,
                    "RepeatRequested",
                    $"Requested repeat dispensation for {medicationName}",
                    "PrescriptionRepeat",
                    id,
                    $"Remaining repeats: {prescriptionRepeat.RemainingRepeats}"
                );

                // Send email notification
                var emailService = HttpContext.RequestServices.GetRequiredService<Services.IEmailService>();
                await emailService.SendRepeatRequestNotificationAsync(
                    customer.Email,
                    customer.FullName,
                    medicationName
                );

                TempData["SuccessMessage"] = $"Dispensation request submitted successfully. {prescriptionRepeat.RemainingRepeats} repeats remaining.";
                _logger.LogInformation($"Customer {customer.CustomerId} requested dispensation for prescription repeat {id}. Remaining repeats: {prescriptionRepeat.RemainingRepeats}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error requesting dispensation for prescription repeat {id}");
                TempData["ErrorMessage"] = "An error occurred while submitting your request. Please try again.";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: PrescriptionRepeats/History - Show prescription repeats history
        public async Task<IActionResult> History()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);
            if (customer == null) return Forbid();

            // Get all prescription repeats for the customer (both active and inactive)
            var prescriptionRepeats = await _context.PrescriptionRepeats
                .Include(pr => pr.PrescriptionLine)
                    .ThenInclude(pl => pl.Prescription)
                        .ThenInclude(p => p.Doctor)
                .Include(pr => pr.PrescriptionLine)
                    .ThenInclude(pl => pl.Medication)
                        .ThenInclude(m => m.DosageForm)
                .Include(pr => pr.PrescriptionLine)
                    .ThenInclude(pl => pl.Prescription)
                        .ThenInclude(p => p.Customer)
                .Where(pr => pr.PrescriptionLine != null && 
                            pr.PrescriptionLine.Prescription != null && 
                            pr.PrescriptionLine.Prescription.CustomerId == customer.CustomerId)
                .OrderByDescending(pr => pr.PrescriptionLine.Prescription.PrescriptionDate)
                .ToListAsync();

            // Get dispensed prescriptions for additional context
            var dispensedPrescriptions = await _context.DispensedPrescriptions
                .Include(dp => dp.PrescriptionLine)
                    .ThenInclude(pl => pl.Prescription)
                        .ThenInclude(p => p.Doctor)
                .Include(dp => dp.PrescriptionLine)
                    .ThenInclude(pl => pl.Medication)
                .Include(dp => dp.Pharmacist)
                .Where(dp => dp.PrescriptionLine != null && 
                            dp.PrescriptionLine.Prescription != null && 
                            dp.PrescriptionLine.Prescription.CustomerId == customer.CustomerId)
                .OrderByDescending(dp => dp.DispensedDate)
                .ToListAsync();

            // Create a combined view model for prescription repeats history
            var historyViewModel = new List<PrescriptionRepeatHistoryViewModel>();

            foreach (var repeat in prescriptionRepeats)
            {
                var dispensedCount = dispensedPrescriptions
                    .Count(dp => dp.PrescriptionLineId == repeat.PrescriptionLineId);

                historyViewModel.Add(new PrescriptionRepeatHistoryViewModel
                {
                    PrescriptionRepeatId = repeat.PrescriptionRepeatId,
                    MedicationName = repeat.PrescriptionLine.Medication.Name,
                    DosageForm = repeat.PrescriptionLine.Medication.DosageForm?.Type ?? "N/A",
                    Instructions = repeat.PrescriptionLine.Instructions,
                    Frequency = repeat.PrescriptionLine.Frequency.ToString(),
                    DoctorName = repeat.PrescriptionLine.Prescription.Doctor?.FullName ?? "N/A",
                    PrescriptionDate = repeat.PrescriptionLine.Prescription.PrescriptionDate,
                    TotalRepeats = repeat.TotalRepeats,
                    RemainingRepeats = repeat.RemainingRepeats,
                    DispensedCount = dispensedCount,
                    LastDispensedDate = repeat.LastDispensedDate,
                    IsActive = repeat.IsActive,
                    Status = repeat.IsActive ? "Active" : "Inactive",
                    Type = "Prescription Repeat"
                });
            }

            return View(historyViewModel);
        }

        // POST: PrescriptionRepeats/DeleteHistory/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteHistory(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);
            if (customer == null) return Forbid();

            var prescriptionRepeat = await _context.PrescriptionRepeats
                .Include(pr => pr.PrescriptionLine)
                    .ThenInclude(pl => pl.Prescription)
                .FirstOrDefaultAsync(pr => pr.PrescriptionRepeatId == id);

            if (prescriptionRepeat == null) return NotFound();

            // Verify customer owns this prescription
            if (prescriptionRepeat.PrescriptionLine?.Prescription?.CustomerId != customer.CustomerId)
                return Forbid();

            try
            {
                // Soft delete by marking as inactive instead of hard delete
                prescriptionRepeat.IsActive = false;
                prescriptionRepeat.RemainingRepeats = 0; // Set remaining to 0
                
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Customer {customer.CustomerId} deleted history for prescription repeat {id}");
                return Json(new { success = true, message = "History entry deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting history for prescription repeat {id}");
                return Json(new { success = false, message = "An error occurred while deleting the history entry." });
            }
        }
    }
}
