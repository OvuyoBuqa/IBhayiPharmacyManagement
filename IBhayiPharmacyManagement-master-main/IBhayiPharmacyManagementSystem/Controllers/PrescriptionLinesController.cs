using IBhayiPharmacyManagementSystem.Data;
using IBhayiPharmacyManagementSystem.Enums;
using IBhayiPharmacyManagementSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using static IBhayiPharmacyManagementSystem.Models.UnprocessedScript;

namespace IBhayiPharmacyManagementSystem.Controllers
{
    public class PrescriptionLinesController : Controller
    {
        private readonly SignInManager<Users> _signInManager;
        private readonly UserManager<Users> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _context;

        public PrescriptionLinesController(
            SignInManager<Users> signInManager,
            UserManager<Users> userManager,
            RoleManager<IdentityRole> roleManager,
            AppDbContext context)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPrescriptionLine(int unprocessedScriptId, PrescriptionLine prescriptionLine)
        {
            try
            {
                // Validate the model state first
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage);
                    return Json(new { success = false, message = string.Join("\n", errors) });
                }

                // Check if medication exists
                var medicationExists = await _context.Medications
                    .AnyAsync(m => m.MedicationId == prescriptionLine.MedicationId);
                if (!medicationExists)
                {
                    return Json(new { success = false, message = "Selected medication does not exist" });
                }

                // Get the unprocessed script with prescription
                var unprocessedScript = await _context.UnprocessedScripts
                    .Include(u => u.Prescription)
                    .FirstOrDefaultAsync(u => u.UnploadId == unprocessedScriptId);

                if (unprocessedScript == null)
                {
                    return Json(new { success = false, message = "Prescription not found" });
                }

                // Check for duplicate medication in the same prescription
                if (unprocessedScript.Prescription != null)
                {
                    var duplicateExists = await _context.PrescriptionLines
                        .Where(pl => pl.PrescriptionId == unprocessedScript.Prescription.PrescriptionId && !pl.IsDeleted)
                        .AnyAsync(pl => pl.MedicationId == prescriptionLine.MedicationId);
                    if (duplicateExists)
                    {
                        return Json(new { success = false, message = "This medication has already been added to the prescription" });
                    }
                }

                // Create prescription if it doesn't exist
                if (unprocessedScript.Prescription == null)
                {
                    unprocessedScript.Prescription = new Prescription
                    {
                        CustomerId = unprocessedScript.CustomerId,
                        PrescriptionDate = DateTime.UtcNow,
                        UploadId = unprocessedScript.UnploadId
                    };
                    await _context.SaveChangesAsync();
                }

                // Set the prescription ID
                prescriptionLine.PrescriptionId = unprocessedScript.Prescription.PrescriptionId;

                // Create a new PrescriptionLine without trying to set navigation properties
                var newLine = new PrescriptionLine
                {
                    PrescriptionId = prescriptionLine.PrescriptionId,
                    MedicationId = prescriptionLine.MedicationId,
                    Quantity = prescriptionLine.Quantity,
                    Instructions = prescriptionLine.Instructions,
                    Frequency = prescriptionLine.Frequency,
                    TotalRepeats = prescriptionLine.TotalRepeats,
                    RepeatsRemaining = prescriptionLine.TotalRepeats
                };

                _context.PrescriptionLines.Add(newLine);
                await _context.SaveChangesAsync();

                // Get medication details for response
                var medication = await _context.Medications
                    .FirstOrDefaultAsync(m => m.MedicationId == prescriptionLine.MedicationId);

                // Update status if needed
                if (unprocessedScript.Status == PrescriptionStatus.Pending)
                {
                    unprocessedScript.Status = PrescriptionStatus.Processing;
                    await _context.SaveChangesAsync();
                }

                return Json(new
                {
                    success = true,
                    message = "Prescription item added successfully",
                    item = new
                    {
                        medicationName = medication?.Name,
                        dosageForm = medication?.DosageForm,
                        strength = medication?.ActiveIngredients?.FirstOrDefault()?.Strength ?? "N/A",
                        quantity = newLine.Quantity,
                        instructions = newLine.Instructions,
                        frequency = newLine.Frequency.ToString(),
                        totalRepeats = newLine.TotalRepeats,
                        lineId = newLine.PrescriptionLineId
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error saving prescription line: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, [FromForm] int unprocessedScriptId)
        {
            try
            {
                var line = await _context.PrescriptionLines
                    .Include(l => l.Prescription)
                    .FirstOrDefaultAsync(l => l.PrescriptionLineId == id);

                if (line == null)
                {
                    return Json(new { success = false, message = "Item not found" });
                }

                // Use soft delete instead of hard delete
                line.IsDeleted = true;
                line.DeletedAt = DateTime.UtcNow;
                // line.DeletedBy = User.Identity.Name; // Uncomment if you want to track who deleted it
                await _context.SaveChangesAsync();

                // Check if this was the last item (excluding soft-deleted items)
                var remainingItems = await _context.PrescriptionLines
                    .Where(pl => pl.PrescriptionId == line.PrescriptionId && !pl.IsDeleted)
                    .AnyAsync();

                if (!remainingItems)
                {
                    var script = await _context.UnprocessedScripts
                        .FirstOrDefaultAsync(u => u.UnploadId == unprocessedScriptId);

                    if (script != null && script.Status == PrescriptionStatus.Processing)
                    {
                        script.Status = PrescriptionStatus.Pending;
                        await _context.SaveChangesAsync();
                    }
                }

                return Json(new { success = true, message = "Item deleted successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error deleting item: " + ex.Message });
            }
        }

        public async Task<IActionResult> PrescriptionLineRecords(int? customerId, DateTime? fromDate, DateTime? toDate)
        {
            // Base query
            var query = _context.PrescriptionLines
                .Include(pl => pl.Prescription)
                    .ThenInclude(p => p.Customer)
                .Include(pl => pl.Medication)
                .AsQueryable();

            // Apply customer filter if provided
            if (customerId.HasValue)
            {
                query = query.Where(pl => pl.Prescription.CustomerId == customerId.Value);
            }

            // Apply date range filter if provided
            if (fromDate.HasValue)
            {
                query = query.Where(pl => pl.Prescription.PrescriptionDate >= fromDate.Value);
            }
            if (toDate.HasValue)
            {
                query = query.Where(pl => pl.Prescription.PrescriptionDate <= toDate.Value);
            }

            // Order by prescription date descending
            var records = await query
                .OrderByDescending(pl => pl.Prescription.PrescriptionDate)
                .ToListAsync();

            // Populate customer dropdown
            ViewBag.CustomerId = new SelectList(_context.Customers, "CustomerId", "FullName", customerId);

            // Pass filter values to view
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            return View(records);
        }
    }
}