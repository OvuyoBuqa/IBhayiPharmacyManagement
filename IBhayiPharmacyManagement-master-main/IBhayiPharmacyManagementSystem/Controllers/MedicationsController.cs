using IBhayiPharmacyManagementSystem.Data;
using IBhayiPharmacyManagementSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IBhayiPharmacyManagementSystem.Controllers
{
    public class MedicationsController : Controller
    {
        private readonly SignInManager<Users> _signInManager;
        private readonly UserManager<Users> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _context;
        private readonly ILogger<MedicationsController> _logger;

        public MedicationsController(AppDbContext context,
             SignInManager<Users> signInManager,
            UserManager<Users> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<MedicationsController> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _logger = logger;
        }

        // GET: Medications
        public async Task<IActionResult> Index()
        {
            return View(await _context.Medications.OrderBy(m => m.Name).ToListAsync());
        }

        // GET: Medications/Create
        public IActionResult AddMedication()
        {
            var medication = new Medication
            {
                MinStockLevel = 10,
                QuantityInStock = 0,
                Schedule = 0
            };

            PopulateDropdowns();
            _logger.LogInformation("Accessed AddMedication (GET) action.");
            return View(medication);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMedication(Medication medication, List<int> SelectedIngredients)
        {
          

            // Check if medication with same name already exists
            if (await _context.Medications.AnyAsync(m => m.Name == medication.Name))
            {
                ModelState.AddModelError("Name", "A medication with this name already exists.");
                PopulateDropdowns(medication, SelectedIngredients);
                _logger.LogWarning("Attempted to add medication with duplicate name: {MedicationName}", medication.Name);
                return View(medication);
            }

            try
            {
                medication.QuantityInStock = Math.Max(medication.QuantityInStock, 0);
                medication.MinStockLevel = Math.Max(medication.MinStockLevel, 1);

                _context.Add(medication);
                await _context.SaveChangesAsync();

                if (SelectedIngredients != null && SelectedIngredients.Any())
                {
                    foreach (var ingredientId in SelectedIngredients)
                    {
                        // Get the active ingredient to retrieve its strength
                        var activeIngredient = await _context.ActiveIngredients.FindAsync(ingredientId);
                        var medicationIngredient = new MedicationIngredient
                        {
                            MedicationId = medication.MedicationId,
                            ActiveIngredientId = ingredientId,
                            Strength = activeIngredient?.Strength ?? ""
                        };
                        _context.Add(medicationIngredient);
                    }
                    await _context.SaveChangesAsync();
                }

                TempData["SuccessMessage"] = $"Medication '{medication.Name}' added successfully!";
                _logger.LogInformation("Medication '{MedicationName}' (ID: {MedicationId}) added successfully.", medication.Name, medication.MedicationId);
                return RedirectToAction(nameof(MedicationHistory));
            }
            catch (DbUpdateException ex)
            {
                // Handle database constraint violations specifically
                if (ex.InnerException?.Message.Contains("duplicate key") == true)
                {
                    ModelState.AddModelError("Name", "A medication with this name already exists.");
                    _logger.LogWarning(ex, "Database error: Attempted to add medication with duplicate name: {MedicationName}", medication.Name);
                }
                else
                {
                    ModelState.AddModelError("", "An error occurred while saving: " + ex.Message);
                    _logger.LogError(ex, "Database error adding medication '{MedicationName}'.", medication.Name);
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while saving: " + ex.Message);
                _logger.LogError(ex, "Unexpected error adding medication '{MedicationName}'.", medication.Name);
            }

            PopulateDropdowns(medication, SelectedIngredients);
            return View(medication);
        }

        private List<SelectListItem> GetScheduleOptions()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "0", Text = "Schedule 0" },
                new SelectListItem { Value = "1", Text = "Schedule 1" },
                new SelectListItem { Value = "2", Text = "Schedule 2" },
                new SelectListItem { Value = "3", Text = "Schedule 3" },
                new SelectListItem { Value = "4", Text = "Schedule 4" },
                new SelectListItem { Value = "5", Text = "Schedule 5" },
                new SelectListItem { Value = "6", Text = "Schedule 6" }
            };
        }

        public async Task<IActionResult> MedicationHistory()
        {
            try
            {
                var medications = await _context.Medications
                    .Include(m => m.DosageForm)
                    .Include(m => m.Supplier)
                    .Include(m => m.ActiveIngredients)
                        .ThenInclude(mi => mi.ActiveIngredient)
                    .OrderBy(m => m.Name)
                    .ToListAsync();

                return View(medications);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading medication history. Please try again.";
                return View(new List<Medication>());
            }
        }

        private bool MedicationExists(int id)
        {
            return _context.Medications.Any(e => e.MedicationId == id);
        }

        // GET: Medications/Edit/5
        public async Task<IActionResult> EditMedication(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var medication = await _context.Medications
                .Include(m => m.Supplier)
                .Include(m => m.ActiveIngredients)
                .FirstOrDefaultAsync(m => m.MedicationId == id);

            if (medication == null)
            {
                return NotFound();
            }

            var selectedIngredients = medication.ActiveIngredients.Select(i => i.ActiveIngredientId).ToList();
            PopulateDropdowns(medication, selectedIngredients);
            _logger.LogInformation("Accessed EditMedication (GET) for medication (ID: {MedicationId}).", id);
            return View(medication);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMedication(int id, [Bind("MedicationId,Name,Schedule,Description,Price,MinStockLevel,QuantityInStock,IsNewMedication,DosageFormId,SupplierId")] Medication medication, List<int>? SelectedIngredients = null)
        {
            if (id != medication.MedicationId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(medication);

                    // Update active ingredients if SelectedIngredients is provided
                    if (SelectedIngredients != null)
                    {
                        var existingIngredients = await _context.MedicationIngredients
                            .Where(mi => mi.MedicationId == id)
                            .ToListAsync();

                        var ingredientsToRemove = existingIngredients
                            .Where(ei => !SelectedIngredients.Contains(ei.ActiveIngredientId))
                            .ToList();

                        _context.MedicationIngredients.RemoveRange(ingredientsToRemove);

                        var existingIngredientIds = existingIngredients.Select(ei => ei.ActiveIngredientId).ToList();
                        var ingredientsToAdd = SelectedIngredients
                            .Where(si => !existingIngredientIds.Contains(si))
                            .Select(si => {
                                var activeIngredient = _context.ActiveIngredients.Find(si);
                                return new MedicationIngredient
                                {
                                    MedicationId = id,
                                    ActiveIngredientId = si,
                                    Strength = activeIngredient?.Strength ?? ""
                                };
                            });

                        await _context.MedicationIngredients.AddRangeAsync(ingredientsToAdd);
                    }

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Medication '{medication.Name}' updated successfully!";
                    return RedirectToAction(nameof(MedicationHistory));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MedicationExists(medication.MedicationId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "An error occurred while saving: " + ex.Message);
                }
            }

            PopulateDropdowns(medication, SelectedIngredients ?? new List<int>());
            return View(medication);
        }

        // GET: Medications/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var medication = await _context.Medications
                .Include(m => m.Supplier)
                .Include(m => m.ActiveIngredients)
                    .ThenInclude(mi => mi.ActiveIngredient)
                .FirstOrDefaultAsync(m => m.MedicationId == id);

            if (medication == null)
            {
                return NotFound();
            }

            // Check for referential integrity before allowing delete
            var hasStockOrderItems = await _context.StockOrderItems.AnyAsync(soi => soi.MedicationId == id);
            var hasPrescriptionLines = await _context.PrescriptionLines.AnyAsync(pl => pl.MedicationId == id);

            if (hasStockOrderItems || hasPrescriptionLines)
            {
                TempData["ErrorMessage"] = $"Cannot delete medication '{medication.Name}' because it is linked to existing stock orders or prescriptions.";
                _logger.LogWarning("Attempted to delete medication (ID: {MedicationId}) linked to stock orders or prescriptions.", id);
                // You might want to return to a view that explains this
                return RedirectToAction(nameof(MedicationHistory)); // Or a specific error view
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new
                {
                    id = medication.MedicationId,
                    name = medication.Name,
                    strength = medication.ActiveIngredients?.FirstOrDefault()?.Strength ?? "N/A",
                    schedule = medication.Schedule,
                    ingredients = medication.ActiveIngredients.Select(i => i.ActiveIngredient.Name).ToList()
                });
            }
            _logger.LogInformation("Accessed Delete (GET) for medication (ID: {MedicationId}).", id);
            return View(medication);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var medication = await _context.Medications
                .Include(m => m.ActiveIngredients)
                .FirstOrDefaultAsync(m => m.MedicationId == id);

            if (medication == null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    _logger.LogWarning("Attempted to confirm deletion of non-existent medication (ID: {MedicationId}).", id);
                    return Json(new { success = false, message = "Medication not found" });
                }
                _logger.LogWarning("Attempted to confirm deletion of non-existent medication (ID: {MedicationId}).", id);
                return NotFound();
            }

            try
            {
                // Check for referential integrity again just before deletion (double-check)
                var hasStockOrderItems = await _context.StockOrderItems.AnyAsync(soi => soi.MedicationId == id);
                var hasPrescriptionLines = await _context.PrescriptionLines.AnyAsync(pl => pl.MedicationId == id);

                if (hasStockOrderItems || hasPrescriptionLines)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        _logger.LogWarning("Attempted to delete medication (ID: {MedicationId}) linked to stock orders or prescriptions during confirmation.", id);
                        return Json(new { success = false, message = $"Cannot delete medication '{medication.Name}' because it is linked to existing stock orders or prescriptions." });
                    }
                    TempData["ErrorMessage"] = $"Cannot delete medication '{medication.Name}' because it is linked to existing stock orders or prescriptions.";
                    _logger.LogWarning("Attempted to delete medication (ID: {MedicationId}) linked to stock orders or prescriptions during confirmation.", id);
                    return RedirectToAction(nameof(MedicationHistory));
                }

                _context.MedicationIngredients.RemoveRange(medication.ActiveIngredients);
                _context.Medications.Remove(medication);
                await _context.SaveChangesAsync();

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    _logger.LogInformation("Medication '{MedicationName}' (ID: {MedicationId}) deleted successfully via AJAX.", medication.Name, medication.MedicationId);
                    return Json(new
                    {
                        success = true,
                        message = $"Medication '{medication.Name}' deleted successfully!"
                    });
                }

                TempData["SuccessMessage"] = $"Medication '{medication.Name}' deleted successfully!";
                _logger.LogInformation("Medication '{MedicationName}' (ID: {MedicationId}) deleted successfully.", medication.Name, medication.MedicationId);
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                var message = "Error deleting medication. It may be referenced by other records.";
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    _logger.LogError(ex, "Database error deleting medication (ID: {MedicationId}) via AJAX.", id);
                    return Json(new { success = false, message });
                }
                TempData["ErrorMessage"] = message;
                _logger.LogError(ex, "Database error deleting medication (ID: {MedicationId}).", id);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                var message = "An unexpected error occurred while deleting the medication.";
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    _logger.LogError(ex, "Unexpected error deleting medication (ID: {MedicationId}) via AJAX.", id);
                    return Json(new { success = false, message });
                }
                TempData["ErrorMessage"] = message;
                _logger.LogError(ex, "Unexpected error deleting medication (ID: {MedicationId}).", id);
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: ActiveIngredients
        public async Task<IActionResult> ActiveIngredients()
        {
            return View(await _context.ActiveIngredients.ToListAsync());
        }

        // GET: ActiveIngredients/Create
        public IActionResult AddActiveIngredient()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddActiveIngredient(ActiveIngredients ingredient)
        {
            if (ModelState.IsValid)
            {
                _context.Add(ingredient);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Ingredient '{ingredient.Name}' added successfully!";
                return RedirectToAction(nameof(ActiveIngredients));
            }
            return View(ingredient);
        }

        // GET: ActiveIngredients/Edit/5
        public async Task<IActionResult> EditActiveIngredient(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var ingredient = await _context.ActiveIngredients.FindAsync(id);
            if (ingredient == null)
            {
                return NotFound();
            }
            return View(ingredient);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditActiveIngredient(int id, ActiveIngredients ingredient)
        {
            if (id != ingredient.ActiveIngredientId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(ingredient);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Ingredient '{ingredient.Name}' updated successfully!";
                    return RedirectToAction(nameof(ActiveIngredients));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ActiveIngredientExists(ingredient.ActiveIngredientId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            return View(ingredient);
        }

        // GET: ActiveIngredients/Delete/5
        public async Task<IActionResult> DeleteActiveIngredient(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var ingredient = await _context.ActiveIngredients
                .FirstOrDefaultAsync(m => m.ActiveIngredientId == id);
            if (ingredient == null)
            {
                return NotFound();
            }

            return View(ingredient);
        }

        [HttpPost, ActionName("DeleteActiveIngredient")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteActiveIngredientConfirmed(int id)
        {
            var ingredient = await _context.ActiveIngredients.FindAsync(id);
            if (ingredient == null)
            {
                return NotFound();
            }

            var isUsed = await _context.MedicationIngredients.AnyAsync(mi => mi.ActiveIngredientId == id);
            if (isUsed)
            {
                TempData["ErrorMessage"] = $"Cannot delete '{ingredient.Name}' because it is used in one or more medications.";
                return RedirectToAction(nameof(ActiveIngredients));
            }

            _context.ActiveIngredients.Remove(ingredient);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Ingredient '{ingredient.Name}' deleted successfully!";
            return RedirectToAction(nameof(ActiveIngredients));
        }

        private bool ActiveIngredientExists(int id)
        {
            return _context.ActiveIngredients.Any(e => e.ActiveIngredientId == id);
        }

        private void PopulateDropdowns(Medication? medication = null, List<int>? selectedIngredients = null)
        {
            ViewBag.SupplierId = new SelectList(_context.Suppliers, "SupplierId", "Name", medication?.SupplierId);
            ViewBag.ScheduleOptions = GetScheduleOptions();
            ViewBag.DosageFormId = new SelectList(_context.Dosages, "DosageFormId", "Type", medication?.DosageFormId);
            ViewBag.ActiveIngredients = new MultiSelectList(_context.ActiveIngredients, "ActiveIngredientId", "Name", selectedIngredients);
            ViewBag.ActiveIngredientsWithStrength = _context.ActiveIngredients.ToList();
        }

        // GET: Medications/ViewAll (For Pharmacist)
        public async Task<IActionResult> ViewAllMedications()
        {
            try
            {
                var medications = await _context.Medications
                    .Include(m => m.DosageForm)
                    .Include(m => m.Supplier)
                    .Include(m => m.ActiveIngredients)
                        .ThenInclude(mi => mi.ActiveIngredient)
                    .OrderBy(m => m.Name)
                    .ToListAsync();

                return View(medications);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading medications. Please try again.";
                return View(new List<Medication>());
            }
        }
    }
}