using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using IBhayiPharmacyManagementSystem.Data;
using IBhayiPharmacyManagementSystem.Models;
using IBhayiPharmacyManagementSystem.ViewModels;
using Microsoft.Extensions.Logging;

namespace IBhayiPharmacyManagementSystem.Controllers
{
    public class PharmaciesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PharmaciesController> _logger;

        public PharmaciesController(AppDbContext context, ILogger<PharmaciesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Pharmacies
        public async Task<IActionResult> Index()
        {
            var pharmacies = await _context.Pharmacies
                                            .Include(p => p.Address)
                                            .Include(p => p.Pharmacist)
                                                .ThenInclude(ph => ph.User)
                                            .ToListAsync();
            return View(pharmacies);
        }

        // GET: Pharmacies/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pharmacy = await _context.Pharmacies
                .Include(p => p.Address)
                .Include(p => p.Pharmacist)
                    .ThenInclude(ph => ph.User)
                .FirstOrDefaultAsync(m => m.PharmacyId == id);

            if (pharmacy == null)
            {
                return NotFound();
            }

            return View(pharmacy);
        }

        // GET: Pharmacies/Create
        public async Task<IActionResult> Create()
        {
            if (await _context.Pharmacies.AnyAsync())
            {
                TempData["ErrorMessage"] = "Only one pharmacy can be registered in the system. To make changes, please edit the existing pharmacy record.";
                return RedirectToAction(nameof(Index));
            }

            var pharmacists = await _context.Pharmacists.Include(p => p.User).ToListAsync();
            var viewModel = new PharmacyViewModel
            {
                Pharmacists = pharmacists.Select(p => new SelectListItem
                {
                    Value = p.PharmacistId.ToString(),
                    Text = p.User.FullName // Assuming FullName is available on the User model
                }).OrderBy(p => p.Text).ToList()
            };

            return View(viewModel);
        }

        // POST: Pharmacies/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PharmacyViewModel model)
        {
            if (await _context.Pharmacies.AnyAsync())
            {
                ModelState.AddModelError(string.Empty, "Only one pharmacy can be registered in the system. To make changes, please edit the existing pharmacy record.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var address = new Address
                    {
                        Street = model.Street,
                        Suburb = model.Suburb,
                        City = model.City,
                        Province = model.Province,
                        ZipCode = model.ZipCode,
                        Country = model.Country
                    };

                    _context.Addresses.Add(address);
                    await _context.SaveChangesAsync(); // Save address first to get Id

                    var pharmacy = new Pharmacy
                    {
                        Name = model.Name,
                        HealthcareCouncilRegistrationNumber = model.HealthcareCouncilRegistrationNumber,
                        AddressId = address.AddressId, // Assign the newly created address ID
                        ContactNumber = model.ContactNumber,
                        Email = model.Email,
                        WebsiteURL = model.WebsiteURL,
                        PharmacistId = model.PharmacistId
                    };

                    _context.Pharmacies.Add(pharmacy);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Pharmacy registered successfully!";
                    _logger.LogInformation("Pharmacy {PharmacyName} (ID: {PharmacyId}) registered by user.", model.Name, pharmacy.PharmacyId);
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException ex)
                {
                    ModelState.AddModelError(string.Empty, "A database error occurred while creating the pharmacy. Please ensure all data is valid and try again.");
                    _context.Entry(model).State = EntityState.Detached; // Detach the entity to prevent issues on re-submission
                    _logger.LogError(ex, "Database error during pharmacy creation for {PharmacyName}", model.Name);
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, "An unexpected error occurred while creating the pharmacy: " + ex.Message);
                    _logger.LogError(ex, "Unexpected error during pharmacy creation for {PharmacyName}", model.Name);
                }
            }

            // If ModelState is not valid, repopulate pharmacists dropdown and return view
            model.Pharmacists = (await _context.Pharmacists.Include(p => p.User).ToListAsync())
                .Select(p => new SelectListItem
                {
                    Value = p.PharmacistId.ToString(),
                    Text = p.User.FullName
                }).OrderBy(p => p.Text).ToList();

