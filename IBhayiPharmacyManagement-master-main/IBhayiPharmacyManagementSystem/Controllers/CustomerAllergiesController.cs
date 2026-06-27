using IBhayiPharmacyManagementSystem.Data;
using IBhayiPharmacyManagementSystem.Models;
using IBhayiPharmacyManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

[Authorize]
public class CustomerAllergiesController : Controller
{
    private readonly SignInManager<Users> _signInManager;
    private readonly UserManager<Users> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly AppDbContext _context;

    public CustomerAllergiesController(SignInManager<Users> signInManager,
            UserManager<Users> userManager,
            RoleManager<IdentityRole> roleManager, AppDbContext context)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
    }

    // GET: CustomerAllergies
    public async Task<IActionResult> CustomerAllergyRecords()
    {
        var allergies = await _context.CustomerAllergies
            .Include(ca => ca.Customer)
            .Include(ca => ca.ActiveIngredient) // Changed from ActiveIngredients to ActiveIngredient
            .ToListAsync();
        return View(allergies);
    }

    // GET: CustomerAllergies/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var customerAllergy = await _context.CustomerAllergies
            .Include(ca => ca.Customer)
            .Include(ca => ca.ActiveIngredient) // Changed from ActiveIngredients to ActiveIngredient
            .FirstOrDefaultAsync(m => m.AllergyId == id);

        if (customerAllergy == null)
        {
            return NotFound();
        }

        return View(customerAllergy);
    }

    // GET: CustomerAllergies/Create
    public async Task<IActionResult> AddCustomerAllergies()
    {
        var viewModel = new CustomerAllergyViewModel
        {
            Customers = await _context.Customers.ToListAsync(),
            ActiveIngredients = await _context.ActiveIngredients.ToListAsync()
        };
        return View(viewModel);
    }

    // POST: CustomerAllergies/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCustomerAllergies(CustomerAllergyViewModel model) // Changed parameter name from viewModel to model
    {
        if (ModelState.IsValid)
        {
            var customerAllergy = new CustomerAllergy
            {
                CustomerId = model.CustomerId,
                ActiveIngredientId = model.ActiveIngredientId,
                Severity = model.Severity,
                Description = model.Description
            };

            _context.Add(customerAllergy);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Allergy record added successfully!";
            return RedirectToAction(nameof(CustomerAllergyRecords));
        }

        // Repopulate dropdowns if validation fails
        model.Customers = await _context.Customers.ToListAsync();
        model.ActiveIngredients = await _context.ActiveIngredients.ToListAsync();
        return View(model);
    }

    // GET: CustomerAllergies/Edit/5
    public async Task<IActionResult> EditCustomerAllergies(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var customerAllergy = await _context.CustomerAllergies.FindAsync(id);
        if (customerAllergy == null)
        {
            return NotFound();
        }

        var viewModel = new CustomerAllergyViewModel
        {
            AllergyId = customerAllergy.AllergyId,
            CustomerId = customerAllergy.CustomerId,
            ActiveIngredientId = customerAllergy.ActiveIngredientId,
            Severity = customerAllergy.Severity,
            Description = customerAllergy.Description,
            Customers = await _context.Customers.ToListAsync(),
            ActiveIngredients = await _context.ActiveIngredients.ToListAsync()
        };

        return View(viewModel);
    }

    // POST: CustomerAllergies/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCustomerAllergies(int id, CustomerAllergyViewModel viewModel)
    {
        if (id != viewModel.AllergyId)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                var customerAllergy = new CustomerAllergy
                {
                    AllergyId = viewModel.AllergyId,
                    CustomerId = viewModel.CustomerId,
                    ActiveIngredientId = viewModel.ActiveIngredientId,
                    Severity = viewModel.Severity,
                    Description = viewModel.Description
                };

                _context.Update(customerAllergy);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Allergy record updated successfully!";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CustomerAllergyExists(viewModel.AllergyId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(CustomerAllergyRecords));
        }

        // Repopulate dropdowns if validation fails
        viewModel.Customers = await _context.Customers.ToListAsync();
        viewModel.ActiveIngredients = await _context.ActiveIngredients.ToListAsync();
        return View(viewModel);
    }

    // GET: CustomerAllergies/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var customerAllergy = await _context.CustomerAllergies
            .Include(ca => ca.Customer)
            .Include(ca => ca.ActiveIngredient) // Changed from ActiveIngredients to ActiveIngredient
            .FirstOrDefaultAsync(m => m.AllergyId == id);

        if (customerAllergy == null)
        {
            return NotFound();
        }

        return View(customerAllergy);
    }

    // POST: CustomerAllergies/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var customerAllergy = await _context.CustomerAllergies.FindAsync(id);
        if (customerAllergy != null)
        {
            _context.CustomerAllergies.Remove(customerAllergy);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Allergy record deleted successfully!";
        }


        return RedirectToAction(nameof(CustomerAllergyRecords));
    }

    private bool CustomerAllergyExists(int id)
    {
        return _context.CustomerAllergies.Any(e => e.AllergyId == id);
    }
}