using IBhayiPharmacyManagementSystem.Data;
using IBhayiPharmacyManagementSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static IBhayiPharmacyManagementSystem.Models.DosageForm;

namespace IBhayiPharmacyManagementSystem.Controllers
{
    public class DosageFormsController : Controller
    {
        private readonly SignInManager<Users> _signInManager;
        private readonly UserManager<Users> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _context;

        public DosageFormsController(SignInManager<Users> signInManager,
            UserManager<Users> userManager,
            RoleManager<IdentityRole> roleManager, AppDbContext context)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        // GET: DosageForms
        public async Task<IActionResult> DosageForms()
        {
            if (TempData["SuccessMessage"] != null)
            {
                ViewBag.SuccessMessage = TempData["SuccessMessage"].ToString();
            }
            return View(await _context.Dosages.ToListAsync());
        }

        // GET: DosageForms/Create
        public IActionResult AddDosageForm()
        {
            ViewBag.DosageTypes = new SelectList(DosageFormTypes.Types);
            return View();
        }

        // POST: DosageForms/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddDosageForm(DosageForm dosageForm)
        {
            // Validate custom dosage type if it's not in the predefined list
            if (!string.IsNullOrEmpty(dosageForm.Type) && !DosageFormTypes.Types.Contains(dosageForm.Type))
            {
                // This is a custom type, validate it
                if (dosageForm.Type.Length < 2)
                {
                    ModelState.AddModelError("Type", "Custom dosage type must be at least 2 characters long.");
                }
                else if (dosageForm.Type.Length > 50)
                {
                    ModelState.AddModelError("Type", "Custom dosage type cannot exceed 50 characters.");
                }
                else
                {
                    // Check if this custom type already exists in the database
                    var existingType = await _context.Dosages
                        .FirstOrDefaultAsync(d => d.Type.ToLower() == dosageForm.Type.ToLower());
                    
                    if (existingType != null)
                    {
                        ModelState.AddModelError("Type", "A dosage form with this type already exists. Please choose a different type.");
                    }
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Trim and capitalize the first letter of the type
                    dosageForm.Type = dosageForm.Type?.Trim();
                    if (!string.IsNullOrEmpty(dosageForm.Type))
                    {
                        dosageForm.Type = char.ToUpper(dosageForm.Type[0]) + dosageForm.Type.Substring(1).ToLower();
                    }

                    _context.Add(dosageForm);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Dosage form added successfully!";
                    return RedirectToAction("DosageForms");
                }
                catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate key") == true)
                {
                    ModelState.AddModelError("Type", "A dosage form with this type already exists. Please choose a different type.");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "An error occurred while saving. Please try again.");
                }
            }
            
            ViewBag.DosageTypes = new SelectList(DosageForm.DosageFormTypes.Types);
            return View(dosageForm);
        }

        // GET: DosageForms/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var dosageForm = await _context.Dosages.FindAsync(id);
            if (dosageForm == null)
            {
                return NotFound();
            }
            
            ViewBag.DosageTypes = new SelectList(DosageFormTypes.Types);
            ViewBag.CurrentType = dosageForm.Type; // Pass the current type to the view
            return View(dosageForm);
        }

        // POST: DosageForms/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, DosageForm dosageForm)
        {
            if (id != dosageForm.DosageFormId)
            {
                return NotFound();
            }

            // Validate custom dosage type if it's not in the predefined list
            if (!string.IsNullOrEmpty(dosageForm.Type) && !DosageFormTypes.Types.Contains(dosageForm.Type))
            {
                // This is a custom type, validate it
                if (dosageForm.Type.Length < 2)
                {
                    ModelState.AddModelError("Type", "Custom dosage type must be at least 2 characters long.");
                }
                else if (dosageForm.Type.Length > 50)
                {
                    ModelState.AddModelError("Type", "Custom dosage type cannot exceed 50 characters.");
                }
                else
                {
                    // Check if this custom type already exists in the database (excluding current record)
                    var existingType = await _context.Dosages
                        .FirstOrDefaultAsync(d => d.Type.ToLower() == dosageForm.Type.ToLower() && d.DosageFormId != id);
                    
                    if (existingType != null)
                    {
                        ModelState.AddModelError("Type", "A dosage form with this type already exists. Please choose a different type.");
                    }
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Trim and capitalize the first letter of the type
                    dosageForm.Type = dosageForm.Type?.Trim();
                    if (!string.IsNullOrEmpty(dosageForm.Type))
                    {
                        dosageForm.Type = char.ToUpper(dosageForm.Type[0]) + dosageForm.Type.Substring(1).ToLower();
                    }

                    _context.Update(dosageForm);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Dosage form updated successfully!";
                    return RedirectToAction(nameof(DosageForms));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DosageFormExists(dosageForm.DosageFormId))
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
            
            ViewBag.DosageTypes = new SelectList(DosageFormTypes.Types);
            return View(dosageForm);
        }

        // POST: DosageForms/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var dosageForm = await _context.Dosages.FindAsync(id);
                if (dosageForm == null)
                {
                    return Json(new { success = false, message = "Dosage form not found." });
                }

                _context.Dosages.Remove(dosageForm);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Dosage form deleted successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while deleting the dosage form." });
            }
        }

        private bool DosageFormExists(int id)
        {
            return _context.Dosages.Any(e => e.DosageFormId == id);
        }
    }
}