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

namespace IBhayiPharmacyManagementSystem.Controllers
{
    public class ActiveIngredientsController : Controller
    {
        private readonly SignInManager<Users> _signInManager;
        private readonly UserManager<Users> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _context;

        public ActiveIngredientsController(SignInManager<Users> signInManager,
            UserManager<Users> userManager,
            RoleManager<IdentityRole> roleManager, AppDbContext context)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        // GET: ActiveIngredients
        public async Task<IActionResult> Index()
        {
            return View(await _context.ActiveIngredients.ToListAsync());
        }

        // GET: ActiveIngredients/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var activeIngredients = await _context.ActiveIngredients
                .FirstOrDefaultAsync(m => m.ActiveIngredientId == id);
            if (activeIngredients == null)
            {
                return NotFound();
            }

            return View(activeIngredients);
        }

        // GET: ActiveIngredients/Create
        public IActionResult AddActiveIngredients()
        {
            return View();
        }

        // POST: ActiveIngredients/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddActiveIngredients(ActiveIngredients activeIngredients)
        {
            // Check if ingredient with same name already exists (case-insensitive)
            if (await _context.ActiveIngredients.AnyAsync(ai => ai.Name.ToLower() == activeIngredients.Name.ToLower()))
            {
                ModelState.AddModelError("Name", "An active ingredient with this name already exists.");
                return View(activeIngredients);
            }

            try
            {
                _context.Add(activeIngredients);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Active ingredient added successfully!";
                return RedirectToAction(nameof(ActiveIngredientsList));  
            }
            catch (DbUpdateException ex)
            {
                // Handle database constraint violations specifically
                if (ex.InnerException?.Message.Contains("duplicate key") == true)
                {
                    ModelState.AddModelError("Name", "An active ingredient with this name already exists.");
                }
                else
                {
                    ModelState.AddModelError("", "An error occurred while saving: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while saving: " + ex.Message);
            }

            return View(activeIngredients);
        }

        // GET: ActiveIngredientsList
        public async Task<IActionResult> ActiveIngredientsList()
        {
            // Add success message handling if coming from create
            if (TempData["SuccessMessage"] != null)
            {
                ViewBag.SuccessMessage = TempData["SuccessMessage"].ToString();
            }

            return View(await _context.ActiveIngredients.ToListAsync());
        }

        // GET: ActiveIngredients/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var activeIngredients = await _context.ActiveIngredients.FindAsync(id);
            if (activeIngredients == null)
            {
                return NotFound();
            }
            return View(activeIngredients);
        }

        // POST: ActiveIngredients/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ActiveIngredientId,Name,Strength,Description")] ActiveIngredients activeIngredients)
        {
            if (id != activeIngredients.ActiveIngredientId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(activeIngredients);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Active ingredient updated successfully!";
                    return RedirectToAction(nameof(ActiveIngredientsList));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ActiveIngredientsExists(activeIngredients.ActiveIngredientId))
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
            
            return View(activeIngredients);
        }

        // GET: ActiveIngredients/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var activeIngredients = await _context.ActiveIngredients
                .FirstOrDefaultAsync(m => m.ActiveIngredientId == id);
            if (activeIngredients == null)
            {
                return NotFound();
            }

            return View(activeIngredients);
        }

        // POST: ActiveIngredients/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var activeIngredients = await _context.ActiveIngredients.FindAsync(id);
            if (activeIngredients == null)
            {
                return Json(new { success = false, message = "Active ingredient not found." });
            }

            // Prevent delete if used by any medications
            var usageCount = await _context.MedicationIngredients
                .CountAsync(mi => mi.ActiveIngredientId == id);

            if (usageCount > 0)
            {
                // Console log indicating number of referencing rows
                Console.WriteLine($"ActiveIngredient ID {id} is referenced by {usageCount} MedicationIngredient rows; deletion blocked.");
                return Json(new { success = false, message = $"Cannot delete. This ingredient is used in {usageCount} medication(s)." });
            }

            _context.ActiveIngredients.Remove(activeIngredients);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Active ingredient deleted successfully." });
        }

        private bool ActiveIngredientsExists(int id)
        {
            return _context.ActiveIngredients.Any(e => e.ActiveIngredientId == id);
        }
    }
}
