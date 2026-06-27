using IBhayiPharmacyManagementSystem.Data;
using IBhayiPharmacyManagementSystem.Models;
using IBhayiPharmacyManagementSystem.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IBhayiPharmacyManagementSystem.Controllers
{
    [Authorize(Roles = "Customer")]
    public class MedicationHistoryController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;

        public MedicationHistoryController(AppDbContext context, UserManager<Users> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Get customer
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

            if (customer == null)
            {
                TempData["Error"] = "Customer profile not found.";
                return RedirectToAction("Index", "Home");
            }

            // Get all medication history for this customer
            var medicationHistory = await GetMedicationHistoryAsync(customer.CustomerId);

            return View(medicationHistory);
        }

        private async Task<List<MedicationHistoryViewModel>> GetMedicationHistoryAsync(int customerId)
        {
            var history = new List<MedicationHistoryViewModel>();

            // Resolve a fallback pharmacist display name for cases where prescription has no pharmacist assigned (e.g., processed but not dispensed)
            var fallbackPharmacistName = await _context.Pharmacists
                .Where(p => p.IsActive)
                .Select(p => p.Name + " " + p.Surname)
                .FirstOrDefaultAsync();

            // Get prescriptions and their lines
            var prescriptions = await _context.Prescriptions
                .Include(p => p.PrescriptionLines)
                    .ThenInclude(pl => pl.Medication)
                        .ThenInclude(m => m.DosageForm)
                .Include(p => p.Doctor)
                .Include(p => p.Pharmacist)
                .Where(p => p.CustomerId == customerId)
                .ToListAsync();

            foreach (var prescription in prescriptions)
            {
                foreach (var line in prescription.PrescriptionLines)
                {
                    history.Add(new MedicationHistoryViewModel
                    {
                        MedicationId = line.MedicationId,
                        MedicationName = line.Medication.Name,
                        DosageForm = line.Medication.DosageForm.Type,
                        Quantity = line.Quantity,
                        Instructions = line.Instructions,
                        Frequency = line.Frequency.ToString(),
                        DoctorName = prescription.Doctor != null ? $"{prescription.Doctor.Name} {prescription.Doctor.Surname}" : "Unknown",
                        PharmacistName = prescription.Pharmacist != null ? $"{prescription.Pharmacist.Name} {prescription.Pharmacist.Surname}" : fallbackPharmacistName,
                        Date = prescription.PrescriptionDate,
                        Status = "Prescribed",
                        Type = "Prescription",
                        AmountPaid = (decimal)((line.Medication.Price) * line.Quantity)
                    });
                }
            }

            // Get dispensed prescriptions
            var dispensedPrescriptions = await _context.DispensedPrescriptions
                .Include(dp => dp.PrescriptionLine)
                    .ThenInclude(pl => pl.Medication)
                        .ThenInclude(m => m.DosageForm)
                .Include(dp => dp.PrescriptionLine)
                    .ThenInclude(pl => pl.Prescription)
                        .ThenInclude(p => p.Customer)
                .Include(dp => dp.PrescriptionLine)
                    .ThenInclude(pl => pl.Prescription)
                        .ThenInclude(p => p.Doctor)
                .Include(dp => dp.PrescriptionLine)
                    .ThenInclude(pl => pl.Prescription)
                        .ThenInclude(p => p.Pharmacist)
                .Include(dp => dp.Pharmacist)
                .Where(dp => dp.PrescriptionLine.Prescription.CustomerId == customerId)
                .ToListAsync();

            foreach (var dispensed in dispensedPrescriptions)
            {
                history.Add(new MedicationHistoryViewModel
                {
                    MedicationId = dispensed.PrescriptionLine.MedicationId,
                    MedicationName = dispensed.PrescriptionLine.Medication.Name,
                    DosageForm = dispensed.PrescriptionLine.Medication.DosageForm.Type,
                    Quantity = dispensed.QuantityDispensed,
                    Instructions = dispensed.PatientInstructions ?? dispensed.PrescriptionLine.Instructions,
                    Frequency = dispensed.PrescriptionLine.Frequency.ToString(),
                    DoctorName = dispensed.PrescriptionLine.Prescription.Doctor != null ? $"{dispensed.PrescriptionLine.Prescription.Doctor.Name} {dispensed.PrescriptionLine.Prescription.Doctor.Surname}" : "Unknown Doctor",
                    PharmacistName = dispensed.Pharmacist != null ? $"{dispensed.Pharmacist.Name} {dispensed.Pharmacist.Surname}" : null,
                    Date = dispensed.DispensedDate,
                    Status = dispensed.IsPaid ? "Dispensed & Paid" : "Dispensed",
                    Type = "Dispensed",
                    AmountPaid = (decimal)((dispensed.PrescriptionLine.Medication.Price) * dispensed.QuantityDispensed)
                });
            }

            // Get order items
            var orderItems = await _context.OrderItems
                .Include(oi => oi.Order)
                    .ThenInclude(o => o.Customer)
                .Include(oi => oi.Medication)
                    .ThenInclude(m => m.DosageForm)
                .Include(oi => oi.Order)
                    .ThenInclude(o => o.Pharmacist)
                .Where(oi => oi.Order.CustomerId == customerId)
                .ToListAsync();

            foreach (var item in orderItems)
            {
                var status = item.DispensingStatus switch
                {
                    DispensingStatusEnum.Pending => "Ordered",
                    DispensingStatusEnum.Filled => "Fully Dispensed",
                    DispensingStatusEnum.OutOfStock => "Out of Stock",
                    _ => "Unknown"
                };

                // Get dispensed pharmacist if item was dispensed
                string? dispensedPharmacistName = null;
                if (item.DispensedBy.HasValue)
                {
                    var dispensedPharmacist = await _context.Pharmacists
                        .FirstOrDefaultAsync(p => p.PharmacistId == item.DispensedBy.Value);
                    if (dispensedPharmacist != null)
                    {
                        dispensedPharmacistName = $"{dispensedPharmacist.Name} {dispensedPharmacist.Surname}";
                    }
                }

                // Use dispensed pharmacist, otherwise use order's assigned pharmacist
                var pharmacistName = !string.IsNullOrEmpty(dispensedPharmacistName) 
                    ? dispensedPharmacistName 
                    : (item.Order.Pharmacist != null ? $"{item.Order.Pharmacist.Name} {item.Order.Pharmacist.Surname}" : "N/A");

                history.Add(new MedicationHistoryViewModel
                {
                    MedicationId = item.MedicationId,
                    MedicationName = item.Medication.Name,
                    DosageForm = item.Medication.DosageForm.Type,
                    Quantity = item.QuantityOrdered,
                    Instructions = !string.IsNullOrEmpty(item.DispensingNotes) ? item.DispensingNotes : "As per order",
                    Frequency = "As needed",
                    DoctorName = "Order", // Orders don't have doctors
                    PharmacistName = pharmacistName,
                    Date = item.DispensedDate ?? item.Order.OrderDate,
                    Status = status,
                    Type = "Order",
                    AmountPaid = (decimal)(item.UnitPrice * item.QuantityOrdered)
                });
            }

            // Get prescription repeats
            var prescriptionRepeats = await _context.PrescriptionRepeats
                .Include(pr => pr.PrescriptionLine)
                    .ThenInclude(pl => pl.Medication)
                        .ThenInclude(m => m.DosageForm)
                .Include(pr => pr.PrescriptionLine)
                    .ThenInclude(pl => pl.Prescription)
                        .ThenInclude(p => p.Doctor)
                .Include(pr => pr.PrescriptionLine)
                    .ThenInclude(pl => pl.Prescription)
                        .ThenInclude(p => p.Pharmacist)
                .Include(pr => pr.Customer)
                .Where(pr => pr.CustomerId == customerId)
                .ToListAsync();

            foreach (var repeat in prescriptionRepeats)
            {
                history.Add(new MedicationHistoryViewModel
                {
                    MedicationId = repeat.PrescriptionLine.MedicationId,
                    MedicationName = repeat.PrescriptionLine.Medication.Name,
                    DosageForm = repeat.PrescriptionLine.Medication.DosageForm.Type,
                    Quantity = repeat.QuantityPerRepeat,
                    Instructions = repeat.PrescriptionLine.Instructions,
                    Frequency = repeat.PrescriptionLine.Frequency.ToString(),
                    DoctorName = repeat.PrescriptionLine.Prescription.Doctor != null ? $"{repeat.PrescriptionLine.Prescription.Doctor.Name} {repeat.PrescriptionLine.Prescription.Doctor.Surname}" : "Unknown Doctor",
                    PharmacistName = repeat.PrescriptionLine.Prescription.Pharmacist != null ? $"{repeat.PrescriptionLine.Prescription.Pharmacist.Name} {repeat.PrescriptionLine.Prescription.Pharmacist.Surname}" : null,
                    Date = repeat.DateCreated,
                    Status = repeat.IsActive ? "Active Repeat" : "Inactive Repeat",
                    Type = "Repeat",
                    TotalRepeats = repeat.TotalRepeats,
                    RemainingRepeats = repeat.RemainingRepeats
                });
            }

            // Sort by date descending
            return history.OrderByDescending(h => h.Date).ToList();
        }
    }

    public class MedicationHistoryViewModel
    {
        public int MedicationId { get; set; }
        public string MedicationName { get; set; } = string.Empty;
        public string DosageForm { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Instructions { get; set; } = string.Empty;
        public string Frequency { get; set; } = string.Empty;
        public string DoctorName { get; set; } = string.Empty;
        public string? PharmacistName { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public decimal? AmountPaid { get; set; }
        public int? TotalRepeats { get; set; }
        public int? RemainingRepeats { get; set; }
    }
}