            return View(model);
        }

        // GET: Pharmacies/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pharmacy = await _context.Pharmacies
                                        .Include(p => p.Address)
                                        .FirstOrDefaultAsync(p => p.PharmacyId == id);
            if (pharmacy == null)
            {
                return NotFound();
            }

            var pharmacists = await _context.Pharmacists.Include(p => p.User).ToListAsync();

            var viewModel = new PharmacyViewModel
            {
                PharmacyId = pharmacy.PharmacyId,
                Name = pharmacy.Name,
                HealthcareCouncilRegistrationNumber = pharmacy.HealthcareCouncilRegistrationNumber,
                Street = pharmacy.Address?.Street ?? "",
                Suburb = pharmacy.Address?.Suburb ?? "",
                City = pharmacy.Address?.City ?? "",
                Province = pharmacy.Address?.Province ?? "",
                ZipCode = pharmacy.Address?.ZipCode ?? "",
                Country = pharmacy.Address?.Country ?? "",
                ContactNumber = pharmacy.ContactNumber,
                Email = pharmacy.Email,
                WebsiteURL = pharmacy.WebsiteURL,
                PharmacistId = pharmacy.PharmacistId,
                Pharmacists = pharmacists.Select(p => new SelectListItem
                {
                    Value = p.PharmacistId.ToString(),
                    Text = p.User.FullName,
                    Selected = p.PharmacistId == pharmacy.PharmacistId
                }).OrderBy(p => p.Text).ToList()
            };

            return View(viewModel);
        }

        // POST: Pharmacies/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PharmacyViewModel model)
        {
            if (id != model.PharmacyId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var pharmacyToUpdate = await _context.Pharmacies
                                                            .Include(p => p.Address)
                                                            .FirstOrDefaultAsync(p => p.PharmacyId == id);

                    if (pharmacyToUpdate == null)
                    {
                        return NotFound();
                    }

                    // Update Pharmacy properties
                    pharmacyToUpdate.Name = model.Name;
                    pharmacyToUpdate.HealthcareCouncilRegistrationNumber = model.HealthcareCouncilRegistrationNumber;
                    pharmacyToUpdate.ContactNumber = model.ContactNumber;
                    pharmacyToUpdate.Email = model.Email;
                    pharmacyToUpdate.WebsiteURL = model.WebsiteURL;
                    pharmacyToUpdate.PharmacistId = model.PharmacistId;

                    // Update Address properties
                    if (pharmacyToUpdate.Address == null)
                    {
                        pharmacyToUpdate.Address = new Address(); // Create new address if it doesn't exist
                    }
                    pharmacyToUpdate.Address.Street = model.Street;
                    pharmacyToUpdate.Address.Suburb = model.Suburb;
                    pharmacyToUpdate.Address.City = model.City;
                    pharmacyToUpdate.Address.Province = model.Province;
                    pharmacyToUpdate.Address.ZipCode = model.ZipCode;
                    pharmacyToUpdate.Address.Country = model.Country;

                    _context.Update(pharmacyToUpdate);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Pharmacy details updated successfully.";
                    _logger.LogInformation("Pharmacy {PharmacyName} (ID: {PharmacyId}) details updated by user.", model.Name, model.PharmacyId);
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    if (!PharmacyExists(model.PharmacyId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        _logger.LogError(ex, "Concurrency error updating pharmacy {PharmacyName} (ID: {PharmacyId}).", model.Name, model.PharmacyId);
                        throw; // Re-throw the exception to let global error handler deal with it
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error updating pharmacy {PharmacyName} (ID: {PharmacyId}).", model.Name, model.PharmacyId);
                    ModelState.AddModelError(string.Empty, "An unexpected error occurred while updating the pharmacy: " + ex.Message);
                }
            }

            // If ModelState is not valid, repopulate pharmacists dropdown and return view
            model.Pharmacists = (await _context.Pharmacists.Include(p => p.User).ToListAsync())
                .Select(p => new SelectListItem
                {
                    Value = p.PharmacistId.ToString(),
                    Text = p.User.FullName,
                    Selected = p.PharmacistId == model.PharmacistId
                }).OrderBy(p => p.Text).ToList();

            return View(model);
        }

        // GET: Pharmacies/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pharmacy = await _context.Pharmacies
                .Include(p => p.Address)
                .Include(p => p.Pharmacist)
                    .ThenInclude(ph => ph.User)
                .FirstOrDefaultAsync(m => m.PharmacyId == id);

            if (pharmacy == null)
            {
                return NotFound();
            }

            return View(pharmacy);
        }

        // POST: Pharmacies/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var pharmacy = await _context.Pharmacies.Include(p => p.Address).FirstOrDefaultAsync(p => p.PharmacyId == id);
                if (pharmacy == null)
                {
                    TempData["ErrorMessage"] = "Pharmacy not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Check if there are any related records before deleting
                var hasPharmacists = await _context.Pharmacists.AnyAsync(ph => ph.PharmacistId == id); // Check for linked pharmacists
                // var hasMedications = await _context.Medications.AnyAsync(m => m.Supplier.SupplierId == id); // This check is problematic. Medications link to Suppliers, not directly to Pharmacy.
                // Need to rethink this: if Pharmacy can only have ONE responsible Pharmacist, then deleting a Pharmacy should be safe if that Pharmacist is not responsible for other pharmacies or if their record is also cleaned up.
                // For now, I will assume a Pharmacy can't be deleted if its responsible pharmacist is still active, or if any medications (which are associated with suppliers) are somehow implicitly tied to this Pharmacy's existence.
                // Given the current schema, medications are linked to Suppliers, and Suppliers are not directly linked to a Pharmacy. This 'hasMedications' check is incorrect here.
                // A better check would be if the *responsible pharmacist* linked to this pharmacy is still active, or if there's any direct relationship between Pharmacy and Medication that's not through Supplier.
                // For now, I'll update the check for pharmacists and comment out the medication check until we clarify the schema.
                // var hasMedications = await _context.Medications.AnyAsync(m => m.Supplier.SupplierId == id); // This needs to be re-evaluated based on schema

                if (hasPharmacists)
                {
                    TempData["ErrorMessage"] = "Cannot delete pharmacy because it has a responsible pharmacist linked.";
                    return RedirectToAction(nameof(Delete), new { id = id });
                }

                _context.Pharmacies.Remove(pharmacy);
                if (pharmacy.Address != null)
                {
                    _context.Addresses.Remove(pharmacy.Address);
                }
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Pharmacy deleted successfully!";
                _logger.LogInformation("Pharmacy (ID: {PharmacyId}) deleted by user.", id);
            }
            catch (DbUpdateException ex)
            {
                TempData["ErrorMessage"] = "A database error occurred while deleting the pharmacy. It might be referenced by other records.";
                _logger.LogError(ex, "Database error deleting pharmacy (ID: {PharmacyId}).", id);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An unexpected error occurred while deleting the pharmacy.";
                _logger.LogError(ex, "Unexpected error deleting pharmacy (ID: {PharmacyId}).", id);
            }

            return RedirectToAction(nameof(Index));
        }

        private bool PharmacyExists(int id)
        {
            return _context.Pharmacies.Any(e => e.PharmacyId == id);
        }
    }
}
