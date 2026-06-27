using IBhayiPharmacyManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IBhayiPharmacyManagementSystem.Data;
using IBhayiPharmacyManagementSystem.Models;

namespace IBhayiPharmacyManagementSystem.Controllers
{
    [Authorize(Roles = "PharmacyManager")]
    public class PharmacyManagerProfileController : Controller
    {
        private readonly UserManager<Users> _userManager;
        private readonly AppDbContext _context;

        public PharmacyManagerProfileController(UserManager<Users> userManager, AppDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account"); // Redirect to login if user not found
            }

            var pharmacyManager = await _context.PharmacyManagers
                .Include(pm => pm.User)
                .Include(pm => pm.Pharmacy)
                .FirstOrDefaultAsync(pm => pm.UserId == currentUser.Id);

            if (pharmacyManager == null)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var model = new PharmacyManagerProfileViewModel
            {
                UserId = pharmacyManager.UserId,
                PharmacyManagerId = pharmacyManager.PharmacyManagerId,
                Name = pharmacyManager.Name,
                Surname = pharmacyManager.Surname,
                ContactNumber = pharmacyManager.ContactNumber,
                Email = pharmacyManager.Email,
                BranchName = pharmacyManager.BranchName,
                PharmacyName = pharmacyManager.Pharmacy?.Name // Get pharmacy name from navigation property
            };

            return View(model);
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            return PartialView("_ChangePassword");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string OldPassword, string NewPassword, string ConfirmNewPassword)
        {
            if (string.IsNullOrWhiteSpace(OldPassword)) ModelState.AddModelError("OldPassword", "Current password is required.");
            if (string.IsNullOrWhiteSpace(NewPassword)) ModelState.AddModelError("NewPassword", "New password is required.");
            if (NewPassword != ConfirmNewPassword) ModelState.AddModelError("ConfirmNewPassword", "Password does not match.");

            if (!ModelState.IsValid) return PartialView("_ChangePassword");

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var result = await _userManager.ChangePasswordAsync(currentUser, OldPassword, NewPassword);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return PartialView("_ChangePassword");
            }

            TempData["SuccessMessage"] = "Password changed successfully.";
            return RedirectToAction("Profile");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(PharmacyManagerProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var pharmacyManager = await _context.PharmacyManagers.FirstOrDefaultAsync(pm => pm.UserId == currentUser.Id);

            if (pharmacyManager == null)
            {
                ModelState.AddModelError(string.Empty, "Pharmacy Manager not found.");
                return View(model);
            }

            // Update PharmacyManager details
            pharmacyManager.Name = model.Name;
            pharmacyManager.Surname = model.Surname;
            pharmacyManager.ContactNumber = model.ContactNumber;
            pharmacyManager.Email = model.Email;
            // BranchName and PharmacyName are display-only in this context, not directly editable via profile
            // If these were editable, you'd need to handle updates to Pharmacy model or other related entities

            // Update Identity User email if it has changed
            if (currentUser.Email != model.Email)
            {
                var setEmailResult = await _userManager.SetEmailAsync(currentUser, model.Email);
                if (!setEmailResult.Succeeded)
                {
                    foreach (var error in setEmailResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    return View(model);
                }
                // Update UserName as well if it's tied to the email
                var setUserNameResult = await _userManager.SetUserNameAsync(currentUser, model.Email);
                if (!setUserNameResult.Succeeded)
                {
                    foreach (var error in setUserNameResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    return View(model);
                }
            }

            try
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Profile updated successfully.";
                return RedirectToAction("Profile");
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError(string.Empty, "Error updating profile. Please try again.");
                return View(model);
            }
        }
    }
}
