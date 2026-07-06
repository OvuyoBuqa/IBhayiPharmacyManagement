using IBhayiPharmacyManagementSystem.Data;
using IBhayiPharmacyManagementSystem.Enums;
using IBhayiPharmacyManagementSystem.Models;
using IBhayiPharmacyManagementSystem.Utilities.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IBhayiPharmacyManagementSystem.ViewModels;

namespace IBhayiPharmacyManagementSystem.Controllers
{
    public class PharmacistsController : Controller
    {
        private readonly SignInManager<Users> _signInManager;
        private readonly UserManager<Users> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _context;
        private readonly ILogger<PharmacistsController> _logger;

        public PharmacistsController(SignInManager<Users> signInManager,
            UserManager<Users> userManager,
            RoleManager<IdentityRole> roleManager, AppDbContext context,
            ILogger<PharmacistsController> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _logger = logger;
        }

        // GET: Pharmacists
        public async Task<IActionResult> Index()
        {
            var appDbContext = _context.Pharmacists.Include(p => p.User);
            _logger.LogInformation("Accessed Pharmacists Index page.");
            return View(await appDbContext.ToListAsync());
        }

        // GET: Pharmacists/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                _logger.LogWarning("Details (GET) for Pharmacist called with null ID.");
                return NotFound();
            }

