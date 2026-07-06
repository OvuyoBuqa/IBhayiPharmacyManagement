using IBhayiPharmacyManagementSystem.Data;
using IBhayiPharmacyManagementSystem.Models;
using IBhayiPharmacyManagementSystem.ViewModels;
using IBhayiPharmacyManagementSystem.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;

namespace IBhayiPharmacyManagementSystem.Controllers
{
    [Authorize(Roles = "Pharmacist,PharmacyManager")]
    public class WalkInCustomersController : Controller
    {
        private readonly SignInManager<Users> _signInManager;
        private readonly UserManager<Users> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _context;

        public WalkInCustomersController(
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

        // GET: WalkInCustomers
        public async Task<IActionResult> Index()
        {
            var viewModel = new WalkInCustomerViewModel();

            // Populate customer dropdown
            viewModel.Customers = await _context.Customers
                .Select(c => new SelectListItem
                {
                    Value = c.CustomerId.ToString(),
                    Text = $"{c.Name} {c.Surname} ({c.IDNumber})"
                })
                .ToListAsync();


            // Populate active ingredients for allergies
            viewModel.ActiveIngredients = await _context.ActiveIngredients.ToListAsync();

            // Populate medications for prescription items
            ViewBag.Medications = await _context.Medications.OrderBy(m => m.Name).ToListAsync();

            return View(viewModel);
        }

        // GET: WalkInCustomers/SearchExistingPrescriptions
        [HttpGet]
        public async Task<IActionResult> SearchExistingPrescriptions()
        {
            // Provide medications for add-item dropdown on the page
            ViewBag.Medications = await _context.Medications
                .OrderBy(m => m.Name)
                .Select(m => new { m.MedicationId, m.Name })
                .ToListAsync();
            return View();
        }

        // AJAX: Search prescriptions by customer name or ID
        [HttpGet]
        public async Task<IActionResult> SearchPrescriptions(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return Json(new { success = false, message = "Please enter a search term" });
            }

            try
            {
                var prescriptions = await _context.Prescriptions
                    .Include(p => p.Customer)
                    .Include(p => p.PrescriptionLines)
                        .ThenInclude(pl => pl.Medication)
                    .Where(p => p.Customer.Name.Contains(searchTerm) || 
                               p.Customer.Surname.Contains(searchTerm) ||
                               p.Customer.IDNumber.Contains(searchTerm) ||
                               (p.Customer.Name + " " + p.Customer.Surname).Contains(searchTerm))
                    .OrderByDescending(p => p.PrescriptionDate)
                    .Take(10) // Limit to 10 most recent results
                    .Select(p => new
                    {
                        prescriptionId = p.PrescriptionId,
                        customerId = p.CustomerId,
                        customerName = $"{p.Customer.Name} {p.Customer.Surname}",
                        customerIdNumber = p.Customer.IDNumber,
                        prescriptionDate = p.PrescriptionDate.ToString("yyyy-MM-dd"),
                        medicationCount = p.PrescriptionLines.Count,
                        medications = p.PrescriptionLines.Select(pl => new
                        {
                            prescriptionLineId = pl.PrescriptionLineId,
                            medicationId = pl.MedicationId,
                            medicationName = pl.Medication.Name,
                            quantity = pl.Quantity,
                            instructions = pl.Instructions,
                            frequency = pl.Frequency.ToString(),
                            repeats = pl.TotalRepeats
                        }).ToList()
                    })
                    .ToListAsync();

                return Json(new { success = true, prescriptions = prescriptions });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error searching prescriptions: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while searching prescriptions" });
            }
        }

