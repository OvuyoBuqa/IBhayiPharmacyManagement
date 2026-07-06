using IBhayiPharmacyManagementSystem.Data;
using IBhayiPharmacyManagementSystem.Models;
using IBhayiPharmacyManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IBhayiPharmacyManagementSystem.Services;
using System.Text.Json;
using System.Security.Claims;

namespace IBhayiPharmacyManagementSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<Users> _signInManager;
        private readonly UserManager<Users> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _context;
        private readonly ILogger<AccountController> _logger;
        private readonly IEmailSender _emailSender;
        private readonly ICustomerActivityService _activityService;

        public AccountController(
            SignInManager<Users> signInManager,
            UserManager<Users> userManager,
            RoleManager<IdentityRole> roleManager,
            AppDbContext context,
            ILogger<AccountController> logger,
            IEmailSender emailSender,
            ICustomerActivityService activityService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _logger = logger;
            _emailSender = emailSender;
            _activityService = activityService;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            _logger.LogInformation("Accessed Login (GET) page.");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Login (POST) failed due to invalid model state for email: {Email}", model.Email);
                return View(model);
            }

            var foundUser = await _userManager.FindByEmailAsync(model.Email);
            if (foundUser == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                _logger.LogWarning("Login (POST) failed: User not found for email: {Email}", model.Email);
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null)
                {
                    _logger.LogError("AccountController: User not found after successful sign-in for email: {Email}", model.Email);
                    ModelState.AddModelError(string.Empty, "An unexpected error occurred. Please try again.");
                    return View(model);
                }

                if (user.ForcePasswordChange)
                {
                    _logger.LogInformation("User {UserEmail} logged in with temporary password, redirecting to change password.", user.Email);
                    TempData["ForcePasswordChange"] = true;
                    TempData["UserEmailForPasswordChange"] = user.Email;
                    return RedirectToAction("ChangePassword", new { email = user.Email });
                }

                var roles = await _userManager.GetRolesAsync(user);
                _logger.LogInformation("User {UserEmail} logged in successfully with roles: {Roles}", user.Email, string.Join(", ", roles));

                // Log customer activity if user is a customer
                if (roles.Contains("Customer"))
                {
                    try
                    {
                        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == user.Id);
                        if (customer != null)
                        {
                            await _activityService.LogActivityAsync(
                                customer.CustomerId,
                                "Login",
                                "Customer logged into the system",
                                null,
                                null,
                                null
                            );
                        }
                    }
                    catch (Exception activityEx)
                    {
                        _logger.LogError(activityEx, "Failed to log login activity");
                    }
                }

                if (roles.Contains("Admin"))
                    return RedirectToLocal(returnUrl) ?? RedirectToAction("Admin", "Home");
                else if (roles.Contains("PharmacyManager"))
                    return RedirectToLocal(returnUrl) ?? RedirectToAction("PharmacyManager", "Home");
                else if (roles.Contains("Pharmacist"))
                    return RedirectToLocal(returnUrl) ?? RedirectToAction("Pharmacist", "Home");
                else if (roles.Contains("Customer"))
                    return RedirectToLocal(returnUrl) ?? RedirectToAction("Customer", "Home");

                return RedirectToLocal(returnUrl) ?? RedirectToAction("Index", "Home");
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("User account locked out: {Email}", model.Email);
                return View("Lockout");
            }

            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            _logger.LogWarning("Login (POST) failed for email: {Email}. Reason: Invalid credentials.", model.Email);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Register()
        {
            var activeIngredients = await _context.ActiveIngredients
                .OrderBy(ai => ai.Name)
                .ToListAsync();

            var model = new RegisterViewModel
            {
                AllergyStatus = "NoAllergies", // Default to no allergies
                AvailableActiveIngredients = activeIngredients
            };
            _logger.LogInformation("Accessed Register (GET) page.");
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            // Always repopulate the allergies list
            var activeIngredients = await _context.ActiveIngredients.ToListAsync();
            model.AvailableActiveIngredients = activeIngredients;

            // Check if ID number already exists across all entities (Customers and Pharmacists)
            bool idExistsInCustomers = await _context.Customers.AnyAsync(c => c.IDNumber == model.IDNumber);
            bool idExistsInPharmacists = await _context.Pharmacists.AnyAsync(p => p.IDNumber == model.IDNumber);
            
            if (idExistsInCustomers || idExistsInPharmacists)
            {
                ModelState.AddModelError("IDNumber", "This ID number is already registered. Please use a unique ID number.");
                _logger.LogWarning("Customer registration (POST) failed - duplicate ID number: {IDNumber}", model.IDNumber);
                return View(model);
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Customer registration (POST) failed due to invalid model state for email: {Email}", model.Email);
                return View(model);
            }

            var user = new Users
            {
                FullName = $"{model.Name} {model.Surname}",
                UserName = model.Email,
                Email = model.Email,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                if (!await _roleManager.RoleExistsAsync("Customer"))
                {
                    await _roleManager.CreateAsync(new IdentityRole("Customer"));
                }

                await _userManager.AddToRoleAsync(user, "Customer");

                var customer = new Customer
                {
                    UserId = user.Id,
                    Name = model.Name,
                    Surname = model.Surname,
                    IDNumber = model.IDNumber,
                    CellPhoneNumber = model.CellPhoneNumber,
                    Email = model.Email,
                    Street = model.Street,
                    Suburb = model.Suburb,
                    City = model.City,
                    Province = model.Province,
                    ZipCode = model.ZipCode,
                    Country = model.Country,
                    ProfileImagePath = "/images/default-profile.png",
                    DateCreated = DateTime.Now
                };

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();

                // Handle allergies based on allergy status
                if (model.AllergyStatus == "KnownAllergies")
                {
                    // Handle database allergies
                    if (model.SelectedAllergyIds != null && model.SelectedAllergyIds.Any())
                    {
                        for (int i = 0; i < model.SelectedAllergyIds.Count; i++)
                        {
                            if (model.SelectedAllergyIds[i] > 0) // Skip the default "0" value
                            {
                                var severity = i < model.AllergySeverities.Count ? model.AllergySeverities[i] : "Moderate";
                                var description = i < model.AllergyDescriptions.Count ? model.AllergyDescriptions[i] : "";

                                // Only add if we have valid data
                                if (!string.IsNullOrWhiteSpace(severity))
                                {
                                    _context.CustomerAllergies.Add(new CustomerAllergy
                                    {
                                        CustomerId = customer.CustomerId,
                                        ActiveIngredientId = model.SelectedAllergyIds[i],
                                        Severity = severity,
                                        Description = description ?? ""
                                    });
                                }
                            }
                        }
                    }

                    // Handle custom allergies - store them in a different way to avoid FK constraint issues
                    if (model.CustomAllergenNames != null && model.CustomAllergenNames.Any())
                    {
                        // Find the "Custom" active ingredient
                        var customIngredient = await _context.ActiveIngredients.FirstOrDefaultAsync(ai => ai.Name == "Custom");
                        if (customIngredient == null)
                        {
                            // If "Custom" ingredient doesn't exist, create it
                            customIngredient = new ActiveIngredients
                            {
                                Name = "Custom",
                                Description = "Custom allergen not in the standard database",
                                Strength = "N/A"
                            };
                            _context.ActiveIngredients.Add(customIngredient);
                            await _context.SaveChangesAsync();
                        }

                        for (int i = 0; i < model.CustomAllergenNames.Count; i++)
                        {
                            if (!string.IsNullOrWhiteSpace(model.CustomAllergenNames[i]))
                            {
                                var severity = i < model.CustomAllergySeverities.Count ? model.CustomAllergySeverities[i] : "Moderate";
                                var description = i < model.CustomAllergyDescriptions.Count ? model.CustomAllergyDescriptions[i] : "";

                                // For custom allergies, we'll store them in the description field with a special prefix
                                // This avoids the foreign key constraint issue
                                _context.CustomerAllergies.Add(new CustomerAllergy
                                    {
                                        CustomerId = customer.CustomerId,
                                        ActiveIngredientId = customIngredient.ActiveIngredientId,
                                        Severity = severity ?? "Moderate",
                                        Description = $"CUSTOM_ALLERGEN: {model.CustomAllergenNames[i]} - {description ?? ""}"
                                    });
                            }
                        }
                    }
                }

                await _context.SaveChangesAsync();

                // Log registration activity
                try
                {
                    await _activityService.LogActivityAsync(
                        customer.CustomerId,
                        "Registration",
                        "Customer registered and created account in the system",
                        "Customer",
                        customer.CustomerId,
                        null
                    );
                }
                catch (Exception activityEx)
                {
                    _logger.LogError(activityEx, "Failed to log registration activity");
                }

                TempData["SuccessMessage"] = "Registration successful! Please login with your credentials.";
                _logger.LogInformation("Customer {CustomerEmail} registered successfully. Customer ID: {CustomerId}", model.Email, customer.CustomerId);
                return RedirectToAction("Login", "Account");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            _logger.LogWarning("Customer registration (POST) failed for email: {Email}. Errors: {Errors}", model.Email, string.Join("; ", result.Errors.Select(e => e.Description)));
            return View(model);
        }

        [HttpGet]
        public IActionResult VerifyEmail()
        {
            _logger.LogInformation("Accessed VerifyEmail (GET) page for password reset.");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyEmail(VerifyEmailViewModel model)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("VerifyEmail (POST) failed due to invalid model state for email: {Email}", model.Email);
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
            {
                ModelState.AddModelError("", "User not found!");
                _logger.LogWarning("VerifyEmail (POST) failed: User not found for email: {Email}", model.Email);
                return View(model);
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var callbackUrl = Url.Action("ResetPassword", "Account", new { token = token, email = user.Email }, protocol: HttpContext.Request.Scheme);

            await _emailSender.SendEmailAsync(
                model.Email,
                "GRP-04-08 - Reset Password - IBhayi Pharmacy",
                $"Please reset your password by clicking here: <a href=\"{callbackUrl}\">link</a>");

            TempData["SuccessMessage"] = "Password reset link sent to your email.";
            _logger.LogInformation("Password reset link sent to {Email} for user ID: {UserId}", model.Email, user.Id);
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult ChangePassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                _logger.LogWarning("ChangePassword (GET) called with null or empty email. Redirecting to Login.");
                return RedirectToAction("Login", "Account");
            }
            _logger.LogInformation("Accessed ChangePassword (GET) for email: {Email}", email);
            
            // Check if this is a forced password change
            var user = _userManager.FindByEmailAsync(email).Result;
            bool isForcedChange = user?.ForcePasswordChange == true;
            
            return View(new ChangePasswordViewModel { 
                Email = email, 
                RequiresOldPassword = !isForcedChange 
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPasswordDirect(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ResetPasswordDirect (POST) failed due to invalid model state for email: {Email}", model.Email);
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
            {
                ModelState.AddModelError("", "User not found!");
                _logger.LogWarning("ResetPasswordDirect (POST) failed: User not found for email: {Email}", model.Email);
                return View(model);
            }

            // Generate a password reset token and use it to reset the password
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

            if (result.Succeeded)
            {
                // If this was a forced password change for a new pharmacist, ideally you'd clear a flag here:
                if (user.ForcePasswordChange)
                {
                    user.ForcePasswordChange = false;
                    await _userManager.UpdateAsync(user);
                    _logger.LogInformation("ForcePasswordChange flag cleared for user {UserId} after password reset.", user.Id);
                }

                if (await _userManager.IsLockedOutAsync(user))
                {
                    await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow);
                    _logger.LogInformation("User {UserId} lockout cleared after password reset.", user.Id);
                }

                TempData["SuccessMessage"] = "Your password has been changed successfully. Please log in with your new password.";
                _logger.LogInformation("Password for user {UserId} reset successfully.", user.Id);
                return RedirectToAction("Login", "Account");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            _logger.LogWarning("Password reset failed for user {Email} via ResetPasswordDirect. Errors: {Errors}", model.Email, string.Join("; ", result.Errors.Select(e => e.Description)));
            return View(model);
        }

        [HttpGet]
        public IActionResult ResetPassword(string token, string email)
        {
            if (token == null || email == null)
            {
                ModelState.AddModelError("", "Invalid password reset token or email.");
                _logger.LogWarning("ResetPassword (GET) called with invalid token or email.");
            }
            _logger.LogInformation("Accessed ResetPassword (GET) with token and email.");
            return View(new ResetPasswordViewModel { Token = token, Email = email });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ResetPassword (POST) failed due to invalid model state for email: {Email}", model.Email);
                return View(model);
            }

            var foundUser = await _userManager.FindByEmailAsync(model.Email);
            if (foundUser == null)
            {
                ModelState.AddModelError("", "User not found.");
                _logger.LogWarning("ResetPassword (POST) failed: User not found for email: {Email}", model.Email);
                return View(model);
            }

            var result = await _userManager.ResetPasswordAsync(foundUser, model.Token, model.Password);

            if (result.Succeeded)
            {
                // Clear the ForcePasswordChange flag if it was set
                if (foundUser.ForcePasswordChange)
                {
                    foundUser.ForcePasswordChange = false;
                    await _userManager.UpdateAsync(foundUser);
                    _logger.LogInformation("ForcePasswordChange flag cleared for user {UserId} after password reset.", foundUser.Id);
                }

                TempData["SuccessMessage"] = "Your password has been reset successfully. Please log in with your new password.";
                _logger.LogInformation("Password for user {UserId} reset successfully via token.", foundUser.Id);
                return RedirectToAction(nameof(Login));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            _logger.LogWarning("Password reset failed for user {Email} via token. Errors: {Errors}", model.Email, string.Join("; ", result.Errors.Select(e => e.Description)));
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    if (roles.Contains("Customer"))
                    {
                        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == user.Id);
                        if (customer != null)
                        {
                            await _activityService.LogActivityAsync(
                                customer.CustomerId,
                                "Logout",
                                "Customer logged out of the system",
                                null,
                                null,
                                null
                            );
                        }
                    }
                }
            }
            catch (Exception activityEx)
            {
                _logger.LogError(activityEx, "Failed to log logout activity");
            }

            await _signInManager.SignOutAsync();
            TempData["LogoutMessage"] = "You have been successfully logged out.";
            _logger.LogInformation("User logged out successfully.");
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            _logger.LogWarning("Access Denied page accessed.");
            return View();
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> ClearPasswordChangeFlag()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null && user.ForcePasswordChange)
                {
                    user.ForcePasswordChange = false;
                    await _userManager.UpdateAsync(user);
                    TempData["SuccessMessage"] = "Password change flag cleared. You can now access all features.";
                    _logger.LogInformation("ForcePasswordChange flag cleared for user {UserId}", user.Id);
                }
                else
                {
                    TempData["InfoMessage"] = "No password change flag to clear.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing password change flag for user {UserId}", User.FindFirstValue(ClaimTypes.NameIdentifier));
                TempData["ErrorMessage"] = "An error occurred while clearing the password change flag.";
            }
            
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> Profile()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser == null)
            {
                _logger.LogWarning("Profile (GET) accessed by unauthenticated user.");
                return RedirectToAction("Login", "Account");
            }

            var customer = await _context.Customers
                .Include(c => c.Allergies)
                    .ThenInclude(a => a.ActiveIngredient)
                .Include(c => c.MedicalInfo)
                .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

            if (customer == null)
            {
                _logger.LogWarning("Customer record not found for user ID: {UserId}", currentUser.Id);
                return RedirectToAction("Login", "Account");
            }
            _logger.LogInformation("Accessed Profile (GET) for customer {CustomerEmail}.");

            var activeIngredients = await _context.ActiveIngredients
                .OrderBy(ai => ai.Name)
                .ToListAsync();

            var model = new ProfileViewModel
            {
                Name = currentUser.FullName,
                Email = currentUser.Email,
                IDNumber = customer.IDNumber,
                CellPhoneNumber = customer.CellPhoneNumber,
                Street = customer.Street,
                Suburb = customer.Suburb,
                City = customer.City,
                Province = customer.Province,
                ZipCode = customer.ZipCode,
                Country = customer.Country,
                DateCreated = customer.DateCreated,
                ProfileImagePath = customer.ProfileImagePath,
                FullAddress = "",
                Allergies = customer.Allergies?.ToList() ?? new List<CustomerAllergy>(),
                ChronicConditions = customer.MedicalInfo?.ChronicConditions,
                MedicalNotes = customer.MedicalInfo?.MedicalNotes,
                EmergencyContactName = customer.MedicalInfo?.EmergencyContactName,
                EmergencyContactPhone = customer.MedicalInfo?.EmergencyContactPhone,
                BloodType = customer.MedicalInfo?.BloodType,
                MedicalAidNumber = customer.MedicalInfo?.MedicalAidNumber,
                MedicalAidScheme = customer.MedicalInfo?.MedicalAidScheme,
                
                // Allergy management properties
                CustomerId = customer.CustomerId,
                AvailableActiveIngredients = activeIngredients
            };

            model.FullAddress = model.GenerateFullAddress();
            return View(model);
        }

        // POST: Customer Change Password (match Pharmacy Manager implementation)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> ChangePassword(string OldPassword, string NewPassword, string ConfirmNewPassword)
        {
            if (string.IsNullOrWhiteSpace(OldPassword)) ModelState.AddModelError("OldPassword", "Current password is required.");
            if (string.IsNullOrWhiteSpace(NewPassword)) ModelState.AddModelError("NewPassword", "New password is required.");
            if (NewPassword != ConfirmNewPassword) ModelState.AddModelError("ConfirmNewPassword", "Password does not match.");

            if (!ModelState.IsValid) return RedirectToAction(nameof(Profile));

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var changePasswordResult = await _userManager.ChangePasswordAsync(user, OldPassword, NewPassword);
            if (!changePasswordResult.Succeeded)
            {
                TempData["ErrorMessage"] = string.Join("; ", changePasswordResult.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Profile));
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["SuccessMessage"] = "Password changed successfully.";
            return RedirectToAction(nameof(Profile));
        }

        [HttpGet]
        public IActionResult RegisterWalkInCustomer()
        {
            _logger.LogInformation("Accessed RegisterWalkInCustomer (GET) page.");
            return View(new RegisterWalkInCustomerViewModel
            {
                Country = "South Africa" // Set default country
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterWalkInCustomer(RegisterWalkInCustomerViewModel model)
        {
            try
            {
                // Check if ID number already exists across all entities (only if provided)
                if (!string.IsNullOrWhiteSpace(model.IDNumber))
                {
                    bool idExistsInCustomers = await _context.Customers.AnyAsync(c => c.IDNumber == model.IDNumber);
                    bool idExistsInPharmacists = await _context.Pharmacists.AnyAsync(p => p.IDNumber == model.IDNumber);
                    
                    if (idExistsInCustomers || idExistsInPharmacists)
                    {
                        ModelState.AddModelError("IDNumber", "This ID number is already registered. Please use a unique ID number.");
                        _logger.LogWarning("Walk-in customer registration (POST) failed - duplicate ID number: {IDNumber}", model.IDNumber);
                        return View(model);
                    }
                }

                // Generate email if not provided
                var email = string.IsNullOrWhiteSpace(model.Email)
                    ? $"{model.Name.ToLower()}.{model.Surname.ToLower()}{DateTime.Now:yyyyMMdd}@walkin.ibhayi"
                    : model.Email.Trim();

                // Create Identity user
                var user = new Users
                {
                    FullName = $"{model.Name} {model.Surname}",
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true
                };

                // Generate random password
                var password = GenerateTemporaryPassword();
                var userResult = await _userManager.CreateAsync(user, password);

                if (!userResult.Succeeded)
                {
                    foreach (var error in userResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    _logger.LogWarning("Walk-in customer registration (POST) failed for email: {Email}. Errors: {Errors}", email, string.Join("; ", userResult.Errors.Select(e => e.Description)));
                    return View(model);
                }

                // Ensure Customer role exists
                if (!await _roleManager.RoleExistsAsync("Customer"))
                {
                    await _roleManager.CreateAsync(new IdentityRole("Customer"));
                }

                // Add to Customer role
                await _userManager.AddToRoleAsync(user, "Customer");

                // Create Customer record
                var customer = new Customer
                {
                    UserId = user.Id,
                    Name = model.Name,
                    Surname = model.Surname,
                    IDNumber = model.IDNumber ?? "N/A",
                    CellPhoneNumber = model.CellPhoneNumber ?? "N/A",
                    Email = email,
                    Street = model.Street,
                    Suburb = model.Suburb,
                    City = model.City,
                    Province = model.Province,
                    ZipCode = model.ZipCode,
                    Country = model.Country ?? "South Africa",
                    ProfileImagePath = "/images/default-profile.png",
                    DateCreated = DateTime.Now,
                    IsWalkInCustomer = true
                };

                // Save to database
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();

                // Handle allergies if provided
                var allergies = Request.Form["allergies"].ToString();
                if (!string.IsNullOrEmpty(allergies))
                {
                    var allergyList = allergies.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var allergy in allergyList)
                    {
                        var trimmedAllergy = allergy.Trim();
                        if (!string.IsNullOrEmpty(trimmedAllergy))
                        {
                            // Find or create active ingredient
                            var activeIngredient = await _context.ActiveIngredients
                                .FirstOrDefaultAsync(ai => ai.Name.ToLower() == trimmedAllergy.ToLower());

                            if (activeIngredient == null)
                            {
                                activeIngredient = new ActiveIngredients
                                {
                                    Name = trimmedAllergy,
                                    Description = $"Auto-created from customer registration",
                                    Strength = "N/A"
                                };
                                _context.ActiveIngredients.Add(activeIngredient);
                                await _context.SaveChangesAsync();
                            }

                            // Create customer allergy
                            var customerAllergy = new CustomerAllergy
                            {
                                CustomerId = customer.CustomerId,
                                ActiveIngredientId = activeIngredient.ActiveIngredientId,
                                Severity = "Moderate", // Default severity
                                Description = $"Allergy to {trimmedAllergy}"
                            };
                            _context.CustomerAllergies.Add(customerAllergy);
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                // Store the success message in TempData
                TempData["SuccessMessage"] = $"Walk-in customer registered successfully!<br>" +
                                            $"<strong>Username:</strong> {email}<br>" +
                                            $"<strong>Temporary Password:</strong> {password}";

                // Ensure TempData is preserved for the next request
                TempData.Keep("SuccessMessage");

                // Redirect to CustomerDetails
                _logger.LogInformation("Walk-in customer {Email} registered successfully. Customer ID: {CustomerId}", email, customer.CustomerId);
                return RedirectToAction("CustomerDetails", "Account", new { id = customer.CustomerId });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error registering walk-in customer");
                TempData["ErrorMessage"] = "An error occurred while registering the customer. Please try again.";
                return View(model);
            }
        }

        private string GenerateTemporaryPassword()
        {
            const string Chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#$%^&*";
            var random = new Random();
            return new string(Enumerable.Repeat(Chars, 12)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private IActionResult? RedirectToLocal(string? returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return null;
        }


        [HttpGet]
        public async Task<IActionResult> CustomerDetails(int id)
        {
            try
            {
                // Get the customer with all related data
                var customer = await _context.Customers
                    .Include(c => c.User)
                    .Include(c => c.Allergies)
                        .ThenInclude(a => a.ActiveIngredient)
                    .Include(c => c.Prescriptions)
                        .ThenInclude(p => p.PrescriptionLines)
                            .ThenInclude(pl => pl.Medication)
                    .FirstOrDefaultAsync(c => c.CustomerId == id);

                if (customer == null)
                {
                    TempData["ErrorMessage"] = "Customer not found.";
                    _logger.LogWarning("Customer with ID {CustomerId} not found for CustomerDetails (GET).");
                    return RedirectToAction("CustomerList");
                }

                // Get dropdown data
                var activeIngredients = await _context.ActiveIngredients.ToListAsync();
                var medications = await _context.Medications.OrderBy(m => m.Name).ToListAsync();

                // Map to ViewModel
                var viewModel = new CustomerDetailsViewModel
                {
                    CustomerId = customer.CustomerId,
                    FullName = $"{customer.Name} {customer.Surname}",
                    Email = customer.Email,
                    IDNumber = customer.IDNumber,
                    CellPhoneNumber = customer.CellPhoneNumber,
                    ProfileImagePath = customer.ProfileImagePath,
                    DateCreated = customer.DateCreated,
                    IsWalkInCustomer = customer.IsWalkInCustomer,
                    Street = customer.Street,
                    Suburb = customer.Suburb,
                    City = customer.City,
                    Province = customer.Province,
                    ZipCode = customer.ZipCode,
                    Country = customer.Country,
                    Allergies = customer.Allergies?.ToList() ?? new List<CustomerAllergy>(),
                    Prescriptions = customer.Prescriptions?.ToList() ?? new List<Prescription>(),
                    ActiveIngredients = activeIngredients,
                    Medications = medications
                };

                viewModel.FullAddress = viewModel.GenerateFullAddress();
                _logger.LogInformation("Accessed Customer Details (GET) for customer ID: {CustomerId}.", id);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer details for Customer ID: {CustomerId}", id);
                TempData["ErrorMessage"] = "An error occurred while retrieving customer details.";
                return RedirectToAction("CustomerList");
            }
        }


        [HttpGet]
        public async Task<IActionResult> CustomerList()
        {
            try
            {
                var customers = await _context.Customers
                    .Include(c => c.User)
                    .OrderByDescending(c => c.DateCreated)
                    .Select(c => new CustomerDetailsViewModel // Or create a CustomerListViewModel if different
                    {
                        CustomerId = c.CustomerId,
                        FullName = $"{c.Name} {c.Surname}",
                        Email = c.Email,
                        CellPhoneNumber = c.CellPhoneNumber,
                        DateCreated = c.DateCreated,
                        IsWalkInCustomer = c.IsWalkInCustomer
                    })
                    .ToListAsync();
                _logger.LogInformation("Accessed CustomerList (GET) page. Retrieved {Count} customers.", customers.Count);
                return View(customers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer list");
                TempData["ErrorMessage"] = "An error occurred while retrieving the customer list.";
                return View(new List<CustomerDetailsViewModel>());
            }
        }

        // POST: Update Profile Information
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> UpdateProfile()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("UpdateProfile (POST) attempted by unauthenticated user.");
                    return Json(new { success = false, message = "User not authenticated" });
                }

                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.UserId == user.Id);

                if (customer == null)
                {
                    _logger.LogWarning("Customer record not found for user ID: {UserId} during profile update.", user.Id);
                    return Json(new { success = false, message = "Customer not found" });
                }

                // Update customer information
                var fullName = Request.Form["fullName"].ToString();
                var email = Request.Form["email"].ToString();
                var phoneNumber = Request.Form["phoneNumber"].ToString();
                var street = Request.Form["street"].ToString();
                var suburb = Request.Form["suburb"].ToString();
                var city = Request.Form["city"].ToString();
                var province = Request.Form["province"].ToString();
                var zipCode = Request.Form["zipCode"].ToString();

                // Update user full name and email
                user.FullName = fullName;
                if (user.Email != email)
                {
                    user.Email = email;
                    user.UserName = email; // Update username as well
                }
                await _userManager.UpdateAsync(user);

                // Update customer information
                customer.Email = email;
                customer.CellPhoneNumber = phoneNumber;
                customer.Street = street;
                customer.Suburb = suburb;
                customer.City = city;
                customer.Province = province;
                customer.ZipCode = zipCode;

                _context.Update(customer);
                await _context.SaveChangesAsync();

                // Log profile update activity
                try
                {
                    await _activityService.LogActivityAsync(
                        customer.CustomerId,
                        "ProfileUpdated",
                        $"Updated profile information: Name, Email, Phone, and Address",
                        "Customer",
                        customer.CustomerId,
                        null
                    );
                }
                catch (Exception activityEx)
                {
                    _logger.LogError(activityEx, "Failed to log profile update activity");
                }

                // Generate new full address
                var fullAddress = GenerateFullAddress(street, suburb, city, province, zipCode, customer.Country);
                var initials = GetInitials(fullName);

                _logger.LogInformation("Profile updated successfully for user {UserId}", user.Id);
                return Json(new {
                    success = true,
                    message = "Profile updated successfully",
                    data = new {
                        fullName = fullName,
                        email = email,
                        phoneNumber = phoneNumber,
                        fullAddress = fullAddress,
                        initials = initials
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile for user {UserId}", User.FindFirstValue(ClaimTypes.NameIdentifier));
                return Json(new { success = false, message = "An error occurred while updating profile" });
            }
        }

        // POST: Update Medical Information
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> UpdateMedicalInfo()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("UpdateMedicalInfo (POST) attempted by unauthenticated user.");
                    return Json(new { success = false, message = "User not authenticated" });
                }

                var customer = await _context.Customers
                    .Include(c => c.MedicalInfo)
                    .FirstOrDefaultAsync(c => c.UserId == user.Id);

                if (customer == null)
                {
                    _logger.LogWarning("Customer record not found for user ID: {UserId} during medical info update.", user.Id);
                    return Json(new { success = false, message = "Customer not found" });
                }

                // Get form data
                var chronicConditions = Request.Form["chronicConditions"].ToString();
                var medicalNotes = Request.Form["medicalNotes"].ToString();
                var emergencyContactName = Request.Form["emergencyContactName"].ToString();
                var emergencyContactPhone = Request.Form["emergencyContactPhone"].ToString();
                var bloodType = Request.Form["bloodType"].ToString();
                var medicalAidNumber = Request.Form["medicalAidNumber"].ToString();
                var medicalAidScheme = Request.Form["medicalAidScheme"].ToString();

                // Create or update medical info
                if (customer.MedicalInfo == null)
                {
                    customer.MedicalInfo = new MedicalInfo
                    {
                        CustomerId = customer.CustomerId,
                        ChronicConditions = chronicConditions,
                        MedicalNotes = medicalNotes,
                        EmergencyContactName = emergencyContactName,
                        EmergencyContactPhone = emergencyContactPhone,
                        BloodType = bloodType,
                        MedicalAidNumber = medicalAidNumber,
                        MedicalAidScheme = medicalAidScheme,
                        DateCreated = DateTime.Now,
                        LastUpdated = DateTime.Now
                    };
                    _context.MedicalInfos.Add(customer.MedicalInfo);
                }
                else
                {
                    customer.MedicalInfo.ChronicConditions = chronicConditions;
                    customer.MedicalInfo.MedicalNotes = medicalNotes;
                    customer.MedicalInfo.EmergencyContactName = emergencyContactName;
                    customer.MedicalInfo.EmergencyContactPhone = emergencyContactPhone;
                    customer.MedicalInfo.BloodType = bloodType;
                    customer.MedicalInfo.MedicalAidNumber = medicalAidNumber;
                    customer.MedicalInfo.MedicalAidScheme = medicalAidScheme;
                    customer.MedicalInfo.LastUpdated = DateTime.Now;
                    _context.MedicalInfos.Update(customer.MedicalInfo);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Medical information updated successfully for user {UserId}", user.Id);
                return Json(new {
                    success = true,
                    message = "Medical information updated successfully",
                    data = new {
                        chronicConditions = chronicConditions,
                        medicalNotes = medicalNotes,
                        emergencyContactName = emergencyContactName,
                        emergencyContactPhone = emergencyContactPhone,
                        bloodType = bloodType,
                        medicalAidNumber = medicalAidNumber,
                        medicalAidScheme = medicalAidScheme
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating medical information for user {UserId}", User.FindFirstValue(ClaimTypes.NameIdentifier));
                return Json(new { success = false, message = "An error occurred while updating medical information" });
            }
        }

        // POST: Change Password
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmNewPassword(ChangePasswordViewModel model)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    // For unauthorized access during password change, redirect to login
                    _logger.LogWarning("ConfirmNewPassword (POST) attempted by unauthenticated user. Redirecting to Login.");
                    return RedirectToAction("Login", "Account");
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    TempData["ErrorMessage"] = $"Validation failed: {string.Join(", ", errors)}";
                    _logger.LogWarning("ConfirmNewPassword (POST) failed for user {UserId} due to invalid model state. Errors: {Errors}", user.Id, string.Join("; ", errors));
                    return View("ChangePassword", model);
                }

                IdentityResult result;

                if (model.RequiresOldPassword)
                {
                    // Regular password change - requires current password
                    var currentPassword = Request.Form["currentPassword"].ToString();
                    if (string.IsNullOrEmpty(currentPassword))
                    {
                        ModelState.AddModelError(string.Empty, "Current password is required");
                        return View("ChangePassword", model);
                    }
                    
                    // Verify current password
                    var isCurrentPasswordValid = await _userManager.CheckPasswordAsync(user, currentPassword);
                    if (!isCurrentPasswordValid)
                    {
                        ModelState.AddModelError(string.Empty, "Current password is incorrect");
                        _logger.LogWarning("ConfirmNewPassword (POST) failed for user {UserId}: Incorrect current password.", user.Id);
                        return View("ChangePassword", model);
                    }
                    result = await _userManager.ChangePasswordAsync(user, currentPassword, model.NewPassword);
                }
                else
                {
                    // Forced password change - no current password needed
                    _logger.LogInformation("Processing forced password change for user {UserId}", user.Id);
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
                }

                if (result.Succeeded)
                {
                    // Clear the ForcePasswordChange flag for both regular and forced password changes
                    if (user.ForcePasswordChange)
                    {
                        user.ForcePasswordChange = false;
                        await _userManager.UpdateAsync(user);
                        _logger.LogInformation("ForcePasswordChange flag cleared for user {UserId} after successful password change.", user.Id);
                    }

                    if (model.RequiresOldPassword)
                    {
                        // For regular password change, sign out and redirect to login
                        await _signInManager.SignOutAsync(); // Sign out the user after successful password change
                        TempData["SuccessMessage"] = "Your password has been changed successfully. Please log in with your new password.";
                        _logger.LogInformation("Password for user {UserId} changed successfully. User signed out.", user.Id);
                        return RedirectToAction("Login", "Account");
                    }
                    else
                    {
                        // For forced password change (new pharmacist), sign them in directly
                        await _signInManager.SignInAsync(user, isPersistent: false); // Sign them in directly after forced change
                        TempData["SuccessMessage"] = "Your password has been set successfully. You are now logged in.";
                        _logger.LogInformation("Password for user {UserId} set successfully (forced change). User logged in.", user.Id);

                        // Determine redirection based on role (similar to Login action)
                        var roles = await _userManager.GetRolesAsync(user);
                        if (roles.Contains("Admin"))
                            return RedirectToAction("Admin", "Home");
                        else if (roles.Contains("PharmacyManager"))
                            return RedirectToAction("PharmacyManager", "Home");
                        else if (roles.Contains("Pharmacist"))
                            return RedirectToAction("Pharmacist", "Home");
                        else if (roles.Contains("Customer"))
                            return RedirectToAction("Customer", "Home");

                        return RedirectToAction("Index", "Home");
                    }
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    _logger.LogWarning("Password change failed for user {UserId}. Errors: {Errors}", user.Id, string.Join("; ", result.Errors.Select(e => e.Description)));
                    return View("ChangePassword", model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user {UserId}", User.FindFirstValue(ClaimTypes.NameIdentifier));
                ModelState.AddModelError(string.Empty, "An error occurred while changing password");
                return View("ChangePassword", model);
            }
        }

        // POST: Toggle Two-Factor Authentication
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> ToggleTwoFactor()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("ToggleTwoFactor (POST) attempted by unauthenticated user.");
                    return Json(new { success = false, message = "User not authenticated" });
                }

                var enabled = Request.Form["enabled"].ToString().ToLower() == "true";

                // Toggle two-factor authentication
                var result = await _userManager.SetTwoFactorEnabledAsync(user, enabled);
                if (result.Succeeded)
                {
                    var action = enabled ? "enabled" : "disabled";
                    _logger.LogInformation("Two-factor authentication {Action} successfully for user {UserId}.", action, user.Id);
                    return Json(new { success = true, message = $"Two-factor authentication {action} successfully" });
                }
                else
                {
                    var action = enabled ? "enable" : "disable";
                    _logger.LogWarning("Failed to {Action} two-factor authentication for user {UserId}. Errors: {Errors}", action, user.Id, string.Join("; ", result.Errors.Select(e => e.Description)));
                    return Json(new { success = false, message = $"Failed to {action} two-factor authentication" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling two-factor authentication for user {UserId}", User.FindFirstValue(ClaimTypes.NameIdentifier));
                return Json(new { success = false, message = "An error occurred while toggling two-factor authentication" });
            }
        }

        // POST: Toggle Login Notifications
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> ToggleLoginNotifications()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("ToggleLoginNotifications (POST) attempted by unauthenticated user.");
                    return Json(new { success = false, message = "User not authenticated" });
                }

                var enabled = Request.Form["enabled"].ToString().ToLower() == "true";

                // This is a placeholder implementation
                // You would typically store this preference in a user settings table
                // For now, we'll just return success

                var action = enabled ? "enabled" : "disabled";
                _logger.LogInformation("Login notifications {Action} successfully for user {UserId}.", action, user.Id);
                return Json(new { success = true, message = $"Login notifications {action} successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling login notifications for user {UserId}", User.FindFirstValue(ClaimTypes.NameIdentifier));
                return Json(new { success = false, message = "An error occurred while toggling login notifications" });
            }
        }

        // Helper method to generate full address
        private string GenerateFullAddress(string street, string suburb, string city, string province, string zipCode, string country)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(street)) parts.Add($"Street: {street}");
            if (!string.IsNullOrEmpty(suburb)) parts.Add($"Suburb: {suburb}");

            var cityParts = new List<string>();
            if (!string.IsNullOrEmpty(city)) cityParts.Add(city);
            if (!string.IsNullOrEmpty(province)) cityParts.Add(province);
            if (!string.IsNullOrEmpty(zipCode)) cityParts.Add(zipCode);

            if (cityParts.Any()) parts.Add($"Location: {string.Join(", ", cityParts)}");
            if (!string.IsNullOrEmpty(country)) parts.Add($"Country: {country}");

            return string.Join(Environment.NewLine, parts);
        }

        // Helper method to get initials
        private string GetInitials(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return "U";

            var names = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (names.Length == 0)
                return "U";

            if (names.Length == 1)
                return names[0].Substring(0, Math.Min(2, names[0].Length)).ToUpper();

            return (names[0].Substring(0, 1) + names[names.Length - 1].Substring(0, 1)).ToUpper();
        }

        // GET: Get Active Ingredients for allergies
        [HttpGet]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> GetActiveIngredients()
        {
            try
            {
                var ingredients = await _context.ActiveIngredients
                    .OrderBy(ai => ai.Name)
                    .Select(ai => new {
                        activeIngredientId = ai.ActiveIngredientId,
                        name = ai.Name
                    })
                    .ToListAsync();

                return Json(ingredients);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active ingredients");
                return Json(new List<object>());
            }
        }

        // POST: Update Allergies
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> UpdateAllergies()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not authenticated" });
                }

                var customer = await _context.Customers
                    .Include(c => c.Allergies)
                    .FirstOrDefaultAsync(c => c.UserId == user.Id);

                if (customer == null)
                {
                    return Json(new { success = false, message = "Customer not found" });
                }

                _logger.LogInformation("Processing allergies update for customer {CustomerId}", customer.CustomerId);

                // Parse JSON data from form
                var allergyDataJson = Request.Form["AllergyData"].ToString();
                var customAllergyDataJson = Request.Form["CustomAllergyData"].ToString();
                var removedAllergyIdsJson = Request.Form["RemovedAllergyIds"].ToString();

                var allergyData = new List<JsonElement>();
                var customAllergyData = new List<JsonElement>();
                var removedAllergyIds = new List<int>();

                if (!string.IsNullOrEmpty(allergyDataJson))
                {
                    allergyData = JsonSerializer.Deserialize<List<JsonElement>>(allergyDataJson) ?? new List<JsonElement>();
                }

                if (!string.IsNullOrEmpty(customAllergyDataJson))
                {
                    customAllergyData = JsonSerializer.Deserialize<List<JsonElement>>(customAllergyDataJson) ?? new List<JsonElement>();
                }

                if (!string.IsNullOrEmpty(removedAllergyIdsJson))
                {
                    removedAllergyIds = System.Text.Json.JsonSerializer.Deserialize<List<int>>(removedAllergyIdsJson);
                }

                // Remove allergies that are marked for deletion
                if (removedAllergyIds.Any())
                {
                    var allergiesToRemove = customer.Allergies
                        .Where(ca => removedAllergyIds.Contains(ca.AllergyId))
                        .ToList();

                    foreach (var allergy in allergiesToRemove)
                    {
                        _context.CustomerAllergies.Remove(allergy);
                        _logger.LogInformation("Removed allergy {AllergyId} for ingredient {IngredientId} during update.", allergy.AllergyId, allergy.ActiveIngredientId);
                    }
                }

                // Update existing allergies
                foreach (var allergy in allergyData)
                {
                    // Handle both string and int values properly, and tolerate missing keys
                    JsonElement allergyIdElement;
                    JsonElement ingredientIdElement;
                    
                    int allergyId = 0;
                    int ingredientId = 0;
                    
                    if (allergy.TryGetProperty("allergyId", out allergyIdElement))
                    {
                        if (allergyIdElement.ValueKind == JsonValueKind.Number)
                        {
                            allergyId = allergyIdElement.GetInt32();
                        }
                        else if (allergyIdElement.ValueKind == JsonValueKind.String)
                        {
                            int.TryParse(allergyIdElement.GetString(), out allergyId);
                        }
                    }

                    if (allergy.TryGetProperty("ingredientId", out ingredientIdElement))
                    {
                        if (ingredientIdElement.ValueKind == JsonValueKind.Number)
                        {
                            ingredientId = ingredientIdElement.GetInt32();
                        }
                        else if (ingredientIdElement.ValueKind == JsonValueKind.String)
                        {
                            int.TryParse(ingredientIdElement.GetString(), out ingredientId);
                        }
                    }

                    string? severity = null;
                    string? description = null;
                    if (allergy.TryGetProperty("severity", out var severityEl))
                    {
                        severity = severityEl.GetString();
                    }
                    if (allergy.TryGetProperty("description", out var descEl))
                    {
                        description = descEl.GetString();
                    }

                    var existingAllergy = customer.Allergies
                        .FirstOrDefault(ca => ca.AllergyId == allergyId);

                    if (existingAllergy != null)
                    {
                        existingAllergy.Severity = severity;
                        existingAllergy.Description = description ?? "";
                        // Don't call Update() on a tracked entity - just modify properties and EF will track changes
                        _logger.LogInformation("Updated existing allergy {AllergyId} for ingredient {IngredientId}", allergyId, ingredientId);
                    }
                    else
                    {
                        // Add new allergy
                        _context.CustomerAllergies.Add(new CustomerAllergy
                        {
                            CustomerId = customer.CustomerId,
                            ActiveIngredientId = ingredientId,
                            Severity = severity,
                            Description = description ?? ""
                        });
                        _logger.LogInformation("Added new allergy for ingredient {IngredientId}", ingredientId);
                    }
                }

                // Handle custom allergies
                if (customAllergyData.Any())
                {
                    var customIngredient = await _context.ActiveIngredients.FirstOrDefaultAsync(ai => ai.Name == "Custom");
                    if (customIngredient == null)
                    {
                        customIngredient = new ActiveIngredients
                        {
                            Name = "Custom",
                            Description = "Custom allergen not in the standard database",
                            Strength = "N/A"
                        };
                        _context.ActiveIngredients.Add(customIngredient);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Created new 'Custom' active ingredient for custom allergies.");
                    }

                foreach (var customAllergy in customAllergyData)
                    {
                    string? customName = null;
                    string? severity = null;
                    string? description = null;
                    if (customAllergy.TryGetProperty("name", out var nameEl)) customName = nameEl.GetString();
                    if (customAllergy.TryGetProperty("severity", out var sevEl)) severity = sevEl.GetString();
                    if (customAllergy.TryGetProperty("description", out var desEl)) description = desEl.GetString();
                        var fullDescription = $"CUSTOM_ALLERGEN: {customName} - {description ?? ""}";

                        // Check if a similar custom allergy already exists for this customer
                        var existingCustomAllergy = customer.Allergies
                            .FirstOrDefault(ca => ca.ActiveIngredientId == customIngredient.ActiveIngredientId && ca.Description == fullDescription);

                        if (existingCustomAllergy == null)
                        {
                            _context.CustomerAllergies.Add(new CustomerAllergy
                            {
                                CustomerId = customer.CustomerId,
                                ActiveIngredientId = customIngredient.ActiveIngredientId,
                                Severity = severity ?? "Moderate",
                                Description = fullDescription
                            });
                            _logger.LogInformation("Added new custom allergy for name {CustomName}", (string)(customName ?? "Unknown"));
                        }
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully saved allergies to database");

                // Log activity
                try
                {
                    var changesSummary = $"{allergyData.Count} allergy(-ies) updated, {removedAllergyIds.Count} removed, {customAllergyData.Count} custom allergy(-ies) added";
                    await _activityService.LogActivityAsync(
                        customer.CustomerId,
                        "ProfileUpdated",
                        $"Updated allergy information: {changesSummary}",
                        "CustomerAllergy",
                        null,
                        null
                    );
                }
                catch (Exception activityEx)
                {
                    _logger.LogError(activityEx, "Failed to log activity for allergy update");
                }

                // Return updated allergies for display
                var updatedAllergies = await _context.CustomerAllergies
                    .Where(ca => ca.CustomerId == customer.CustomerId)
                    .Include(ca => ca.ActiveIngredient)
                    .Select(ca => new
                    {
                        allergyId = ca.AllergyId,
                        activeIngredientId = ca.ActiveIngredientId,
                        activeIngredientName = ca.ActiveIngredient.Name,
                        severity = ca.Severity,
                        description = ca.Description
                    })
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} updated allergies for display", updatedAllergies.Count);

                return Json(new {
                    success = true,
                    message = "Allergies updated successfully",
                    data = updatedAllergies
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating allergies");
                return Json(new { success = false, message = "An error occurred while updating allergies: " + ex.Message });
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult RegisterPharmacyManager()
        {
            ViewBag.Pharmacies = _context.Pharmacies.ToList();
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterPharmacyManager(RegisterPharmacyManagerViewModel model)
        {
            ViewBag.Pharmacies = _context.Pharmacies.ToList(); // Repopulate pharmacies on POST back

            if (ModelState.IsValid)
            {
                var user = new Users { UserName = model.Email, Email = model.Email, FullName = $"{model.Name} {model.Surname}" };
                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "PharmacyManager");

                    var pharmacyManager = new PharmacyManager
                    {
                        UserId = user.Id,
                        Name = model.Name,
                        Surname = model.Surname,
                        BranchName = model.BranchName,
                        ContactNumber = model.ContactNumber,
                        Email = model.Email,
                        IsActive = true,
                        PharmacyId = model.PharmacyId
                    };

                    _context.PharmacyManagers.Add(pharmacyManager);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Admin created a new Pharmacy Manager account with password.");
                    TempData["SuccessMessage"] = "Pharmacy Manager registered successfully!";
                    return RedirectToAction(nameof(RegisterPharmacyManager));
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }

      
}
}