            var pharmacist = await _context.Pharmacists
                .Include(p => p.User)
                .FirstOrDefaultAsync(m => m.PharmacistId == id);
            if (pharmacist == null)
            {
                _logger.LogWarning("Pharmacist with ID {PharmacistId} not found for Details (GET).", id);
                return NotFound();
            }
            _logger.LogInformation("Accessed Pharmacist Details (GET) for pharmacist (ID: {PharmacistId}).", id);
            return View(pharmacist);
        }

        // GET: Pharmacists/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                _logger.LogWarning("Edit (GET) for Pharmacist called with null ID.");
                return NotFound();
            }

            var pharmacist = await _context.Pharmacists.Include(p => p.User).FirstOrDefaultAsync(p => p.PharmacistId == id);
            if (pharmacist == null)
            {
                _logger.LogWarning("Pharmacist with ID {PharmacistId} not found for Edit (GET).", id);
                return NotFound();
            }

            // Populate ViewModel for Edit
            var viewModel = new EditPharmacistViewModel
            {
                PharmacistId = pharmacist.PharmacistId,
                UserId = pharmacist.UserId,
                Name = pharmacist.Name,
                Surname = pharmacist.Surname,
                IDNumber = pharmacist.IDNumber,
                CellPhoneNumber = pharmacist.CellPhone,
                RegistrationNumber = pharmacist.RegistrationNumber,
                Email = pharmacist.User.Email, // Assuming email is editable via the User record
                FullName = pharmacist.User.FullName // Assuming full name is editable via the User record
            };

            _logger.LogInformation("Accessed Edit (GET) for pharmacist (ID: {PharmacistId}).", id);
            return View(viewModel);
        }

        // POST: Pharmacists/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditPharmacistViewModel model)
        {
            if (id != model.PharmacistId)
            {
                _logger.LogWarning("Edit (POST) for Pharmacist: ID mismatch (route ID: {RouteId}, model ID: {ModelId}).", id, model.PharmacistId);
                return NotFound();
            }

            // Check if ID number already exists across all entities (excluding current pharmacist)
            bool idExistsInCustomers = await _context.Customers.AnyAsync(c => c.IDNumber == model.IDNumber);
            bool idExistsInPharmacists = await _context.Pharmacists.AnyAsync(p => p.IDNumber == model.IDNumber && p.PharmacistId != id);
            
            if (idExistsInCustomers || idExistsInPharmacists)
            {
                ModelState.AddModelError("IDNumber", "This ID number is already registered. Please use a unique ID number.");
                _logger.LogWarning("Pharmacist edit (POST) failed - duplicate ID number: {IDNumber}", model.IDNumber);
                return View(model);
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var pharmacistToUpdate = await _context.Pharmacists.Include(p => p.User).FirstOrDefaultAsync(p => p.PharmacistId == id);
                    if (pharmacistToUpdate == null)
                    {
                        _logger.LogWarning("Pharmacist with ID {PharmacistId} not found for Edit (POST).", id);
                        return NotFound();
                    }

                    // Update Pharmacist specific details
                    pharmacistToUpdate.Name = model.Name;
                    pharmacistToUpdate.Surname = model.Surname;
                    pharmacistToUpdate.IDNumber = model.IDNumber;
                    pharmacistToUpdate.CellPhone = model.CellPhoneNumber;
                    pharmacistToUpdate.RegistrationNumber = model.RegistrationNumber;

                    // Update associated User details
                    var user = await _userManager.FindByIdAsync(pharmacistToUpdate.UserId);
                    if (user != null)
                    {
                        user.Email = model.Email;
                        user.UserName = model.Email; // Keep UserName and Email consistent
                        user.FullName = model.FullName;
                        await _userManager.UpdateAsync(user);
                    }

                    _context.Update(pharmacistToUpdate);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Pharmacist details updated successfully!";
                    _logger.LogInformation("Pharmacist '{FullName}' (ID: {PharmacistId}) details updated.", model.FullName, model.PharmacistId);
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    if (!PharmacistExists(model.PharmacistId))
                    {
                        _logger.LogWarning("Concurrency error updating non-existent pharmacist (ID: {PharmacistId}).", model.PharmacistId);
                        return NotFound();
                    }
                    else
                    {
                        _logger.LogError(ex, "Concurrency error updating pharmacist (ID: {PharmacistId}).", model.PharmacistId);
                        throw; // Re-throw the exception to let global error handler deal with it
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error updating pharmacist (ID: {PharmacistId}).", model.PharmacistId);
                    ModelState.AddModelError(string.Empty, "An unexpected error occurred while updating the pharmacist: " + ex.Message);
                }
                return RedirectToAction(nameof(Index));
            }
            _logger.LogWarning("Model state invalid for Pharmacist Edit (POST) for pharmacist (ID: {PharmacistId}).", id);
            // If ModelState is not valid, return to view with validation errors
            return View(model);
        }

        // GET: Pharmacists/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                _logger.LogWarning("Delete (GET) for Pharmacist called with null ID.");
                return NotFound();
            }

            var pharmacist = await _context.Pharmacists
                .Include(p => p.User)
                .FirstOrDefaultAsync(m => m.PharmacistId == id);
            if (pharmacist == null)
            {
                _logger.LogWarning("Pharmacist with ID {PharmacistId} not found for Delete (GET).", id);
                return NotFound();
            }

            // Check for linked orders or dispensation requests for referential integrity
            var hasOrders = await _context.Orders.AnyAsync(o => o.PharmacistId == id);
            var hasDispensationRequests = await _context.DispensationRequests.AnyAsync(dr => dr.ProcessedByPharmacistId == id);

            if (hasOrders || hasDispensationRequests)
            {
                TempData["ErrorMessage"] = "Cannot delete pharmacist because they are linked to existing orders or dispensation requests.";
                _logger.LogWarning("Attempted to delete pharmacist (ID: {PharmacistId}) linked to orders or dispensation requests.", id);
                ViewBag.CanDelete = false;
            }
            else
            {
                ViewBag.CanDelete = true;
            }

            _logger.LogInformation("Accessed Delete (GET) for pharmacist (ID: {PharmacistId}).", id);
            return View(pharmacist);
        }

        // POST: Pharmacists/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var pharmacist = await _context.Pharmacists.Include(p => p.User).FirstOrDefaultAsync(p => p.PharmacistId == id);
                if (pharmacist == null)
                {
                    TempData["ErrorMessage"] = "Pharmacist not found.";
                    _logger.LogWarning("Attempted to confirm deletion of non-existent pharmacist (ID: {PharmacistId}).", id);
                    return RedirectToAction(nameof(Index));
                }

                // Check for linked orders or dispensation requests again (double-check)
                var hasOrders = await _context.Orders.AnyAsync(o => o.PharmacistId == id);
                var hasDispensationRequests = await _context.DispensationRequests.AnyAsync(dr => dr.ProcessedByPharmacistId == id);

                if (hasOrders || hasDispensationRequests)
                {
                    TempData["ErrorMessage"] = "Cannot delete pharmacist because they are linked to existing orders or dispensation requests.";
                    _logger.LogWarning("Attempted to delete pharmacist (ID: {PharmacistId}) linked to orders or dispensation requests during confirmation.", id);
                    return RedirectToAction(nameof(Delete), new { id = id }); // Redirect back to the GET Delete view with error
                }

                // Delete the associated user account first
                var user = await _userManager.FindByIdAsync(pharmacist.UserId);
                if (user != null)
                {
                    var deleteUserResult = await _userManager.DeleteAsync(user);
                    if (!deleteUserResult.Succeeded)
                    {
                        _logger.LogError("Failed to delete user account {UserId} for pharmacist {PharmacistId}. Errors: {Errors}", user.Id, id, string.Join("; ", deleteUserResult.Errors.Select(e => e.Description)));
                        TempData["ErrorMessage"] = "Failed to delete associated user account. Please try again.";
                        return RedirectToAction(nameof(Delete), new { id = id });
                    }
                }

                _context.Pharmacists.Remove(pharmacist);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Pharmacist deleted successfully!";
                _logger.LogInformation("Pharmacist (ID: {PharmacistId}) and associated user account deleted successfully.", id);
            }
            catch (DbUpdateException ex)
            {
                TempData["ErrorMessage"] = "A database error occurred while deleting the pharmacist. It might be referenced by other records.";
                _logger.LogError(ex, "Database error deleting pharmacist (ID: {PharmacistId}).", id);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An unexpected error occurred while deleting the pharmacist.";
                _logger.LogError(ex, "Unexpected error deleting pharmacist (ID: {PharmacistId}).", id);
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Pharmacists/Profile
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var pharmacist = await _context.Pharmacists
                                    .Include(p => p.User)
                                    .FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (pharmacist == null)
            {
                return NotFound($"No pharmacist found for user with ID '{_userManager.GetUserId(User)}'.");
            }

            return View(pharmacist);
        }

        // POST: Pharmacists/UpdatePersonalDetails
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePersonalDetails(string FullName, string PhoneNumber)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Unable to load user." });
                }
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var pharmacist = await _context.Pharmacists
                                    .Include(p => p.User)
                                    .FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (pharmacist == null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "No pharmacist found for user." });
                }
                return NotFound($"No pharmacist found for user with ID '{_userManager.GetUserId(User)}'.");
            }

            // Basic validation
            if (string.IsNullOrWhiteSpace(FullName))
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Full name is required." });
                }
                ModelState.AddModelError("FullName", "Full name is required.");
            }

            if (!ModelState.IsValid)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    var errors = ModelState.Where(x => x.Value.Errors.Count > 0)
                        .Select(x => new { field = x.Key, errors = x.Value.Errors.Select(e => e.ErrorMessage) })
                        .ToDictionary(x => x.field, x => x.errors);
                    return Json(new { success = false, message = "Validation failed.", errors = errors });
                }
                return View("Profile", pharmacist);
            }

            try
            {
                // Update ASP.NET Identity user fields
                user.FullName = FullName.Trim();
                user.PhoneNumber = string.IsNullOrWhiteSpace(PhoneNumber) ? null : PhoneNumber.Trim();
                var userUpdateResult = await _userManager.UpdateAsync(user);
                
                if (!userUpdateResult.Succeeded)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, message = "Failed to update user details." });
                    }
                    foreach (var error in userUpdateResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    return View("Profile", pharmacist);
                }

                // Best-effort sync of Pharmacist first/last name from FullName (non-breaking)
                var parts = (FullName ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0) pharmacist.Name = parts.First();
                if (parts.Length > 1) pharmacist.Surname = string.Join(' ', parts.Skip(1));
                pharmacist.CellPhone = string.IsNullOrWhiteSpace(PhoneNumber) ? pharmacist.CellPhone : PhoneNumber.Trim();
                _context.Update(pharmacist);
                await _context.SaveChangesAsync();

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = "Personal details updated successfully!" });
                }

                TempData["SuccessMessage"] = "Personal details updated.";
                return RedirectToAction(nameof(Profile));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating pharmacist personal details for user {UserId}", user.Id);
                
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "An error occurred while saving your details. Please try again." });
                }
                
                ModelState.AddModelError(string.Empty, "An error occurred while saving your details.");
                return View("Profile", pharmacist);
            }
        }

        // POST: Pharmacists/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string OldPassword, string NewPassword, string ConfirmNewPassword)
        {
            if (string.IsNullOrWhiteSpace(OldPassword)) ModelState.AddModelError("OldPassword", "Current password is required.");
            if (string.IsNullOrWhiteSpace(NewPassword)) ModelState.AddModelError("NewPassword", "New password is required.");
            if (NewPassword != ConfirmNewPassword) ModelState.AddModelError("ConfirmNewPassword", "Password does not match.");

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            // Always load pharmacist model for view returns
            var pharmacist = await _context.Pharmacists
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == currentUser.Id);

            if (!ModelState.IsValid)
            {
                return View("Profile", pharmacist);
            }

            var changePasswordResult = await _userManager.ChangePasswordAsync(currentUser, OldPassword, NewPassword);
            if (!changePasswordResult.Succeeded)
            {
                foreach (var error in changePasswordResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View("Profile", pharmacist);
            }

            await _signInManager.RefreshSignInAsync(currentUser);
            TempData["SuccessMessage"] = "Password changed successfully.";
            return RedirectToAction(nameof(Profile));
        }

        // GET: Pharmacists/Reports - Show report options
        [Authorize(Roles = "Pharmacist, PharmacyManager")]
        public IActionResult Reports()
        {
            // Add logging to debug authorization
            _logger.LogInformation("Reports action accessed by user: {User}, Roles: {Roles}", 
                User.Identity?.Name, 
                string.Join(", ", User.Claims.Where(c => c.Type == System.Security.Claims.ClaimTypes.Role).Select(c => c.Value)));
            return View();
        }

        // GET: Pharmacists/GenerateReport - Generate prescriptions report
        [Authorize(Roles = "Pharmacist, PharmacyManager")]
        public async Task<IActionResult> GenerateReport(DateTime? startDate, DateTime? endDate, string groupBy = "patient")
        {
            // Check if user is authenticated first
            if (!User.Identity.IsAuthenticated)
            {
                _logger.LogWarning("GenerateReport accessed by unauthenticated user. Redirecting to login.");
                return Challenge();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) 
            {
                _logger.LogWarning("GenerateReport: User not found after authentication check. Redirecting to login.");
                return Challenge();
            }

            // Check if user is a Pharmacist or PharmacyManager
            var isPharmacist = await _userManager.IsInRoleAsync(currentUser, "Pharmacist");
            var isPharmacyManager = await _userManager.IsInRoleAsync(currentUser, "PharmacyManager");

            _logger.LogInformation("GenerateReport: User {UserEmail} - Pharmacist: {IsPharmacist}, PharmacyManager: {IsPharmacyManager}", 
                currentUser.Email, isPharmacist, isPharmacyManager);

            if (!isPharmacist && !isPharmacyManager)
            {
                _logger.LogWarning("GenerateReport: User {UserEmail} does not have required roles. Access denied.", currentUser.Email);
                return Forbid();
            }

            // Set default date range if not provided
            if (!startDate.HasValue)
                startDate = DateTime.Today.AddMonths(-1);
            if (!endDate.HasValue)
                endDate = DateTime.Today;

            List<DispensedPrescription> dispensedPrescriptions;
            string reportName;

            if (isPharmacist)
            {
                // If user is a Pharmacist, show only their prescriptions
                var pharmacist = await _context.Pharmacists
                    .FirstOrDefaultAsync(p => p.UserId == currentUser.Id);
                if (pharmacist == null) return Forbid();

                // Get dispensed prescriptions from prescription lines
                dispensedPrescriptions = await _context.DispensedPrescriptions
                    .Include(dp => dp.PrescriptionLine)
                        .ThenInclude(pl => pl.Prescription)
                            .ThenInclude(p => p.Customer)
                    .Include(dp => dp.PrescriptionLine)
                        .ThenInclude(pl => pl.Medication)
                    .Where(dp => dp.PharmacistId == pharmacist.PharmacistId)
                    .Where(dp => dp.DispensedDate >= startDate.Value && dp.DispensedDate <= endDate.Value.AddDays(1))
                    .OrderBy(dp => dp.DispensedDate)
                    .ToListAsync();

                // Also include COMPLETED prescriptions (from processing) that don't have DispensedPrescription records
                var dispensedPrescriptionLineIdsForPharmacist = dispensedPrescriptions
                    .Where(dp => dp.PrescriptionLineId > 0)
                    .Select(dp => dp.PrescriptionLineId)
                    .ToHashSet();

                var completedScriptsForPharmacist = await _context.UnprocessedScripts
                    .Include(u => u.Prescription)
                        .ThenInclude(p => p.PrescriptionLines)
                            .ThenInclude(pl => pl.Medication)
                    .Include(u => u.Customer)
                    .Where(u => u.Status == UnprocessedScript.PrescriptionStatus.Completed)
                    .Where(u => u.ProcessedDate >= startDate.Value && u.ProcessedDate <= endDate.Value.AddDays(1))
                    .Where(u => u.ProcessedById == currentUser.Id) // completed by this pharmacist
                    .ToListAsync();

                var completedConverted = completedScriptsForPharmacist
                    .SelectMany(u => (u.Prescription?.PrescriptionLines ?? new List<PrescriptionLine>())
                        .Where(pl => !dispensedPrescriptionLineIdsForPharmacist.Contains(pl.PrescriptionLineId))
                        .Select(pl => new DispensedPrescription
                        {
                            DispensedPrescriptionId = pl.PrescriptionLineId + 3000000,
                            PrescriptionLineId = pl.PrescriptionLineId,
                            PharmacistId = pharmacist.PharmacistId,
                            DispensedDate = u.ProcessedDate ?? u.Prescription?.PrescriptionDate ?? DateTime.MinValue,
                            QuantityDispensed = pl.Quantity,
                            AmountDue = (decimal)((pl.Medication?.Price ?? 0) * pl.Quantity),
                            IsPaid = false,
                            DispensingNotes = u.ProcessingNotes ?? "Completed prescription",
                            PatientInstructions = pl.Instructions ?? string.Empty,
                            PrescriptionLine = pl,
                            Pharmacist = pharmacist
                        }))
                    .ToList();

                dispensedPrescriptions.AddRange(completedConverted);

                // Include IMPORTED prescriptions (no UploadId) that appear under Completed view, de-duplicated
                var importedPrescriptionsPharmacist = await _context.Prescriptions
                    .Include(p => p.Customer)
                    .Include(p => p.PrescriptionLines)
                        .ThenInclude(pl => pl.Medication)
                    .Where(p => p.UploadId == null)
                    .Where(p => p.PrescriptionDate >= startDate.Value && p.PrescriptionDate <= endDate.Value.AddDays(1))
                    .ToListAsync();

                var importedConvertedPharmacist = importedPrescriptionsPharmacist
                    .SelectMany(p => p.PrescriptionLines
                        .Where(pl => !dispensedPrescriptionLineIdsForPharmacist.Contains(pl.PrescriptionLineId))
                        .Select(pl => new DispensedPrescription
                        {
                            DispensedPrescriptionId = pl.PrescriptionLineId + 4000000,
                            PrescriptionLineId = pl.PrescriptionLineId,
                            PharmacistId = pharmacist.PharmacistId,
                            DispensedDate = p.PrescriptionDate,
                            QuantityDispensed = pl.Quantity,
                            AmountDue = (decimal)((pl.Medication?.Price ?? 0) * pl.Quantity),
                            IsPaid = false,
                            DispensingNotes = "Imported prescription",
                            PatientInstructions = pl.Instructions ?? string.Empty,
                            PrescriptionLine = pl,
                            Pharmacist = pharmacist
                        }))
                    .ToList();

                dispensedPrescriptions.AddRange(importedConvertedPharmacist);

                // Also get walk-in customer prescriptions that don't have DispensedPrescription records
                var walkInPrescriptions = await _context.Prescriptions
                    .Include(p => p.Customer)
                    .Include(p => p.PrescriptionLines)
                        .ThenInclude(pl => pl.Medication)
                    .Where(p => p.Customer.IsWalkInCustomer == true)
                    .Where(p => p.PrescriptionDate >= startDate.Value && p.PrescriptionDate <= endDate.Value.AddDays(1))
                    .OrderBy(p => p.PrescriptionDate)
                    .ToListAsync();

                // Get all prescription line IDs that have been dispensed by this pharmacist
                var dispensedPrescriptionLineIds = await _context.DispensedPrescriptions
                    .Where(dp => dp.PharmacistId == pharmacist.PharmacistId)
                    .Select(dp => dp.PrescriptionLineId)
                    .ToListAsync();

                // Convert walk-in prescriptions to DispensedPrescription-like objects
                // Only include prescription lines that haven't been dispensed by this pharmacist
                var convertedWalkInPrescriptions = walkInPrescriptions
                    .SelectMany(p => p.PrescriptionLines
                        .Where(pl => !dispensedPrescriptionLineIds.Contains(pl.PrescriptionLineId))
                        .Select(pl => new DispensedPrescription
                        {
                            DispensedPrescriptionId = pl.PrescriptionLineId + 2000000, // Offset to avoid ID conflicts
                            PrescriptionLineId = pl.PrescriptionLineId,
                            PharmacistId = pharmacist.PharmacistId,
                            DispensedDate = p.PrescriptionDate,
                            QuantityDispensed = pl.Quantity,
                            AmountDue = 0, // Walk-in prescriptions may not have pricing
                            IsPaid = false,
                            DispensingNotes = "Walk-in customer prescription",
                            PatientInstructions = pl.Instructions ?? "",
                            PrescriptionLine = pl,
                            Pharmacist = pharmacist
                        }))
                    .ToList();

                // Add walk-in prescriptions to the main list
                dispensedPrescriptions.AddRange(convertedWalkInPrescriptions);

                // Also get dispensed medications from orders
                var dispensedOrderItems = await _context.OrderItems
                    .Include(oi => oi.Order)
                        .ThenInclude(o => o.Customer)
                    .Include(oi => oi.Medication)
                    .Where(oi => oi.DispensedBy == pharmacist.PharmacistId)
                    .Where(oi => oi.DispensedDate >= startDate.Value && oi.DispensedDate <= endDate.Value.AddDays(1))
                    .Where(oi => oi.DispensingStatus == DispensingStatusEnum.Filled)
                    .Where(oi => oi.QuantityDispensed > 0)
                    .OrderBy(oi => oi.DispensedDate)
                    .ToListAsync();

                // Convert OrderItems to DispensedPrescription-like objects for consistent reporting
                var convertedOrderItems = dispensedOrderItems.Select(oi => new DispensedPrescription
                {
                    DispensedPrescriptionId = oi.OrderItemId + 1000000, // Offset to avoid ID conflicts
                    PrescriptionLineId = 0, // No prescription line for orders
                    PharmacistId = oi.DispensedBy ?? 0,
                    DispensedDate = oi.DispensedDate ?? DateTime.MinValue,
                    QuantityDispensed = oi.QuantityDispensed,
                    AmountDue = (decimal)(oi.UnitPrice * oi.QuantityDispensed),
                    IsPaid = oi.Order.PaymentStatus,
                    PaymentDate = oi.Order.PaymentStatus ? oi.DispensedDate : null,
                    DispensingNotes = oi.DispensingNotes,
                    PatientInstructions = oi.DispensingNotes ?? "No specific instructions provided", // Use dispensing notes as patient instructions
                    // Create a virtual prescription line for orders
                    PrescriptionLine = new PrescriptionLine
                    {
                        PrescriptionLineId = 0,
                        Medication = oi.Medication,
                        Prescription = new Prescription
                        {
                            CustomerId = oi.Order.CustomerId,
                            DoctorId = null, // Orders don't have doctors
                            Customer = oi.Order.Customer
                        }
                    },
                    Pharmacist = pharmacist // Set the pharmacist for orders
                }).ToList();

                // Combine both sources
                dispensedPrescriptions.AddRange(convertedOrderItems);
                dispensedPrescriptions = dispensedPrescriptions.OrderBy(dp => dp.DispensedDate).ToList();

                reportName = $"PharmacistReport_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf";
            }
            else
            {
                // If user is a PharmacyManager, show all prescriptions
                dispensedPrescriptions = await _context.DispensedPrescriptions
                    .Include(dp => dp.PrescriptionLine)
                        .ThenInclude(pl => pl.Prescription)
                            .ThenInclude(p => p.Customer)
                    .Include(dp => dp.PrescriptionLine)
                        .ThenInclude(pl => pl.Medication)
                    .Include(dp => dp.Pharmacist)
                    .Where(dp => dp.DispensedDate >= startDate.Value && dp.DispensedDate <= endDate.Value.AddDays(1))
                    .OrderBy(dp => dp.DispensedDate)
                    .ToListAsync();

                // Also include COMPLETED prescriptions (from processing) that don't have DispensedPrescription records
                var dispensedPrescriptionLineIdsAll = dispensedPrescriptions
                    .Where(dp => dp.PrescriptionLineId > 0)
                    .Select(dp => dp.PrescriptionLineId)
                    .ToHashSet();

                var completedScriptsAll = await _context.UnprocessedScripts
                    .Include(u => u.Prescription)
                        .ThenInclude(p => p.PrescriptionLines)
                            .ThenInclude(pl => pl.Medication)
                    .Include(u => u.Customer)
                    .Where(u => u.Status == UnprocessedScript.PrescriptionStatus.Completed)
                    .Where(u => u.ProcessedDate >= startDate.Value && u.ProcessedDate <= endDate.Value.AddDays(1))
                    .ToListAsync();

                // Map Users (ProcessedById) to Pharmacists for attribution where possible
                var processedByUserIds = completedScriptsAll
                    .Select(u => u.ProcessedById)
                    .Where(id => id != null)
                    .Distinct()
                    .ToList();
                var userIdToPharmacist = await _context.Pharmacists
                    .Where(p => processedByUserIds.Contains(p.UserId))
                    .ToDictionaryAsync(p => p.UserId, p => p);

                var completedConvertedAll = completedScriptsAll
                    .SelectMany(u => (u.Prescription?.PrescriptionLines ?? new List<PrescriptionLine>())
                        .Where(pl => !dispensedPrescriptionLineIdsAll.Contains(pl.PrescriptionLineId))
                        .Select(pl => new DispensedPrescription
                        {
                            DispensedPrescriptionId = pl.PrescriptionLineId + 3000000,
                            PrescriptionLineId = pl.PrescriptionLineId,
                            PharmacistId = userIdToPharmacist.TryGetValue(u.ProcessedById ?? string.Empty, out var ph) ? ph.PharmacistId : 0,
                            DispensedDate = u.ProcessedDate ?? u.Prescription?.PrescriptionDate ?? DateTime.MinValue,
                            QuantityDispensed = pl.Quantity,
                            AmountDue = (decimal)((pl.Medication?.Price ?? 0) * pl.Quantity),
                            IsPaid = false,
                            DispensingNotes = u.ProcessingNotes ?? "Completed prescription",
                            PatientInstructions = pl.Instructions ?? string.Empty,
                            PrescriptionLine = pl,
                            Pharmacist = userIdToPharmacist.TryGetValue(u.ProcessedById ?? string.Empty, out var ph2) ? ph2 : null
                        }))
                    .ToList();

                dispensedPrescriptions.AddRange(completedConvertedAll);

                // Include IMPORTED prescriptions (no UploadId) that appear under Completed view, de-duplicated
                var importedPrescriptionsAll = await _context.Prescriptions
                    .Include(p => p.Customer)
                    .Include(p => p.PrescriptionLines)
                        .ThenInclude(pl => pl.Medication)
                    .Where(p => p.UploadId == null)
                    .Where(p => p.PrescriptionDate >= startDate.Value && p.PrescriptionDate <= endDate.Value.AddDays(1))
                    .ToListAsync();

                var importedConvertedAll = importedPrescriptionsAll
                    .SelectMany(p => p.PrescriptionLines
                        .Where(pl => !dispensedPrescriptionLineIdsAll.Contains(pl.PrescriptionLineId))
                        .Select(pl => new DispensedPrescription
                        {
                            DispensedPrescriptionId = pl.PrescriptionLineId + 4000000,
                            PrescriptionLineId = pl.PrescriptionLineId,
                            PharmacistId = 0,
                            DispensedDate = p.PrescriptionDate,
                            QuantityDispensed = pl.Quantity,
                            AmountDue = (decimal)((pl.Medication?.Price ?? 0) * pl.Quantity),
                            IsPaid = false,
                            DispensingNotes = "Imported prescription",
                            PatientInstructions = pl.Instructions ?? string.Empty,
                            PrescriptionLine = pl,
                            Pharmacist = null
                        }))
                    .ToList();

                dispensedPrescriptions.AddRange(importedConvertedAll);

                // Also get walk-in customer prescriptions that don't have DispensedPrescription records
                var walkInPrescriptions = await _context.Prescriptions
                    .Include(p => p.Customer)
                    .Include(p => p.PrescriptionLines)
                        .ThenInclude(pl => pl.Medication)
                    .Where(p => p.Customer.IsWalkInCustomer == true)
                    .Where(p => p.PrescriptionDate >= startDate.Value && p.PrescriptionDate <= endDate.Value.AddDays(1))
                    .OrderBy(p => p.PrescriptionDate)
                    .ToListAsync();

                // Get all prescription line IDs that have been dispensed
                var dispensedPrescriptionLineIds = await _context.DispensedPrescriptions
                    .Select(dp => dp.PrescriptionLineId)
                    .ToListAsync();

                // Convert walk-in prescriptions to DispensedPrescription-like objects
                // Only include prescription lines that haven't been dispensed
                var convertedWalkInPrescriptions = walkInPrescriptions
                    .SelectMany(p => p.PrescriptionLines
                        .Where(pl => !dispensedPrescriptionLineIds.Contains(pl.PrescriptionLineId))
                        .Select(pl => new DispensedPrescription
                        {
                            DispensedPrescriptionId = pl.PrescriptionLineId + 2000000, // Offset to avoid ID conflicts
                            PrescriptionLineId = pl.PrescriptionLineId,
                            PharmacistId = 0, // No specific pharmacist for walk-in prescriptions
                            DispensedDate = p.PrescriptionDate,
                            QuantityDispensed = pl.Quantity,
                            AmountDue = (decimal)((pl.Medication?.Price ?? 0) * pl.Quantity), // Calculate based on medication price
                            IsPaid = false,
                            DispensingNotes = "Walk-in customer prescription",
                            PatientInstructions = pl.Instructions ?? "",
                            PrescriptionLine = pl,
                            Pharmacist = null // No specific pharmacist
                        }))
                    .ToList();

                // Add walk-in prescriptions to the main list
                dispensedPrescriptions.AddRange(convertedWalkInPrescriptions);

                // Also get dispensed medications from orders
                var dispensedOrderItems = await _context.OrderItems
                    .Include(oi => oi.Order)
                        .ThenInclude(o => o.Customer)
                    .Include(oi => oi.Medication)
                    .Include(oi => oi.Order)
                        .ThenInclude(o => o.Pharmacist)
                    .Where(oi => oi.DispensedDate >= startDate.Value && oi.DispensedDate <= endDate.Value.AddDays(1))
                    .Where(oi => oi.DispensingStatus == DispensingStatusEnum.Filled)
                    .Where(oi => oi.QuantityDispensed > 0)
                    .OrderBy(oi => oi.DispensedDate)
                    .ToListAsync();

                // Convert OrderItems to DispensedPrescription-like objects for consistent reporting
                var convertedOrderItems = dispensedOrderItems.Select(oi => new DispensedPrescription
                {
                    DispensedPrescriptionId = oi.OrderItemId + 1000000, // Offset to avoid ID conflicts
                    PrescriptionLineId = 0, // No prescription line for orders
                    PharmacistId = oi.DispensedBy ?? 0,
                    DispensedDate = oi.DispensedDate ?? DateTime.MinValue,
                    QuantityDispensed = oi.QuantityDispensed,
                    AmountDue = (decimal)(oi.UnitPrice * oi.QuantityDispensed),
                    IsPaid = oi.Order.PaymentStatus,
                    PaymentDate = oi.Order.PaymentStatus ? oi.DispensedDate : null,
                    DispensingNotes = oi.DispensingNotes,
                    PatientInstructions = oi.DispensingNotes ?? "No specific instructions provided", // Use dispensing notes as patient instructions
                    // Create a virtual prescription line for orders
                    PrescriptionLine = new PrescriptionLine
                    {
                        PrescriptionLineId = 0,
                        Medication = oi.Medication,
                        Prescription = new Prescription
                        {
                            CustomerId = oi.Order.CustomerId,
                            DoctorId = null, // Orders don't have doctors
                            Customer = oi.Order.Customer
                        }
                    },
                    Pharmacist = oi.Order.Pharmacist // Set the pharmacist for orders
                }).ToList();

                // Combine both sources
                dispensedPrescriptions.AddRange(convertedOrderItems);
                dispensedPrescriptions = dispensedPrescriptions.OrderBy(dp => dp.DispensedDate).ToList();

                reportName = $"AllPharmacistsReport_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf";
            }

            if (!dispensedPrescriptions.Any())
            {
                // Log for debugging
                _logger.LogWarning("No dispensed prescriptions found for pharmacist {PharmacistId} in date range {StartDate} - {EndDate}. Total dispensed prescriptions in database: {TotalCount}", 
                    isPharmacist ? (await _context.Pharmacists.FirstOrDefaultAsync(p => p.UserId == currentUser.Id))?.PharmacistId : 0, 
                    startDate, endDate, 
                    await _context.DispensedPrescriptions.CountAsync());
                
                TempData["InfoMessage"] = $"No dispensed prescriptions found for the selected date range ({startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}). Please ensure you have dispensed some prescriptions during this period.";
                return RedirectToAction(nameof(Reports));
            }

            // Generate PDF report
            var reportGenerator = new PharmacistReportGenerator();
            var pharmacistName = isPharmacist ? 
                $"{dispensedPrescriptions.First().Pharmacist?.Name} {dispensedPrescriptions.First().Pharmacist?.Surname}" : 
                "All Pharmacists";
            var reportBytes = reportGenerator.GenerateReport(dispensedPrescriptions, pharmacistName, startDate.Value, endDate.Value, groupBy);
            
            return File(reportBytes, "application/pdf", reportName);
        }

        private bool PharmacistExists(int id)
        {
            return _context.Pharmacists.Any(e => e.PharmacistId == id);
        }
    }
}