        // POST: Create a prescription from items (no file upload)
        [HttpPost]
        public async Task<IActionResult> SubmitRePrescription(int customerId, string itemsJson)
        {
            if (customerId <= 0)
            {
                return Json(new { success = false, message = "Customer is required." });
            }
            if (string.IsNullOrWhiteSpace(itemsJson))
            {
                return Json(new { success = false, message = "Please add at least one item." });
            }

            try
            {
                Console.WriteLine($"Received itemsJson: {itemsJson}");
                var items = JsonSerializer.Deserialize<List<PrescriptionItemViewModel>>(itemsJson) ?? new List<PrescriptionItemViewModel>();
                Console.WriteLine($"Deserialized {items.Count} items");
                if (!items.Any())
                {
                    return Json(new { success = false, message = "Please add at least one item." });
                }
                
                // Debug each item
                foreach (var item in items)
                {
                    Console.WriteLine($"Item: MedicationId={item.MedicationId}, Quantity={item.Quantity}, Instructions='{item.Instructions}', Frequency='{item.Frequency}', Repeats={item.Repeats}");
                }

                // Create a dummy UnprocessedScript for direct prescriptions
                var dummyScript = new UnprocessedScript
                {
                    CustomerId = customerId,
                    DoctorId = null,
                    UploadDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    ScriptImagePath = "", // Empty for direct prescriptions
                    Status = UnprocessedScript.PrescriptionStatus.Completed,
                    ProcessedDate = DateTime.UtcNow,
                    ProcessedById = _userManager.GetUserId(User)
                };

                _context.UnprocessedScripts.Add(dummyScript);
                await _context.SaveChangesAsync();

                var prescription = new Prescription
                {
                    CustomerId = customerId,
                    DoctorId = null,
                    PrescriptionDate = DateTime.UtcNow,
                    UploadId = dummyScript.UnploadId
                };

                _context.Prescriptions.Add(prescription);
                await _context.SaveChangesAsync();

                foreach (var item in items)
                {
                    // Check if medication exists
                    var medicationExists = await _context.Medications.AnyAsync(m => m.MedicationId == item.MedicationId);
                    Console.WriteLine($"Medication {item.MedicationId} exists: {medicationExists}");
                    
                    if (!medicationExists)
                    {
                        Console.WriteLine($"ERROR: Medication {item.MedicationId} does not exist in database!");
                        return Json(new { success = false, message = $"Medication with ID {item.MedicationId} not found." });
                    }
                    
                    var line = new PrescriptionLine
                    {
                        PrescriptionId = prescription.PrescriptionId,
                        MedicationId = item.MedicationId,
                        Quantity = item.Quantity,
                        Instructions = item.Instructions,
                        Frequency = Enum.Parse<DosageFrequency>(item.Frequency ?? nameof(DosageFrequency.OnceDaily)),
                        TotalRepeats = item.Repeats
                    };
                    _context.PrescriptionLines.Add(line);
                    Console.WriteLine($"Added prescription line: PrescriptionId={line.PrescriptionId}, MedicationId={line.MedicationId}, Quantity={line.Quantity}");
                }

                try
                {
                    Console.WriteLine($"About to save {items.Count} prescription lines for prescription {prescription.PrescriptionId}");
                    Console.WriteLine($"Prescription lines before save: {_context.PrescriptionLines.Count(pl => pl.PrescriptionId == prescription.PrescriptionId)}");
                    
                    await _context.SaveChangesAsync();
                    
                    Console.WriteLine($"Successfully saved {items.Count} prescription lines for prescription {prescription.PrescriptionId}");
                    Console.WriteLine($"Prescription lines after save: {_context.PrescriptionLines.Count(pl => pl.PrescriptionId == prescription.PrescriptionId)}");
                    
                    // Additional verification
                    var savedLines = await _context.PrescriptionLines
                        .Where(pl => pl.PrescriptionId == prescription.PrescriptionId)
                        .ToListAsync();
                    Console.WriteLine($"Verified prescription lines count: {savedLines.Count}");
                    foreach (var line in savedLines)
                    {
                        Console.WriteLine($"Line: MedicationId={line.MedicationId}, Quantity={line.Quantity}, Instructions='{line.Instructions}'");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving prescription lines: {ex.Message}");
                    Console.WriteLine($"Inner exception: {ex.InnerException?.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    throw;
                }

                return Json(new { success = true, message = "Prescription submitted successfully.", prescriptionId = prescription.PrescriptionId });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // AJAX: Decrement repeat count for a prescription line
        [HttpPost]
        public async Task<IActionResult> DecrementRepeat(int prescriptionLineId)
        {
            try
            {
                var prescriptionLine = await _context.PrescriptionLines
                    .FirstOrDefaultAsync(pl => pl.PrescriptionLineId == prescriptionLineId);

                if (prescriptionLine == null)
                {
                    return Json(new { success = false, message = "Prescription line not found." });
                }

                if (prescriptionLine.TotalRepeats <= 0)
                {
                    return Json(new { success = false, message = "No repeats available for this prescription." });
                }

                // Decrement the repeat count
                prescriptionLine.TotalRepeats -= 1;
                await _context.SaveChangesAsync();

                return Json(new { 
                    success = true, 
                    message = "Repeat count decremented successfully.",
                    newRepeatCount = prescriptionLine.TotalRepeats
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error decrementing repeat: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while updating repeat count." });
            }
        }

        // AJAX: Increment repeat count for a prescription line (when unchecking)
        [HttpPost]
        public async Task<IActionResult> IncrementRepeat(int prescriptionLineId)
        {
            try
            {
                var prescriptionLine = await _context.PrescriptionLines
                    .FirstOrDefaultAsync(pl => pl.PrescriptionLineId == prescriptionLineId);

                if (prescriptionLine == null)
                {
                    return Json(new { success = false, message = "Prescription line not found." });
                }

                // Increment the repeat count
                prescriptionLine.TotalRepeats += 1;
                await _context.SaveChangesAsync();

                return Json(new { 
                    success = true, 
                    message = "Repeat count incremented successfully.",
                    newRepeatCount = prescriptionLine.TotalRepeats
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error incrementing repeat: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while updating repeat count." });
            }
        }

        // AJAX: Get customer details
        [HttpGet]
        public async Task<IActionResult> GetCustomerDetails(int customerId)
        {
            var customer = await _context.Customers
                .Include(c => c.Allergies)
                .ThenInclude(a => a.ActiveIngredient)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            if (customer == null)
            {
                return Json(new { success = false, message = "Customer not found" });
            }

            var allergies = customer.Allergies.Select(a => new
            {
                ingredient = a.ActiveIngredient.Name,
                severity = a.Severity,
                description = a.Description
            }).ToList();

            return Json(new
            {
                success = true,
                customer = new
                {
                    id = customer.CustomerId,
                    name = $"{customer.Name} {customer.Surname}",
                    idNumber = customer.IDNumber,
                    email = customer.Email,
                    phone = customer.CellPhoneNumber,
                    dateCreated = customer.DateCreated.ToString("yyyy-MM-dd"),
                    allergies = allergies
                }
            });
        }

        // DEBUG: Test endpoint to check what data is received
        [HttpPost]
        public IActionResult DebugFormData()
        {
            try
            {
                var formData = Request.Form;
                var debugInfo = new
                {
                    FormKeys = formData.Keys.ToList(),
                    FormValues = formData.ToDictionary(k => k.Key, v => v.Value.ToString()),
                    HasName = formData.ContainsKey("NewCustomerName"),
                    HasSurname = formData.ContainsKey("NewCustomerSurname"),
                    HasIDNumber = formData.ContainsKey("NewCustomerIDNumber"),
                    AllergyKeys = formData.Keys.Where(k => k.Contains("NewCustomerAllergies")).ToList()
                };

                return Json(new { success = true, data = debugInfo });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Debug endpoint error: {ex.Message}");
                return Json(new { success = false, error = $"An error occurred while processing the debug request: {ex.Message}" });
            }
        }

        // DEBUG: Simple test customer registration
        [HttpPost]
        public async Task<IActionResult> TestCustomerRegistration()
        {
            try
            {
                Console.WriteLine("=== TEST CUSTOMER REGISTRATION STARTED ===");

                // Create a simple test customer
                var testCustomer = new WalkInCustomerViewModel
                {
                    NewCustomerName = "Test",
                    NewCustomerSurname = "Customer",
                    NewCustomerIDNumber = "1234567890123",
                    NewCustomerEmail = "test@example.com",
                    NewCustomerPhone = "0123456789"
                };

                // Call the actual registration method
                var result = await RegisterCustomer(testCustomer);

                Console.WriteLine("=== TEST CUSTOMER REGISTRATION COMPLETED ===");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== TEST CUSTOMER REGISTRATION ERROR: {ex.Message} ===");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return Json(new { success = false, error = ex.Message });
            }
        }

        // DEBUG: Test allergy processing specifically
        [HttpPost]
        public async Task<IActionResult> TestAllergyProcessing()
        {
            try
            {
                Console.WriteLine("=== TEST ALLERGY PROCESSING STARTED ===");

                // Test data with allergies
                var testCustomer = new WalkInCustomerViewModel
                {
                    NewCustomerName = "Test",
                    NewCustomerSurname = "Customer",
                    NewCustomerIDNumber = "1234567890123",
                    NewCustomerEmail = "test@example.com",
                    NewCustomerPhone = "0123456789",
                    NewCustomerAllergies = new List<ViewModels.NewCustomerAllergyViewModel>
                    {
                        new ViewModels.NewCustomerAllergyViewModel
                        {
                            ActiveIngredientId = 1, // Assuming ID 1 exists
                            Severity = "Moderate",
                            Description = "Test allergy"
                        }
                    }
                };

                Console.WriteLine("Test customer created with allergies");
                var result = await RegisterCustomer(testCustomer);

                Console.WriteLine("=== TEST ALLERGY PROCESSING COMPLETED ===");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== TEST ALLERGY PROCESSING ERROR: {ex.Message} ===");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return Json(new { success = false, error = ex.Message });
            }
        }

        // POST: Register new walk-in customer
        [HttpPost]
        //[ValidateAntiForgeryToken] // Temporarily disabled for debugging
        public async Task<IActionResult> RegisterCustomer(WalkInCustomerViewModel model)
        {
            Console.WriteLine("=== RegisterCustomer method called ===");
            Console.WriteLine($"Model received: Name={model?.NewCustomerName}, Surname={model?.NewCustomerSurname}, ID={model?.NewCustomerIDNumber}");

            // Check if this is an AJAX request
            bool isAjaxRequest = Request.Headers.ContainsKey("X-Requested-With") &&
                                Request.Headers["X-Requested-With"].ToString() == "XMLHttpRequest";

            Console.WriteLine($"Is AJAX request: {isAjaxRequest}");
            Console.WriteLine($"ModelState.IsValid: {ModelState.IsValid}");

            if (!ModelState.IsValid)
            {
                Console.WriteLine("ModelState is invalid. Validation errors:");
                foreach (var error in ModelState)
                {
                    if (error.Value.Errors.Any())
                    {
                        Console.WriteLine($"Field: {error.Key}");
                        foreach (var err in error.Value.Errors)
                        {
                            Console.WriteLine($"  Error: {err.ErrorMessage}");
                        }
                    }
                }

                if (isAjaxRequest)
                {
                    var errors = ModelState.Where(x => x.Value.Errors.Count > 0)
                        .Select(x => new { field = x.Key, errors = x.Value.Errors.Select(e => e.ErrorMessage) })
                        .ToDictionary(x => x.field, x => x.errors);

                    return Json(new { success = false, message = "Please correct the errors below.", errors });
                }

                // Repopulate dropdowns for regular form submission
                model.Customers = await _context.Customers
                    .Select(c => new SelectListItem
                    {
                        Value = c.CustomerId.ToString(),
                        Text = $"{c.Name} {c.Surname} ({c.IDNumber})"
                    })
                    .ToListAsync();


                model.ActiveIngredients = await _context.ActiveIngredients.ToListAsync();

                // Populate medications for prescription items
                ViewBag.Medications = await _context.Medications.OrderBy(m => m.Name).ToListAsync();

                return View("Index", model);
            }

            try
            {
                // Test database connectivity
                try
                {
                    Console.WriteLine("Testing database connectivity...");
                    var testQuery = await _context.Customers.CountAsync();
                    Console.WriteLine($"Database connection OK. Current customer count: {testQuery}");

                    // Also check ActiveIngredients table
                    var activeIngredientsCount = await _context.ActiveIngredients.CountAsync();
                    Console.WriteLine($"ActiveIngredients count: {activeIngredientsCount}");

                    if (activeIngredientsCount == 0)
                    {
                        Console.WriteLine("WARNING: No ActiveIngredients found in database!");
                    }
                    else
                    {
                        var firstFewIngredients = await _context.ActiveIngredients.Take(3).Select(ai => $"{ai.ActiveIngredientId}: {ai.Name}").ToListAsync();
                        Console.WriteLine($"Sample ActiveIngredients: {string.Join(", ", firstFewIngredients)}");
                    }
                }
                catch (Exception dbEx)
                {
                    Console.WriteLine($"Database connection error: {dbEx.Message}");
                    if (isAjaxRequest)
                    {
                        return Json(new { success = false, message = $"Database connection error: {dbEx.Message}" });
                    }
                    ModelState.AddModelError("", $"Database connection error: {dbEx.Message}");
                    return View("Index", model);
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(model.NewCustomerName) ||
                    string.IsNullOrWhiteSpace(model.NewCustomerSurname) ||
                    string.IsNullOrWhiteSpace(model.NewCustomerIDNumber))
                {
                    Console.WriteLine($"Validation failed - Name: '{model.NewCustomerName}', Surname: '{model.NewCustomerSurname}', ID: '{model.NewCustomerIDNumber}'");
                    if (isAjaxRequest)
                    {
                        return Json(new { success = false, message = "Name, surname, and ID number are required fields." });
                    }
                    ModelState.AddModelError("", "Name, surname, and ID number are required fields.");
                    return View("Index", model);
                }

                // Check if ID number already exists across all entities
                if (!string.IsNullOrWhiteSpace(model.NewCustomerIDNumber))
                {
                    bool idExistsInCustomers = await _context.Customers.AnyAsync(c => c.IDNumber == model.NewCustomerIDNumber);
                    bool idExistsInPharmacists = await _context.Pharmacists.AnyAsync(p => p.IDNumber == model.NewCustomerIDNumber);
                    
                    if (idExistsInCustomers || idExistsInPharmacists)
                    {
                        if (isAjaxRequest)
                        {
                            return Json(new { success = false, message = "This ID number is already registered. Please use a unique ID number." });
                        }
                        ModelState.AddModelError("", "This ID number is already registered. Please use a unique ID number.");
                        return View("Index", model);
                    }
                }

                // Log successful validation
                Console.WriteLine($"Validation passed - Creating customer: {model.NewCustomerName} {model.NewCustomerSurname} ({model.NewCustomerIDNumber})");

                // Debug: Log the received allergy data
                Console.WriteLine($"=== ALLERGY DEBUGGING START ===");
                Console.WriteLine($"Received {model.NewCustomerAllergies?.Count ?? 0} allergies from model");
                if (model.NewCustomerAllergies != null)
                {
                    for (int i = 0; i < model.NewCustomerAllergies.Count; i++)
                    {
                        var allergy = model.NewCustomerAllergies[i];
                        Console.WriteLine($"Model Allergy {i}: ActiveIngredientId={allergy.ActiveIngredientId}, Severity='{allergy.Severity}', Description='{allergy.Description}'");
                    }
                }

                // Also check raw form data for allergy fields
                var formData = Request.Form;
                Console.WriteLine($"Total form data keys: {formData.Keys.Count}");
                Console.WriteLine($"All form keys: {string.Join(", ", formData.Keys)}");

                var allergyKeys = formData.Keys.Where(k => k.Contains("NewCustomerAllergies")).ToList();
                Console.WriteLine($"Raw allergy form keys ({allergyKeys.Count}): {string.Join(", ", allergyKeys)}");
                foreach (var key in allergyKeys)
                {
                    Console.WriteLine($"Raw allergy data - {key}: '{formData[key]}'");
                }

                // Check for potential conflicts
                var tokenKeys = formData.Keys.Where(k => k.Contains("__RequestVerificationToken")).ToList();
                if (tokenKeys.Any())
                {
                    Console.WriteLine($"Found verification token keys: {string.Join(", ", tokenKeys)}");
                }
                Console.WriteLine($"=== ALLERGY DEBUGGING END ===");

                // Use the provided email (now required)
                var email = model.NewCustomerEmail;
                
                // Ensure email is unique
                var existingUser = await _userManager.FindByEmailAsync(email);
                int emailCounter = 1;
                while (existingUser != null)
                {
                    // Append counter if email already exists
                    var baseEmail = email.Split('@')[0];
                    var emailDomain = email.Split('@')[1];
                    email = $"{baseEmail}.{emailCounter}@{emailDomain}";
                    existingUser = await _userManager.FindByEmailAsync(email);
                    emailCounter++;
                }

                // Create Identity User (following existing pattern)
                var user = new Users
                {
                    UserName = email,
                    Email = email,
                    FullName = $"{model.NewCustomerName} {model.NewCustomerSurname}",
                    EmailConfirmed = true
                };

                // Generate random password (following existing pattern)
                var password = GenerateTemporaryPassword();
                Console.WriteLine($"Creating user with email: {email}, password length: {password.Length}");

                var userResult = await _userManager.CreateAsync(user, password);
                Console.WriteLine($"User creation result: {userResult.Succeeded}");

                if (!userResult.Succeeded)
                {
                    var errors = string.Join(", ", userResult.Errors.Select(e => e.Description));
                    if (isAjaxRequest)
                    {
                        return Json(new { success = false, message = $"Failed to create user account: {errors}" });
                    }
                    ModelState.AddModelError("", $"Failed to create user account: {errors}");
                    return View("Index", model);
                }

                // Ensure Customer role exists (following existing pattern)
                if (!await _roleManager.RoleExistsAsync("Customer"))
                {
                    var roleResult = await _roleManager.CreateAsync(new IdentityRole("Customer"));
                    if (!roleResult.Succeeded)
                    {
                        var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                        if (isAjaxRequest)
                        {
                            return Json(new { success = false, message = $"Failed to create customer role: {errors}" });
                        }
                        ModelState.AddModelError("", $"Failed to create customer role: {errors}");
                        return View("Index", model);
                    }
                }

                // Add to Customer role (following existing pattern)
                var roleAssignResult = await _userManager.AddToRoleAsync(user, "Customer");
                if (!roleAssignResult.Succeeded)
                {
                    var errors = string.Join(", ", roleAssignResult.Errors.Select(e => e.Description));
                    if (isAjaxRequest)
                    {
                        return Json(new { success = false, message = $"Failed to assign customer role: {errors}" });
                    }
                    ModelState.AddModelError("", $"Failed to assign customer role: {errors}");
                    return View("Index", model);
                }

                // Create Customer record (following existing pattern)
                var customer = new Customer
                {
                    UserId = user.Id,
                    Name = model.NewCustomerName ?? "",
                    Surname = model.NewCustomerSurname ?? "",
                    IDNumber = model.NewCustomerIDNumber ?? "",
                    Email = email,
                    CellPhoneNumber = model.NewCustomerPhone ?? "0000000000",
                    Street = "",
                    Suburb = "",
                    City = "",
                    Province = "",
                    ZipCode = "",
                    Country = "South Africa",
                    ProfileImagePath = "/images/default-profile.png",
                    DateCreated = DateTime.Now,
                    IsWalkInCustomer = true
                };

                Console.WriteLine($"Created customer object: UserId={customer.UserId}, Name={customer.Name}, Email={customer.Email}");

                // Save customer to database using transaction for data integrity
                using (var transaction = _context.Database.BeginTransaction())
                {
                    try
                    {
                        Console.WriteLine("Adding customer to context...");
                        _context.Customers.Add(customer);
                        Console.WriteLine("Saving customer to database...");
                        await _context.SaveChangesAsync();
                        Console.WriteLine("Customer saved successfully, ID: " + customer.CustomerId);

                        // Add allergies if any (following existing pattern)
                        List<ViewModels.NewCustomerAllergyViewModel> allergiesToSave;

                        // First try to use the model-bound allergies
                        if (model.NewCustomerAllergies != null && model.NewCustomerAllergies.Count > 0)
                        {
                            allergiesToSave = model.NewCustomerAllergies.Where(a => a.ActiveIngredientId >= 0 && !string.IsNullOrEmpty(a.Severity)).ToList();
                            Console.WriteLine($"Using model-bound allergies: {allergiesToSave.Count}");
                        }
                        else
                        {
                            // Fallback: manually parse allergy data from form
                            Console.WriteLine("Model allergies empty, trying manual parsing...");
                            allergiesToSave = new List<ViewModels.NewCustomerAllergyViewModel>();
                            var fallbackFormData = Request.Form;
                            var allergyIndexes = new HashSet<int>();

                            // Find all allergy indexes
                            foreach (var key in fallbackFormData.Keys)
                            {
                                if (key.Contains("NewCustomerAllergies[") && key.Contains("].ActiveIngredientId"))
                                {
                                    var startIndex = key.IndexOf('[') + 1;
                                    var endIndex = key.IndexOf(']');
                                    if (int.TryParse(key.Substring(startIndex, endIndex - startIndex), out int index))
                                    {
                                        allergyIndexes.Add(index);
                                    }
                                }
                            }

                            Console.WriteLine($"Found allergy indexes: {string.Join(", ", allergyIndexes)}");

                            // Parse each allergy
                            foreach (var index in allergyIndexes)
                            {
                                Console.WriteLine($"=== PARSING ALLERGY {index} ===");
                                var ingredientIdKey = $"NewCustomerAllergies[{index}].ActiveIngredientId";
                                var severityKey = $"NewCustomerAllergies[{index}].Severity";
                                var descriptionKey = $"NewCustomerAllergies[{index}].Description";

                                Console.WriteLine($"Looking for keys: {ingredientIdKey}, {severityKey}, {descriptionKey}");

                                bool hasIngredientId = fallbackFormData.ContainsKey(ingredientIdKey);
                                bool hasSeverity = fallbackFormData.ContainsKey(severityKey);
                                bool hasDescription = fallbackFormData.ContainsKey(descriptionKey);

                                Console.WriteLine($"Key existence - IngredientId: {hasIngredientId}, Severity: {hasSeverity}, Description: {hasDescription}");

                                if (hasIngredientId && hasSeverity)
                                {
                                    string ingredientIdValue = fallbackFormData[ingredientIdKey];
                                    string severityValue = fallbackFormData[severityKey];
                                    string descriptionValue = hasDescription ? fallbackFormData[descriptionKey] : "";

                                    Console.WriteLine($"Raw values - IngredientId: '{ingredientIdValue}', Severity: '{severityValue}', Description: '{descriptionValue}'");

                                    if (int.TryParse(ingredientIdValue, out int ingredientId) &&
                                        !string.IsNullOrEmpty(severityValue))
                                    {
                                        var allergyViewModel = new ViewModels.NewCustomerAllergyViewModel
                                        {
                                            ActiveIngredientId = ingredientId,
                                            Severity = severityValue,
                                            Description = descriptionValue
                                        };

                                        allergiesToSave.Add(allergyViewModel);
                                        Console.WriteLine($"Successfully parsed allergy {index}: IngredientId={ingredientId}, Severity='{severityValue}', Description='{descriptionValue}'");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Failed to parse allergy {index}: Parse failed or severity empty");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"Missing required keys for allergy {index}");
                                }
                            }
                        }

                        // Save the allergies
                        if (allergiesToSave.Count > 0)
                        {
                            Console.WriteLine($"Saving {allergiesToSave.Count} allergies to database");
                            foreach (var allergy in allergiesToSave)
                            {
                                Console.WriteLine($"Processing allergy: ActiveIngredientId={allergy.ActiveIngredientId}, Severity='{allergy.Severity}', Description='{allergy.Description}'");

                                // Validate required fields
                                if (allergy.ActiveIngredientId < 0)
                                {
                                    Console.WriteLine($"ERROR: Invalid ActiveIngredientId: {allergy.ActiveIngredientId}");
                                    throw new Exception($"Invalid ActiveIngredientId: {allergy.ActiveIngredientId}");
                                }

                                if (string.IsNullOrEmpty(allergy.Severity))
                                {
                                    Console.WriteLine("ERROR: Severity is null or empty");
                                    throw new Exception("Allergy severity cannot be null or empty");
                                }

                                int activeIngredientId = allergy.ActiveIngredientId;

                                // Handle custom allergies (ID 0 means it's a custom allergy entered by the user)
                                if (allergy.ActiveIngredientId == 0)
                                {
                                    Console.WriteLine($"Custom allergy detected. Looking for or creating new ActiveIngredient with name: '{allergy.Description}'");
                                    
                                    // Check if this custom allergy already exists
                                    var existingIngredient = await _context.ActiveIngredients
                                        .FirstOrDefaultAsync(ai => ai.Name.ToLower() == allergy.Description.ToLower());
                                    
                                    if (existingIngredient != null)
                                    {
                                        Console.WriteLine($"Custom allergy '{allergy.Description}' already exists with ID: {existingIngredient.ActiveIngredientId}");
                                        activeIngredientId = existingIngredient.ActiveIngredientId;
                                    }
                                    else
                                    {
                                        // Create new ActiveIngredient for custom allergy
                                        var newIngredient = new ActiveIngredients
                                        {
                                            Name = allergy.Description,
                                            Description = $"Custom allergy: {allergy.Description}",
                                            Strength = "N/A"
                                        };
                                        _context.ActiveIngredients.Add(newIngredient);
                                        await _context.SaveChangesAsync(); // Save to get the new ID
                                        activeIngredientId = newIngredient.ActiveIngredientId;
                                        Console.WriteLine($"Created new ActiveIngredient with ID: {activeIngredientId}");
                                    }
                                }
                                else
                                {
                                    // Verify the ActiveIngredient exists for known allergies
                                    var activeIngredientExists = await _context.ActiveIngredients.AnyAsync(ai => ai.ActiveIngredientId == allergy.ActiveIngredientId);
                                    if (!activeIngredientExists)
                                    {
                                        Console.WriteLine($"ERROR: ActiveIngredient with ID {allergy.ActiveIngredientId} does not exist");
                                        throw new Exception($"ActiveIngredient with ID {allergy.ActiveIngredientId} does not exist");
                                    }
                                    Console.WriteLine($"Known allergy with ID: {allergy.ActiveIngredientId}");
                                }

                                var customerAllergy = new CustomerAllergy
                                {
                                    CustomerId = customer.CustomerId,
                                    ActiveIngredientId = activeIngredientId,
                                    Severity = allergy.Severity ?? "Moderate", // Provide default if null
                                    Description = string.IsNullOrWhiteSpace(allergy.Description) ? (allergy.Severity ?? "Moderate") : allergy.Description
                                };

                                _context.CustomerAllergies.Add(customerAllergy);
                                Console.WriteLine($"Added allergy to context: CustomerId={customer.CustomerId}, IngredientId={activeIngredientId}, Severity={customerAllergy.Severity}");
                            }

                            Console.WriteLine("Saving allergies to database...");
                            await _context.SaveChangesAsync();
                            Console.WriteLine("Allergies saved successfully");
                        }
                        else
                        {
                            Console.WriteLine("No allergies to save");
                        }

                        // Commit transaction if everything succeeds
                        Console.WriteLine("Committing transaction...");
                        transaction.Commit();
                        Console.WriteLine("Transaction committed successfully");
                    }
                    catch (Exception ex)
                    {
                        // Rollback transaction on error
                        transaction.Rollback();

                        // Log detailed error for debugging
                        Console.WriteLine($"Database transaction error: {ex.Message}");
                        Console.WriteLine($"Stack trace: {ex.StackTrace}");
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                        }

                        if (isAjaxRequest)
                        {
                            return Json(new { success = false, message = $"Database error: {ex.Message}" });
                        }
                        ModelState.AddModelError("", $"Database error: {ex.Message}");
                        return View("Index", model);
                    }
                }

                TempData["SuccessMessage"] = $"Walk-in customer {customer.Name} {customer.Surname} registered successfully!";

                if (isAjaxRequest)
                {
                    return Json(new
                    {
                        success = true,
                        message = $"Walk-in customer {customer.Name} {customer.Surname} registered successfully!",
                        customerId = customer.CustomerId,
                        customerName = $"{customer.Name} {customer.Surname}",
                        customerText = $"{customer.Name} {customer.Surname} ({customer.IDNumber})"
                    });
                }

                model.SelectedCustomerId = customer.CustomerId;

                // Repopulate dropdowns with new customer selected (newest first)
                model.Customers = await _context.Customers
                    .OrderByDescending(c => c.DateCreated)
                    .Select(c => new SelectListItem
                    {
                        Value = c.CustomerId.ToString(),
                        Text = $"{c.Name} {c.Surname} ({c.IDNumber})",
                        Selected = c.CustomerId == customer.CustomerId
                    })
                    .ToListAsync();


                model.ActiveIngredients = await _context.ActiveIngredients.ToListAsync();

                // Populate medications for prescription items
                ViewBag.Medications = await _context.Medications.OrderBy(m => m.Name).ToListAsync();

                return View("Index", model);
            }
            catch (Exception ex)
            {
                // Log the detailed error for debugging
                Console.WriteLine($"Customer registration error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");

                if (isAjaxRequest)
                {
                    return Json(new { success = false, message = $"An error occurred while registering the customer: {ex.Message}" });
                }

                ModelState.AddModelError("", $"An error occurred while registering the customer: {ex.Message}");

                // Repopulate dropdowns for error display
                try
                {
                    model.Customers = await _context.Customers
                        .Select(c => new SelectListItem
                        {
                            Value = c.CustomerId.ToString(),
                            Text = $"{c.Name} {c.Surname} ({c.IDNumber})"
                        })
                        .ToListAsync();


                    model.ActiveIngredients = await _context.ActiveIngredients.ToListAsync();

                    // Populate medications for prescription items
                    ViewBag.Medications = await _context.Medications.OrderBy(m => m.Name).ToListAsync();
                }
                catch (Exception dropdownEx)
                {
                    Console.WriteLine($"Error repopulating dropdowns: {dropdownEx.Message}");
                }

                return View("Index", model);
            }
        }

        // Generate temporary password (following existing pattern)
        private static string GenerateTemporaryPassword()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 12)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        // POST: Upload prescription
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadPrescription(WalkInCustomerViewModel model, IFormFile prescriptionFile)
        {
            if (model.SelectedCustomerId == 0)
            {
                ModelState.AddModelError("", "Please select a customer first.");
                return await PopulateViewModelAndReturn(model);
            }

            if (prescriptionFile == null || prescriptionFile.Length == 0)
            {
                ModelState.AddModelError("", "Please select a prescription file to upload.");
                return await PopulateViewModelAndReturn(model);
            }

            // Validate file
            var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".heic" };
            var ext = System.IO.Path.GetExtension(prescriptionFile.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(ext))
            {
                ModelState.AddModelError("", "Only PDF, JPG, PNG, or HEIC files are allowed.");
                return await PopulateViewModelAndReturn(model);
            }

            if (prescriptionFile.Length > 10 * 1024 * 1024) // 10MB limit
            {
                ModelState.AddModelError("", "File size cannot exceed 10MB.");
                return await PopulateViewModelAndReturn(model);
            }

            try
            {
                // Store file content in database instead of file system
                byte[] fileContent;
                using (var memoryStream = new MemoryStream())
                {
                    await prescriptionFile.CopyToAsync(memoryStream);
                    fileContent = memoryStream.ToArray();
                }

                // Create unprocessed script record
                var unprocessedScript = new UnprocessedScript
                {
                    CustomerId = model.SelectedCustomerId,
                    DoctorId = null, // No doctor assigned for walk-in customers
                    UploadDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    ScriptImagePath = $"/uploads/{Guid.NewGuid()}{ext}", // Keep for compatibility
                    FileContent = fileContent,
                    FileName = prescriptionFile.FileName,
                    ContentType = prescriptionFile.ContentType,
                    Status = UnprocessedScript.PrescriptionStatus.Completed, // Save as Completed for walk-in customers
                    Comments = model.PrescriptionComments,
                    ProcessedDate = DateTime.UtcNow, // Set processed date since it's completed
                    ProcessedById = _userManager.GetUserId(User) // Set the current pharmacist/manager as processor
                };

                _context.UnprocessedScripts.Add(unprocessedScript);
                await _context.SaveChangesAsync();

                // Process prescription items if provided
                if (!string.IsNullOrEmpty(model.PrescriptionItems))
                {
                    try
                    {
                        var prescriptionItems = JsonSerializer.Deserialize<List<PrescriptionItemViewModel>>(model.PrescriptionItems);
                        if (prescriptionItems != null && prescriptionItems.Any())
                        {
                            // Create a prescription record
                            var prescription = new Prescription
                            {
                                CustomerId = model.SelectedCustomerId,
                                DoctorId = null, // No doctor assigned for walk-in customers
                                PrescriptionDate = DateTime.UtcNow,
                                UploadId = unprocessedScript.UnploadId
                            };

                            _context.Prescriptions.Add(prescription);
                            await _context.SaveChangesAsync();

                            // Add prescription lines
                            foreach (var item in prescriptionItems)
                            {
                                var prescriptionLine = new PrescriptionLine
                                {
                                    PrescriptionId = prescription.PrescriptionId,
                                    MedicationId = item.MedicationId,
                                    Quantity = item.Quantity,
                                    Instructions = item.Instructions,
                                    Frequency = Enum.Parse<DosageFrequency>(item.Frequency),
                                    TotalRepeats = item.Repeats
                                };

                                _context.PrescriptionLines.Add(prescriptionLine);
                            }

                            try
                            {
                                Console.WriteLine($"About to save {prescriptionItems.Count} prescription lines for prescription {prescription.PrescriptionId}");
                                Console.WriteLine($"Prescription lines before save: {_context.PrescriptionLines.Count(pl => pl.PrescriptionId == prescription.PrescriptionId)}");
                                
                                await _context.SaveChangesAsync();
                                
                                Console.WriteLine($"Successfully saved {prescriptionItems.Count} prescription lines for prescription {prescription.PrescriptionId}");
                                Console.WriteLine($"Prescription lines after save: {_context.PrescriptionLines.Count(pl => pl.PrescriptionId == prescription.PrescriptionId)}");
                                
                                // Additional verification
                                var savedLines = await _context.PrescriptionLines
                                    .Where(pl => pl.PrescriptionId == prescription.PrescriptionId)
                                    .ToListAsync();
                                Console.WriteLine($"Verified prescription lines count: {savedLines.Count}");
                                foreach (var line in savedLines)
                                {
                                    Console.WriteLine($"Line: MedicationId={line.MedicationId}, Quantity={line.Quantity}, Instructions='{line.Instructions}'");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error saving prescription lines: {ex.Message}");
                                Console.WriteLine($"Inner exception: {ex.InnerException?.Message}");
                                throw;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the error but don't fail the upload
                        Console.WriteLine($"Error processing prescription items: {ex.Message}");
                    }
                }

                TempData["SuccessMessage"] = $"Prescription uploaded successfully! Reference #: {unprocessedScript.UnploadId}";

                // Redirect to completed prescriptions where the uploaded prescription is saved
                return RedirectToAction("PendingScripts", "Prescriptions", new { status = "Completed" });
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "An error occurred while uploading the prescription. Please try again.");
                return await PopulateViewModelAndReturn(model);
            }
        }

        private async Task<IActionResult> PopulateViewModelAndReturn(WalkInCustomerViewModel model)
        {
            // Repopulate dropdowns
            model.Customers = await _context.Customers
                .Select(c => new SelectListItem
                {
                    Value = c.CustomerId.ToString(),
                    Text = $"{c.Name} {c.Surname} ({c.IDNumber})",
                    Selected = c.CustomerId == model.SelectedCustomerId
                })
                .ToListAsync();


            model.ActiveIngredients = await _context.ActiveIngredients.ToListAsync();

            // Populate medications for prescription items
            ViewBag.Medications = await _context.Medications.OrderBy(m => m.Name).ToListAsync();

            return View("Index", model);
        }

        // AJAX: Check medication allergies
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> CheckMedicationAllergies(int customerId, int medicationId)
        {
            try
            {
                if (customerId <= 0 || medicationId <= 0)
                {
                    return Json(new { success = true, hasConflict = false });
                }

                // Get customer allergies
                var customerAllergies = await _context.CustomerAllergies
                    .Include(ca => ca.ActiveIngredient)
                    .Where(ca => ca.CustomerId == customerId)
                    .ToListAsync();

                if (!customerAllergies.Any())
                {
                    return Json(new { success = true, hasConflict = false });
                }

                var allergicIngredientIds = customerAllergies.Select(ca => ca.ActiveIngredientId).ToList();

                // Get medication ingredients
                var medication = await _context.Medications
                    .Include(m => m.ActiveIngredients)
                        .ThenInclude(mi => mi.ActiveIngredient)
                    .FirstOrDefaultAsync(m => m.MedicationId == medicationId);

                if (medication == null)
                {
                    return Json(new { success = false, message = "Medication not found" });
                }

                var medicationIngredients = medication.ActiveIngredients?.Select(mi => mi.ActiveIngredientId).ToList() ?? new List<int>();
                var conflictingIngredients = medicationIngredients.Intersect(allergicIngredientIds).ToList();

                if (conflictingIngredients.Any())
                {
                    var conflicts = customerAllergies
                        .Where(ca => conflictingIngredients.Contains(ca.ActiveIngredientId))
                        .Select(ca => new
                        {
                            ingredientId = ca.ActiveIngredientId,
                            ingredientName = ca.ActiveIngredient?.Name ?? "Unknown",
                            severity = ca.Severity,
                            description = ca.Description
                        }).ToList();

                    // Find safe alternatives
                    var safeAlternatives = await _context.Medications
                        .Include(m => m.ActiveIngredients)
                            .ThenInclude(mi => mi.ActiveIngredient)
                        .Include(m => m.DosageForm)
                        .Include(m => m.Supplier)
                        .Where(m => m.MedicationId != medicationId && 
                                   !m.ActiveIngredients.Any(mi => allergicIngredientIds.Contains(mi.ActiveIngredientId)))
                        .Select(m => new
                        {
                            medicationId = m.MedicationId,
                            name = m.Name,
                            description = m.Description,
                            price = m.Price,
                            dosageForm = m.DosageForm != null ? m.DosageForm.Type : "Unknown",
                            supplier = m.Supplier != null ? m.Supplier.Name : "Unknown",
                            activeIngredients = m.ActiveIngredients.Select(mi => new
                            {
                                ingredientId = mi.ActiveIngredientId,
                                name = mi.ActiveIngredient.Name,
                                strength = mi.Strength
                            }).ToList()
                        })
                        .OrderBy(m => m.name)
                        .Take(10)
                        .ToListAsync();

                    return Json(new
                    {
                        success = true,
                        hasConflict = true,
                        medicationName = medication.Name,
                        conflictingIngredients = conflicts,
                        safeAlternatives = safeAlternatives
                    });
                }

                return Json(new { success = true, hasConflict = false });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error checking allergies: {ex.Message}" });
            }
        }

    }
}
