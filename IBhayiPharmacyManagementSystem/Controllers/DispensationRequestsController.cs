using IBhayiPharmacyManagementSystem.Data;
using IBhayiPharmacyManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IBhayiPharmacyManagementSystem.Controllers
{
    public class DispensationRequestsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly ILogger<DispensationRequestsController> _logger;

        public DispensationRequestsController(
            AppDbContext context, 
            UserManager<Users> userManager,
            ILogger<DispensationRequestsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: DispensationRequests - Show all pending requests for pharmacists
        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var dispensationRequests = await _context.DispensationRequests
                .Include(dr => dr.PrescriptionRepeat)
                    .ThenInclude(pr => pr.PrescriptionLine)
                        .ThenInclude(pl => pl.Medication)
                .Include(dr => dr.PrescriptionRepeat)
                    .ThenInclude(pr => pr.PrescriptionLine)
                        .ThenInclude(pl => pl.Prescription)
                            .ThenInclude(p => p.Customer)
                .Include(dr => dr.PrescriptionRepeat)
                    .ThenInclude(pr => pr.PrescriptionLine)
                        .ThenInclude(pl => pl.Prescription)
                            .ThenInclude(p => p.Doctor)
                .Include(dr => dr.Customer)
                .Where(dr => dr.Status == DispensationRequestStatus.Pending)
                .OrderByDescending(dr => dr.RequestDate)
                .ToListAsync();

            return View(dispensationRequests);
        }

        // GET: DispensationRequests/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var dispensationRequest = await _context.DispensationRequests
                .Include(dr => dr.PrescriptionRepeat)
                    .ThenInclude(pr => pr.PrescriptionLine)
                        .ThenInclude(pl => pl.Medication)
                .Include(dr => dr.PrescriptionRepeat)
                    .ThenInclude(pr => pr.PrescriptionLine)
                        .ThenInclude(pl => pl.Prescription)
                            .ThenInclude(p => p.Customer)
                .Include(dr => dr.PrescriptionRepeat)
                    .ThenInclude(pr => pr.PrescriptionLine)
                        .ThenInclude(pl => pl.Prescription)
                            .ThenInclude(p => p.Doctor)
                .Include(dr => dr.Customer)
                .FirstOrDefaultAsync(dr => dr.DispensationRequestId == id);

            if (dispensationRequest == null) return NotFound();

            return View(dispensationRequest);
        }

        // POST: DispensationRequests/Process/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Process(int id, DispensationRequestStatus status, string? processingNotes)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Invalid request data provided.";
                return RedirectToAction(nameof(Index));
            }

            var dispensationRequest = await _context.DispensationRequests
                .Include(dr => dr.PrescriptionRepeat)
                    .ThenInclude(pr => pr.PrescriptionLine)
                        .ThenInclude(pl => pl.Medication)
                .Include(dr => dr.Customer)
                .FirstOrDefaultAsync(dr => dr.DispensationRequestId == id);

            if (dispensationRequest == null) return NotFound();

            try
            {
                // Update request status
                dispensationRequest.Status = status;
                dispensationRequest.ProcessedDate = DateTime.UtcNow;
                // Assign the ID of the currently logged-in pharmacist
                var pharmacist = await _context.Pharmacists.FirstOrDefaultAsync(p => p.UserId == currentUser.Id);
                if (pharmacist == null)
                {
                    _logger.LogError("Pharmacist record not found for user {UserId}", currentUser.Id);
                    TempData["ErrorMessage"] = "Pharmacist record not found. Cannot process request.";
                    return RedirectToAction(nameof(Index));
                }
                dispensationRequest.ProcessedByPharmacistId = pharmacist.PharmacistId;
                
                if (!string.IsNullOrEmpty(processingNotes))
                {
                    dispensationRequest.Notes = processingNotes;
                }

                // If status is Ready or Dispensed, create a DispensedPrescription record
                if (status == DispensationRequestStatus.Ready || status == DispensationRequestStatus.Dispensed)
                {
                    var dispensedPrescription = new DispensedPrescription
                    {
                        PrescriptionLineId = dispensationRequest.PrescriptionRepeat?.PrescriptionLineId ?? 0,
                        // Assign the ID of the currently logged-in pharmacist
                        PharmacistId = pharmacist.PharmacistId,
                        DispensedDate = DateTime.UtcNow,
                        QuantityDispensed = dispensationRequest.PrescriptionRepeat?.QuantityPerRepeat ?? 0,
                        AmountDue = dispensationRequest.PrescriptionRepeat != null ? CalculateAmountDue(dispensationRequest.PrescriptionRepeat) : 0,
                        IsPaid = false,
                        DispensingNotes = processingNotes ?? "Dispensed via repeat request",
                        PatientInstructions = dispensationRequest.PrescriptionRepeat.PrescriptionLine?.Instructions ?? ""
                    };

                    _context.DispensedPrescriptions.Add(dispensedPrescription);
                }

                await _context.SaveChangesAsync();

                // Add notification for customer dashboard
                if (status == DispensationRequestStatus.Ready || status == DispensationRequestStatus.Dispensed)
                {
                    var customerId = dispensationRequest.CustomerId;
                    var medicationName = dispensationRequest.PrescriptionRepeat?.PrescriptionLine?.Medication?.Name ?? "your medication";
                    var notificationMessage = $"Gr-8 Your repeat request for {medicationName} is now {status.ToString().ToLower()}.";
                    await AddCustomerNotification(customerId, notificationMessage, "DispensationRequest", id.ToString());
                }

                TempData["SuccessMessage"] = $"Dispensation request {status.ToString().ToLower()} successfully.";
                _logger.LogInformation($"User {currentUser.Email} {status.ToString().ToLower()} dispensation request {id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing dispensation request {id}");
                TempData["ErrorMessage"] = "An error occurred while processing the request. Please try again.";
            }

            return RedirectToAction(nameof(Index));
        }

        private decimal CalculateAmountDue(PrescriptionRepeat prescriptionRepeat)
        {
            // Calculate amount based on medication price and quantity
            var medication = prescriptionRepeat.PrescriptionLine?.Medication;
            if (medication?.Price != null)
            {
                return (decimal)(medication.Price * prescriptionRepeat.QuantityPerRepeat);
            }
            return 0;
        }

        private async Task AddCustomerNotification(int customerId, string message, string relatedEntityType, string relatedEntityId)
        {
            var notification = new Notification
            {
                CustomerId = customerId,
                Message = message,
                DateSent = DateTime.UtcNow,
                IsRead = false,
                RelatedEntityType = relatedEntityType,
                RelatedEntityId = relatedEntityId
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }
    }
}
