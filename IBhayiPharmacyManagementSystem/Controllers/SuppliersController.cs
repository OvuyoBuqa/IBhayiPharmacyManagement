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
    public class SuppliersController : Controller
    {
        private readonly SignInManager<Users> _signInManager;
        private readonly UserManager<Users> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _context;
        private readonly ILogger<SuppliersController> _logger;

        public SuppliersController(AppDbContext context,
             SignInManager<Users> signInManager,
            UserManager<Users> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<SuppliersController> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _logger = logger;
        }

        // GET: Suppliers
        public async Task<IActionResult> Index()
        {
            return View(await _context.Suppliers.ToListAsync());
        }

        // GET: Suppliers/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(m => m.SupplierId == id);
            if (supplier == null)
            {
                return NotFound();
            }

            return View(supplier);
        }

        // GET: Suppliers/Create
        public IActionResult AddSupplier()
        {
            // Get the list of suppliers for the dropdown
            ViewBag.SupplierId = new SelectList(_context.Suppliers, "SupplierId", "Name");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddSupplier(Supplier model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Add your logic to save the supplier to the database
                    _context.Suppliers.Add(model);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Supplier added successfully!";
                    _logger.LogInformation("Supplier '{SupplierName}' (ID: {SupplierId}) added successfully.", model.Name, model.SupplierId);
                    return RedirectToAction("SupplierList"); // Assuming "SupplierList" is your list action
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, "An error occurred while adding the supplier: " + ex.Message);
                    _logger.LogError(ex, "Error adding supplier '{SupplierName}'.", model.Name);
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        // In SuppliersController
        public async Task<IActionResult> SupplierList()
        {
            var suppliers = await _context.Suppliers.ToListAsync();
            return View(suppliers);
        }

        public async Task<IActionResult> SupplierDetails(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(m => m.SupplierId == id);

            if (supplier == null)
            {
                return NotFound();
            }

            return View(supplier);
        }


        // GET: Suppliers/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null)
            {
                return NotFound();
            }
            return View(supplier);
        }

        // POST: Suppliers/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("SupplierId,Name,ContactPerson,Email")] Supplier supplier)
        {
            if (id != supplier.SupplierId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(supplier);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Supplier updated successfully!";
                    _logger.LogInformation("Supplier '{SupplierName}' (ID: {SupplierId}) updated successfully.", supplier.Name, supplier.SupplierId);
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    if (!SupplierExists(supplier.SupplierId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        _logger.LogError(ex, "Concurrency error updating supplier '{SupplierName}' (ID: {SupplierId}).", supplier.Name, supplier.SupplierId);
                        throw; // Re-throw the exception to let global error handler deal with it
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating supplier '{SupplierName}' (ID: {SupplierId}).", supplier.Name, supplier.SupplierId);
                    ModelState.AddModelError(string.Empty, "An unexpected error occurred while updating the supplier: " + ex.Message);
                }
                return RedirectToAction(nameof(SupplierList));
            }
            return View(supplier);
        }

        // GET: Suppliers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(m => m.SupplierId == id);
            if (supplier == null)
            {
                return NotFound();
            }

            return View(supplier);
        }

        // POST: Suppliers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var supplier = await _context.Suppliers.FindAsync(id);
                if (supplier == null)
                {
                    TempData["ErrorMessage"] = "Supplier not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Check if the supplier is linked to any medications or stock orders
                var hasMedications = await _context.Medications.AnyAsync(m => m.SupplierId == id);
                var hasStockOrders = await _context.StockOrders.AnyAsync(so => so.SupplierId == id);

                if (hasMedications || hasStockOrders)
                {
                    TempData["ErrorMessage"] = "Cannot delete supplier because it is linked to existing medications or stock orders.";
                    _logger.LogWarning("Attempted to delete supplier (ID: {SupplierId}) that is linked to medications or stock orders.", id);
                    return RedirectToAction(nameof(Delete), new { id = id }); // Redirect back to the GET Delete view with error
                }

                _context.Suppliers.Remove(supplier);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Supplier deleted successfully!";
                _logger.LogInformation("Supplier (ID: {SupplierId}) deleted successfully.", id);
            }
            catch (DbUpdateException ex)
            {
                TempData["ErrorMessage"] = "A database error occurred while deleting the supplier. It might be referenced by other records.";
                _logger.LogError(ex, "Database error deleting supplier (ID: {SupplierId}).", id);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An unexpected error occurred while deleting the supplier.";
                _logger.LogError(ex, "Unexpected error deleting supplier (ID: {SupplierId}).", id);
            }

            return RedirectToAction(nameof(SupplierList));
        }

        private bool SupplierExists(int id)
        {
            return _context.Suppliers.Any(e => e.SupplierId == id);
        }
    }
}
