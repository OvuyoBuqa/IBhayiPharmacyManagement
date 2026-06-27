using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using IBhayiPharmacyManagementSystem.Models;
using IBhayiPharmacyManagementSystem.ViewModels;
using IBhayiPharmacyManagementSystem.Services;
using IBhayiPharmacyManagementSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace Ibhayi_Pharmacy.Controllers
{
    [Authorize(Roles = "Admin, PharmacyManager")]
    public class StaffRegistrationController : Controller
    {
        private readonly UserManager<Users> _userManager;
        private readonly ILogger<StaffRegistrationController> _logger;
        private readonly IEmailSender _emailSender;
        private readonly AppDbContext _context;

        public StaffRegistrationController(
            UserManager<Users> userManager,
            ILogger<StaffRegistrationController> logger,
            IEmailSender emailSender,
            AppDbContext context)
        {
            _userManager = userManager;
            _logger = logger;
            _emailSender = emailSender;
            _context = context;
        }

        [HttpGet]
        public IActionResult Register()
        {
            var isPharmacyManager = User.IsInRole("PharmacyManager");

            var model = new RegisterStaffViewModel
            {
                // Set available roles based on current user
                AvailableRoles = isPharmacyManager ?
                    new List<string> { "Pharmacist" } :
                    new List<string> { "Pharmacist", "PharmacyManager" }
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterStaffViewModel model)
        {
            // Validate role assignment permissions
            var isPharmacyManager = User.IsInRole("PharmacyManager");
            if (isPharmacyManager && model.Role != "Pharmacist")
            {
                ModelState.AddModelError("Role", "You can only register Pharmacists");
                // Re-populate available roles for the view
                model.AvailableRoles = new List<string> { "Pharmacist" };
                return View(model);
            }

            // Additional validation for Pharmacist role before creating user account
            if (model.Role == "Pharmacist")
            {
                // Check if ID number already exists across all entities (Customers and Pharmacists)
                if (!string.IsNullOrWhiteSpace(model.IDNumber))
                {
                    bool idExistsInCustomers = await _context.Customers.AnyAsync(c => c.IDNumber == model.IDNumber);
                    bool idExistsInPharmacists = await _context.Pharmacists.AnyAsync(p => p.IDNumber == model.IDNumber);
                    
                    if (idExistsInCustomers || idExistsInPharmacists)
                    {
                        ModelState.AddModelError("IDNumber", "This ID number is already registered. Please use a unique ID number.");
                        model.AvailableRoles = User.IsInRole("PharmacyManager") ? new List<string> { "Pharmacist" } : new List<string> { "Pharmacist", "PharmacyManager" };
                        return View(model);
                    }
                }
                
                // Check if registration number already exists
                if (!string.IsNullOrWhiteSpace(model.RegistrationNumber))
                {
                    if (await _context.Pharmacists.AnyAsync(p => p.RegistrationNumber == model.RegistrationNumber))
                    {
                        ModelState.AddModelError("RegistrationNumber", "A pharmacist with this Registration Number already exists.");
                        model.AvailableRoles = User.IsInRole("PharmacyManager") ? new List<string> { "Pharmacist" } : new List<string> { "Pharmacist", "PharmacyManager" };
                        return View(model);
                    }
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var user = new Users
                    {
                        UserName = model.Email,
                        Email = model.Email,
                        FullName = model.FullName
                    };

                    var newPassword = GenerateRandomPassword();
                    var result = await _userManager.CreateAsync(user, newPassword);

                    if (result.Succeeded)
                    {
                        // Assign selected role
                        await _userManager.AddToRoleAsync(user, model.Role);

                        // Force password change on first login
                        user.ForcePasswordChange = true;
                        await _userManager.UpdateAsync(user);

                        // Generate and email password
                        var subject = "GRP-04-08 - Your New Account and Temporary Password for IBhayi Pharmacy";
                        var systemUrl = Url.Action("Login", "Account", null, protocol: HttpContext.Request.Scheme, host: HttpContext.Request.Host.Value);
                        var body = $"Dear {model.FullName},\n\nYour account has been created successfully. Your temporary password is: {newPassword}\n\nPlease log in and change your password immediately upon first login.\n\nAccess the system here: {systemUrl}\n\nRegards,\nIBhayi Pharmacy Management System";
                        await _emailSender.SendEmailAsync(user.Email, subject, body);

                        // If the role is Pharmacist, create a Pharmacist profile
                        if (model.Role == "Pharmacist")
                        {

                            var pharmacist = new Pharmacist
                            {
                                UserId = user.Id,
                                Name = model.FullName.Split(' ')[0], // Assuming first name
                                Surname = model.FullName.Split(' ').Length > 1 ? model.FullName.Split(' ')[1] : "", // Assuming last name
                                IDNumber = model.IDNumber!,
                                Email = model.Email,
                                CellPhone = model.CellPhoneNumber!,
                                RegistrationNumber = model.RegistrationNumber!,
                                IsActive = true
                            };
                            await _context.Pharmacists.AddAsync(pharmacist);
                            await _context.SaveChangesAsync();
                        }

                        // Log who registered the user
                        var currentUser = await _userManager.GetUserAsync(User);
                        _logger.LogInformation($"New {model.Role} '{user.Email}' registered by {currentUser.Email} with temporary password.");

                        TempData["SuccessMessage"] = $"Successfully registered {model.FullName}. Temporary password sent to {model.Email}.";
                        
                        // If registering a pharmacist, redirect to pharmacists list to show the new pharmacist
                        if (model.Role == "Pharmacist")
                        {
                            return RedirectToAction("Index", "Pharmacists");
                        }
                        
                        return RedirectToAction("Success");
                    }
                    else // This else handles if (result.Succeeded) is false
                    {
                        foreach (var error in result.Errors)
                        {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }
                        // Repopulate roles before returning view with errors
                        model.AvailableRoles = User.IsInRole("PharmacyManager") ? new List<string> { "Pharmacist" } : new List<string> { "Pharmacist", "PharmacyManager" };
                        return View(model);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during staff registration for {Email}", model.Email);
                    ModelState.AddModelError(string.Empty, "An unexpected error occurred during registration. Please try again.");
                    // Repopulate roles before returning view with exception error
                    model.AvailableRoles = User.IsInRole("PharmacyManager") ? new List<string> { "Pharmacist" } : new List<string> { "Pharmacist", "PharmacyManager" };
                    return View(model);
                }
            }

            // This repopulation is for when ModelState.IsValid is false.
            model.AvailableRoles = User.IsInRole("PharmacyManager") ?
                new List<string> { "Pharmacist" } :
                new List<string> { "Pharmacist", "PharmacyManager" };

            return View(model);
        }

        private string GenerateRandomPassword(int length = 12)
        {
            const string validChars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()_+";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            Random rnd = new Random();

            // Ensure password has at least one uppercase, one lowercase, one digit, and one special character
            sb.Append(validChars[rnd.Next(0, 25)]); // Uppercase
            sb.Append(validChars[rnd.Next(26, 51)]); // Lowercase
            sb.Append(validChars[rnd.Next(52, 61)]); // Digit
            sb.Append(validChars[rnd.Next(62, validChars.Length - 1)]); // Special character

            for (int i = sb.Length; i < length; i++)
            {
                sb.Append(validChars[rnd.Next(validChars.Length)]);
            }

            return new string(sb.ToString().OrderBy(s => (rnd.Next(2) % 2) == 0).ToArray());
        }

        [HttpGet]
        public IActionResult Success()
        {
            return View();
        }
    }
}

