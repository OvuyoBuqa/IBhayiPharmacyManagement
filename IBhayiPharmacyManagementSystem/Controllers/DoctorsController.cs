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
    public class DoctorsController : Controller
    {
        private readonly SignInManager<Users> _signInManager;
        private readonly UserManager<Users> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _context;

        public DoctorsController(SignInManager<Users> signInManager,
            UserManager<Users> userManager,
            RoleManager<IdentityRole> roleManager, AppDbContext context)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        // GET: Doctors
        public async Task<IActionResult> Index()
        {
            return View(await _context.Doctors.ToListAsync());
        }

        // GET: Doctors/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var doctor = await _context.Doctors
                .FirstOrDefaultAsync(m => m.DoctorId == id);
            if (doctor == null)
            {
                return NotFound();
            }

            return View(doctor);
        }

        // GET: Doctors/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Doctors/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create( Doctor doctor)
        {
            // Check if practice number already exists
            if (await _context.Doctors.AnyAsync(d => d.PracticeNumber == doctor.PracticeNumber))
            {
                ModelState.AddModelError("PracticeNumber", "This practice number is already registered. Please use a unique practice number.");
                return View(doctor);
            }

            if (ModelState.IsValid)
            {
                _context.Add(doctor);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Doctor added successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(doctor);
        }

        // GET: Doctors/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var doctor = await _context.Doctors.FindAsync(id);
            if (doctor == null)
            {
                return NotFound();
            }
            return View(doctor);
        }

        // POST: Doctors/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id,  Doctor doctor)
        {
            if (id != doctor.DoctorId)
            {
                return NotFound();
            }

            // Check if practice number already exists (excluding current doctor)
            if (await _context.Doctors.AnyAsync(d => d.PracticeNumber == doctor.PracticeNumber && d.DoctorId != id))
            {
                ModelState.AddModelError("PracticeNumber", "This practice number is already registered. Please use a unique practice number.");
                return View(doctor);
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(doctor);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Doctor updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DoctorExists(doctor.DoctorId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(doctor);
        }

        // GET: Doctors/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var doctor = await _context.Doctors
                .FirstOrDefaultAsync(m => m.DoctorId == id);
            if (doctor == null)
            {
                return NotFound();
            }

            // Check if the doctor is linked to any prescriptions or dispensation requests
            var hasPrescriptions = await _context.Prescriptions.AnyAsync(p => p.DoctorId == id);
            

            if (hasPrescriptions)
            {
                ViewBag.CanDelete = false;
                ViewBag.ErrorMessage = "This doctor cannot be deleted because they are linked to existing prescriptions or dispensation requests.";
            }
            else
            {
                ViewBag.CanDelete = true;
            }

            return View(doctor);
        }

        // POST: Doctors/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var doctor = await _context.Doctors.FindAsync(id);
            if (doctor == null)
            {
                return NotFound();
            }

            // Check again for linked prescriptions or dispensation requests before deleting
            var hasPrescriptions = await _context.Prescriptions.AnyAsync(p => p.DoctorId == id);


            if (hasPrescriptions)
            {
                TempData["ErrorMessage"] = "Cannot delete doctor because they are linked to existing prescriptions.";
                return RedirectToAction(nameof(Delete), new { id = id });
            }

            _context.Doctors.Remove(doctor);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Doctor deleted successfully!";
            return RedirectToAction(nameof(Index));
        }

        private bool DoctorExists(int id)
        {
            return _context.Doctors.Any(e => e.DoctorId == id);
        }
    }
}
