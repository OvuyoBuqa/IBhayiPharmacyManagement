using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using IBhayiPharmacyManagementSystem.Models;
using IBhayiPharmacyManagementSystem.ViewModels;
using IBhayiPharmacyManagementSystem.Services;
using IBhayiPharmacyManagementSystem.Data;
using IBhayiPharmacyManagementSystem.Enums;
using System.Text.RegularExpressions;
using System.Text;
using System.Linq;

namespace IBhayiPharmacyManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,PharmacyManager")]
    public class DataImportController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<DataImportController> _logger;
        private readonly PdfImportService _pdfImportService;
        private readonly XlsxImportService _xlsxImportService;

        public DataImportController(
            AppDbContext context,
            UserManager<Users> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<DataImportController> logger,
            PdfImportService pdfImportService,
            XlsxImportService xlsxImportService)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
            _pdfImportService = pdfImportService;
            _xlsxImportService = xlsxImportService;
        }

        // GET: DataImport Dashboard
        public async Task<IActionResult> Index()
        {
            var viewModel = new DataImportDashboardViewModel
            {
                DatabaseStats = await GetDatabaseStats()
            };
            return View(viewModel);
        }

        // POST: Upload and Parse PDF
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadPdf(DataImportDashboardViewModel model)
        {
            if (model.PdfFile == null || model.PdfFile.Length == 0)
            {
                ModelState.AddModelError("PdfFile", "Please select a PDF file to upload.");
                model.DatabaseStats = await GetDatabaseStats();
                return View("Index", model);
            }

            // Validate file
            var allowedExtensions = new[] { ".pdf" };
            var ext = Path.GetExtension(model.PdfFile.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(ext))
            {
                ModelState.AddModelError("PdfFile", "Only PDF files are allowed.");
                model.DatabaseStats = await GetDatabaseStats();
                return View("Index", model);
            }

            if (model.PdfFile.Length > 50 * 1024 * 1024) // 50MB limit
            {
                ModelState.AddModelError("PdfFile", "File size cannot exceed 50MB.");
                model.DatabaseStats = await GetDatabaseStats();
                return View("Index", model);
            }

            try
            {
                // Parse PDF content
                var extractedData = await _pdfImportService.ParsePdfAsync(model.PdfFile);
                
                // Debug: Log the parsed data
                _logger.LogInformation("Parsed data - Active Ingredients: {Count}, Dosage Forms: {Count2}", 
                    extractedData.ActiveIngredients?.Count ?? 0, extractedData.DosageForms?.Count ?? 0);
                
                if (extractedData.ActiveIngredients?.Any() == true)
                {
                    _logger.LogInformation("First few active ingredients: {Ingredients}", 
                        string.Join(", ", extractedData.ActiveIngredients.Take(5)));
                }
                
                // Store parsed data in session for preview
                HttpContext.Session.SetString("ParsedData", System.Text.Json.JsonSerializer.Serialize(extractedData));
                
                // Redirect to preview page
                return RedirectToAction("Preview");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing PDF file");
                ModelState.AddModelError("", "An error occurred while parsing the PDF file. Please check the file format.");
                model.DatabaseStats = await GetDatabaseStats();
                return View("Index", model);
            }
        }

        // GET: Preview extracted data
        public IActionResult Preview()
        {
            var jsonData = HttpContext.Session.GetString("ParsedData");
            if (string.IsNullOrEmpty(jsonData))
            {
                TempData["ErrorMessage"] = "No parsed data found. Please upload a PDF file first.";
                return RedirectToAction("Index");
            }

            var parsedData = System.Text.Json.JsonSerializer.Deserialize<PdfParsedData>(jsonData);
            
            // Debug: Log the deserialized data
            _logger.LogInformation("Deserialized data - Active Ingredients: {Count}, Dosage Forms: {Count2}", 
                parsedData.ActiveIngredients?.Count ?? 0, parsedData.DosageForms?.Count ?? 0);
            
            if (parsedData.ActiveIngredients?.Any() == true)
            {
                _logger.LogInformation("Deserialized active ingredients: {Ingredients}", 
                    string.Join(", ", parsedData.ActiveIngredients.Take(5)));
            }
            
            var viewModel = new DataImportPreviewViewModel
            {
                ParsedData = parsedData,
                ImportOptions = new ImportOptionsViewModel
                {
                    ImportActiveIngredients = true,
                    ImportDosageForms = true,
                    ImportSuppliers = true,
                    ImportMedications = true,
                    ImportDoctors = true,
                    ImportPharmacyManagers = true,
                    ImportPharmacists = true
                }
            };

            // No role-based hiding: doctors should be visible and imported into Doctors only

            return View(viewModel);
        }

        // POST: Execute import
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExecuteImport(DataImportPreviewViewModel model)
        {
            var jsonData = HttpContext.Session.GetString("ParsedData");
            if (string.IsNullOrEmpty(jsonData))
            {
                TempData["ErrorMessage"] = "No parsed data found. Please upload a PDF file first.";
                return RedirectToAction("Index");
            }

            var parsedData = System.Text.Json.JsonSerializer.Deserialize<PdfParsedData>(jsonData);
            var importResults = new ImportResultsViewModel();

            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                // Import Active Ingredients
                if (model.ImportOptions.ImportActiveIngredients)
                {
                    importResults.ActiveIngredientsResult = await ImportActiveIngredients(parsedData.ActiveIngredients);
                }

                // Import Dosage Forms
                if (model.ImportOptions.ImportDosageForms)
                {
                    importResults.DosageFormsResult = await ImportDosageForms(parsedData.DosageForms);
                }

                // Import Suppliers
                if (model.ImportOptions.ImportSuppliers)
                {
                    importResults.SuppliersResult = await ImportSuppliers(parsedData.Suppliers);
                }

                // Import Doctors
                if (model.ImportOptions.ImportDoctors)
                {
                    importResults.DoctorsResult = await ImportDoctors(parsedData.Doctors);
                }

                // Import Pharmacy Managers (exclude any entries that actually belong to Doctors)
                if (model.ImportOptions.ImportPharmacyManagers)
                {
                    var doctorEmails = new HashSet<string>(parsedData.Doctors?.Select(d => d.Email?.Trim().ToLower())
                        .Where(e => !string.IsNullOrWhiteSpace(e)) ?? Enumerable.Empty<string>());
                    var doctorPhones = new HashSet<string>(parsedData.Doctors?.Select(d => d.PhoneNumber?.Replace(" ", "").Trim())
                        .Where(p => !string.IsNullOrWhiteSpace(p)) ?? Enumerable.Empty<string>());

                    var managersToImport = parsedData.PharmacyManagers
                        ?.Where(pm =>
                        {
                            var email = pm.Email?.Trim().ToLower();
                            var phone = pm.PhoneNumber?.Replace(" ", "").Trim();
                            var isDoctorMatch = (!string.IsNullOrWhiteSpace(email) && doctorEmails.Contains(email))
                                || (!string.IsNullOrWhiteSpace(phone) && doctorPhones.Contains(phone));
                            return !isDoctorMatch;
                        })
                        .ToList() ?? new List<PharmacyManagerData>();

                    importResults.PharmacyManagersResult = await ImportPharmacyManagers(managersToImport);
                }

                // Import Pharmacists
                if (model.ImportOptions.ImportPharmacists)
                {
                    importResults.PharmacistsResult = await ImportPharmacists(parsedData.Pharmacists);
                }

                // Import Medications (must be last due to dependencies)
                if (model.ImportOptions.ImportMedications)
                {
                    importResults.MedicationsResult = await ImportMedications(parsedData.Medications);
                }

                await transaction.CommitAsync();
                
                // Clear session data
                HttpContext.Session.Remove("ParsedData");
                
                TempData["SuccessMessage"] = "Data imported successfully!";
                return View("ImportResults", importResults);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during data import: {Message}", ex.Message);
                _logger.LogError(ex, "Stack trace: {StackTrace}", ex.StackTrace);
                TempData["ErrorMessage"] = $"An error occurred during import: {ex.Message}";
                return View("Preview", model);
            }
        }

        // GET: Debug PDF parsing
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DebugPdf(IFormFile pdfFile)
        {
            if (pdfFile == null || pdfFile.Length == 0)
            {
                return Json(new { error = "No file uploaded" });
            }

            try
            {
                using var stream = pdfFile.OpenReadStream();
                using var document = UglyToad.PdfPig.PdfDocument.Open(stream);
                
                var fullText = new StringBuilder();
                
                // Extract text from all pages
                foreach (var page in document.GetPages())
                {
                    fullText.AppendLine(page.Text);
                }

                var text = fullText.ToString();
                
                return Json(new { 
                    success = true,
                    textLength = text.Length,
                    first500Chars = text.Length > 500 ? text.Substring(0, 500) : text,
                    fullText = text,
                    textPreview = "Full text is available in the 'fullText' property. Check console or download the data."
                });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        private async Task<DatabaseStatsViewModel> GetDatabaseStats()
        {
            return new DatabaseStatsViewModel
            {
                ActiveIngredientsCount = await _context.ActiveIngredients.CountAsync(),
                DosageFormsCount = await _context.Dosages.CountAsync(),
                SuppliersCount = await _context.Suppliers.CountAsync(),
                MedicationsCount = await _context.Medications.CountAsync(),
                DoctorsCount = await _context.Doctors.CountAsync(),
                PharmacyManagersCount = await _context.PharmacyManagers.CountAsync(),
                PharmacistsCount = await _context.Pharmacists.CountAsync(),
                CustomersCount = await _context.Customers.CountAsync(),
                OrdersCount = await _context.Orders.CountAsync(),
                PrescriptionsCount = await _context.Prescriptions.CountAsync(),
                DispensedPrescriptionsCount = await _context.DispensedPrescriptions.CountAsync()
            };
        }

        private async Task<ImportResult> ImportActiveIngredients(List<string> ingredients)
        {
            var result = new ImportResult();
            var existingIngredients = await _context.ActiveIngredients.Select(ai => ai.Name).ToListAsync();

            foreach (var ingredientName in ingredients)
            {
                if (!existingIngredients.Contains(ingredientName))
                {
                    var ingredient = new ActiveIngredients
                    {
                        Name = ingredientName,
                        Description = $"Imported active ingredient: {ingredientName}",
                        Strength = "As per medication" // Default strength
                    };
                    _context.ActiveIngredients.Add(ingredient);
                    result.SuccessCount++;
                }
                else
                {
                    result.SkippedCount++;
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }

        private async Task<ImportResult> ImportDosageForms(List<string> dosageForms)
        {
            var result = new ImportResult();
            var existingForms = await _context.Dosages.Select(df => df.Type).ToListAsync();
            var existingFormsNormalized = existingForms.ToDictionary(df => df.ToLowerInvariant(), df => df);

            foreach (var formType in dosageForms)
            {
                // Normalize the dosage form to handle singular/plural variations
                var normalizedType = NormalizeDosageForm(formType);
                
                if (string.IsNullOrEmpty(normalizedType))
                {
                    result.SkippedCount++;
                    continue;
                }

                // Normalize the lookup key (already lowercase from NormalizeDosageForm)
                var normalizedKey = normalizedType.ToLowerInvariant();

                // Check if a normalized version already exists
                if (existingFormsNormalized.TryGetValue(normalizedKey, out var existingType))
                {
                    // Use the existing capitalization from the database
                    result.SkippedCount++;
                }
                else
                {
                    // Capitalize first letter for consistency
                    var capitalizedType = !string.IsNullOrEmpty(normalizedType) 
                        ? char.ToUpper(normalizedType[0]) + normalizedType.Substring(1).ToLowerInvariant() 
                        : formType;
                    
                    var dosageForm = new DosageForm
                    {
                        Type = capitalizedType,
                        Description = $"Imported dosage form: {capitalizedType}"
                    };
                    _context.Dosages.Add(dosageForm);
                    result.SuccessCount++;
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }

        private async Task<ImportResult> ImportSuppliers(List<SupplierData> suppliers)
        {
            var result = new ImportResult();
            var existingSuppliers = await _context.Suppliers.Select(s => s.Name).ToListAsync();

            foreach (var supplierData in suppliers)
            {
                if (!existingSuppliers.Contains(supplierData.Name))
                {
                    var supplier = new Supplier
                    {
                        Name = supplierData.Name,
                        ContactPerson = supplierData.ContactPerson, // Use the combined name+surname
                        Email = supplierData.Email
                    };
                    _context.Suppliers.Add(supplier);
                    result.SuccessCount++;
                }
                else
                {
                    result.SkippedCount++;
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }

        private async Task<ImportResult> ImportDoctors(List<DoctorData> doctors)
        {
            var result = new ImportResult();
            var existingDoctors = await _context.Doctors.Select(d => d.PracticeNumber).ToListAsync();

            foreach (var doctorData in doctors)
            {
                var practiceNumber = int.Parse(doctorData.PracticeNumber);
                if (!existingDoctors.Contains(practiceNumber))
                {
                    var doctor = new Doctor
                    {
                        Name = doctorData.FirstName,
                        Surname = doctorData.LastName,
                        Email = doctorData.Email,
                        PhoneNumber = doctorData.PhoneNumber,
                        PracticeNumber = practiceNumber
                    };
                    _context.Doctors.Add(doctor);
                    result.SuccessCount++;
                }
                else
                {
                    result.SkippedCount++;
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }

        private async Task<ImportResult> ImportPharmacyManagers(List<PharmacyManagerData> managers)
        {
            var result = new ImportResult();
            var existingManagers = await _context.PharmacyManagers.Select(pm => pm.Email).ToListAsync();

            // Get or create default pharmacy
            var defaultPharmacy = await _context.Pharmacies.FirstOrDefaultAsync();
            if (defaultPharmacy == null)
            {
                // Get first pharmacist if exists, or skip pharmacist requirement
                var firstPharmacist = await _context.Pharmacists.FirstOrDefaultAsync();
                
                defaultPharmacy = new Pharmacy
                {
                    Name = "Default Pharmacy",
                    ContactNumber = "0000000000",
                    Email = "default@pharmacy.com",
                    HealthcareCouncilRegistrationNumber = "DEFAULT",
                    PharmacistId = firstPharmacist?.PharmacistId ?? 0
                };
                _context.Pharmacies.Add(defaultPharmacy);
                await _context.SaveChangesAsync();
            }

            foreach (var managerData in managers)
            {
                if (!existingManagers.Contains(managerData.Email))
                {
                    // Check if user exists, if not create one
                    var existingUser = await _userManager.FindByEmailAsync(managerData.Email);
                    if (existingUser == null)
                    {
                        // Create a new user for this manager
                        existingUser = new Users
                        {
                            UserName = managerData.Email,
                            Email = managerData.Email,
                            FullName = $"{managerData.FirstName} {managerData.LastName}",
                            EmailConfirmed = false,
                            LockoutEnabled = false
                        };
                        var password = $"{managerData.LastName}123!";
                        var createResult = await _userManager.CreateAsync(existingUser, password);
                        if (!createResult.Succeeded)
                        {
                            result.ErrorCount++;
                            _logger.LogWarning("Failed to create user for pharmacy manager {Email}: {Errors}", 
                                managerData.Email, string.Join(", ", createResult.Errors.Select(e => e.Description)));
                            continue;
                        }
                        
                        // Assign PharmacyManager role
                        if (!await _roleManager.RoleExistsAsync("PharmacyManager"))
                        {
                            await _roleManager.CreateAsync(new IdentityRole("PharmacyManager"));
                        }
                        await _userManager.AddToRoleAsync(existingUser, "PharmacyManager");
                    }

                    var manager = new PharmacyManager
                    {
                        UserId = existingUser.Id,
                        Name = managerData.FirstName,
                        Surname = managerData.LastName,
                        Email = managerData.Email,
                        ContactNumber = managerData.PhoneNumber,
                        PharmacyId = defaultPharmacy.PharmacyId
                    };
                    _context.PharmacyManagers.Add(manager);
                    result.SuccessCount++;
                }
                else
                {
                    result.SkippedCount++;
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }

        private async Task<ImportResult> ImportPharmacists(List<PharmacistData> pharmacists)
        {
            var result = new ImportResult();
            var existingPharmacists = await _context.Pharmacists.Select(p => p.RegistrationNumber).ToListAsync();

            foreach (var pharmacistData in pharmacists)
            {
                if (!existingPharmacists.Contains(pharmacistData.RegistrationNumber))
                {
                    // Check if user exists, if not create one
                    var existingUser = await _userManager.FindByEmailAsync(pharmacistData.Email);
                    if (existingUser == null)
                    {
                        // Create a new user for this pharmacist
                        existingUser = new Users
                        {
                            UserName = pharmacistData.Email,
                            Email = pharmacistData.Email,
                            FullName = $"{pharmacistData.FirstName} {pharmacistData.LastName}",
                            EmailConfirmed = false,
                            LockoutEnabled = false
                        };
                        var password = $"{pharmacistData.LastName}123!";
                        var createResult = await _userManager.CreateAsync(existingUser, password);
                        if (!createResult.Succeeded)
                        {
                            result.ErrorCount++;
                            _logger.LogWarning("Failed to create user for pharmacist {Email}: {Errors}", 
                                pharmacistData.Email, string.Join(", ", createResult.Errors.Select(e => e.Description)));
                            continue;
                        }
                        
                        // Assign Pharmacist role
                        if (!await _roleManager.RoleExistsAsync("Pharmacist"))
                        {
                            await _roleManager.CreateAsync(new IdentityRole("Pharmacist"));
                        }
                        await _userManager.AddToRoleAsync(existingUser, "Pharmacist");
                    }

                    var pharmacist = new Pharmacist
                    {
                        UserId = existingUser.Id,
                        Name = pharmacistData.FirstName,
                        Surname = pharmacistData.LastName,
                        Email = pharmacistData.Email,
                        CellPhone = pharmacistData.PhoneNumber,
                        RegistrationNumber = pharmacistData.RegistrationNumber
                    };
                    _context.Pharmacists.Add(pharmacist);
                    result.SuccessCount++;
                }
                else
                {
                    result.SkippedCount++;
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }

        private async Task<ImportResult> ImportMedications(List<MedicationData> medications)
        {
            var result = new ImportResult();
            var existingMedications = await _context.Medications.Select(m => m.Name).ToListAsync();

            // Get lookup dictionaries - case insensitive
            // Handle potential duplicates by grouping and taking the first one
            var dosageFormsList = await _context.Dosages.ToListAsync();
            var dosageForms = dosageFormsList
                .GroupBy(df => NormalizeDosageForm(df.Type.ToLowerInvariant()))
                .ToDictionary(g => g.Key, g => g.First().DosageFormId);
            
            var suppliers = await _context.Suppliers.ToDictionaryAsync(s => s.Name.ToLowerInvariant(), s => s.SupplierId);
            var activeIngredients = await _context.ActiveIngredients.ToDictionaryAsync(ai => ai.Name.ToLowerInvariant(), ai => ai.ActiveIngredientId);

            foreach (var medicationData in medications)
            {
                if (!existingMedications.Contains(medicationData.Name))
                {
                    // Find dosage form - case insensitive lookup with normalization
                    var dosageFormKey = NormalizeDosageForm(medicationData.DosageForm);
                    if (string.IsNullOrEmpty(dosageFormKey))
                    {
                        _logger.LogWarning("Could not find dosage form: '{DosageForm}' for medication: {MedicationName}", medicationData.DosageForm, medicationData.Name);
                        result.ErrorCount++;
                        continue;
                    }
                    
                    // If dosage form doesn't exist, create it
                    if (!dosageForms.TryGetValue(dosageFormKey, out var dosageFormId))
                    {
                        // Capitalize first letter for consistency
                        var capitalizedType = !string.IsNullOrEmpty(dosageFormKey) 
                            ? char.ToUpper(dosageFormKey[0]) + dosageFormKey.Substring(1).ToLowerInvariant() 
                            : medicationData.DosageForm;
                        
                        var newDosageForm = new DosageForm
                        {
                            Type = capitalizedType,
                            Description = $"Auto-created dosage form: {capitalizedType}"
                        };
                        _context.Dosages.Add(newDosageForm);
                        await _context.SaveChangesAsync();
                        
                        dosageFormId = newDosageForm.DosageFormId;
                        dosageForms[dosageFormKey] = dosageFormId; // Add to dictionary for subsequent lookups
                        _logger.LogInformation("Created new dosage form: '{DosageForm}' for medication: {MedicationName}", capitalizedType, medicationData.Name);
                    }

                    // Find supplier - case insensitive lookup
                    var supplierKey = medicationData.Supplier?.ToLowerInvariant();
                    if (string.IsNullOrEmpty(supplierKey))
                    {
                        _logger.LogWarning("Could not find supplier: '{Supplier}' for medication: {MedicationName}", medicationData.Supplier, medicationData.Name);
                        result.ErrorCount++;
                        continue;
                    }
                    
                    // If supplier doesn't exist, create it
                    if (!suppliers.TryGetValue(supplierKey, out var supplierId))
                    {
                        // Capitalize first letter for consistency
                        var capitalizedName = !string.IsNullOrEmpty(supplierKey) 
                            ? char.ToUpper(supplierKey[0]) + supplierKey.Substring(1).ToLowerInvariant() 
                            : medicationData.Supplier;
                        
                        var newSupplier = new Supplier
                        {
                            Name = capitalizedName,
                            ContactPerson = "Imported", // Single field
                            Email = $"info@{supplierKey.ToLowerInvariant().Replace(" ", "")}.com"
                        };
                        _context.Suppliers.Add(newSupplier);
                        await _context.SaveChangesAsync();
                        
                        supplierId = newSupplier.SupplierId;
                        suppliers[supplierKey] = supplierId; // Add to dictionary for subsequent lookups
                        _logger.LogInformation("Created new supplier: '{Supplier}' for medication: {MedicationName}", capitalizedName, medicationData.Name);
                    }

                    // Validate price - skip only if price is exactly 0 or negative (not if it's the 0.01 default)
                    if (medicationData.Price < 0)
                    {
                        _logger.LogWarning("Skipping medication '{MedicationName}' due to negative price: {Price}", 
                            medicationData.Name, medicationData.Price);
                        result.SkippedCount++;
                        continue;
                    }
                    
                    // Log if using default price
                    if (medicationData.Price == 0.01m)
                    {
                        _logger.LogWarning("Medication '{MedicationName}' imported with default price {Price} - price extraction failed, please update manually", 
                            medicationData.Name, medicationData.Price);
                    }

                    var medication = new Medication
                    {
                        Name = medicationData.Name,
                        Schedule = medicationData.Schedule,
                        DosageFormId = dosageFormId,
                        SupplierId = supplierId,
                        Price = (double)medicationData.Price,
                        MinStockLevel = medicationData.ReorderLevel,
                        QuantityInStock = medicationData.StockOnHand,
                        Description = $"Imported medication: {medicationData.Name}"
                    };

                    _logger.LogInformation("Importing medication: {Name}, Price: {Price}", medication.Name, medication.Price);
                    _context.Medications.Add(medication);
                    await _context.SaveChangesAsync(); // Save to get ID

                    // Add active ingredients - case insensitive lookup
                    foreach (var ingredientData in medicationData.ActiveIngredients)
                    {
                        var ingredientKey = ingredientData.Name?.ToLowerInvariant();
                        if (!string.IsNullOrEmpty(ingredientKey) && activeIngredients.TryGetValue(ingredientKey, out var ingredientId))
                        {
                            var medicationIngredient = new MedicationIngredient
                            {
                                MedicationId = medication.MedicationId,
                                ActiveIngredientId = ingredientId,
                                Strength = ingredientData.Strength
                            };
                            _context.MedicationIngredients.Add(medicationIngredient);
                        }
                    }

                    await _context.SaveChangesAsync();
                    result.SuccessCount++;
                }
                else
                {
                    result.SkippedCount++;
                }
            }

            return result;
        }

        /// <summary>
        /// Removes numerical data and special characters from a name for matching
        /// </summary>
        private string RemoveSpecialCharsAndNumbers(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;
            
            // Remove all numbers and special characters, keep only letters and spaces
            var cleaned = new string(name.Where(c => char.IsLetter(c) || char.IsWhiteSpace(c)).ToArray());
            return cleaned.Trim();
        }

        /// <summary>
        /// Finds a customer by name with improved matching logic that ignores numbers and special characters
        /// </summary>
        private Customer? FindCustomerByName(List<Customer> customers, string customerName)
        {
            if (string.IsNullOrWhiteSpace(customerName))
                return null;

            var normalizedName = customerName.Trim().ToLowerInvariant();
            var cleanedSearchName = RemoveSpecialCharsAndNumbers(normalizedName);
            
            // Try exact match first
            var customer = customers.FirstOrDefault(c => 
                $"{c.Name} {c.Surname}".Trim().ToLowerInvariant() == normalizedName);
            
            if (customer != null) return customer;
            
            // Try with cleaned name (removing numbers and special characters)
            if (!string.IsNullOrWhiteSpace(cleanedSearchName))
            {
                customer = customers.FirstOrDefault(c =>
                {
                    var cleanedCustomerName = RemoveSpecialCharsAndNumbers($"{c.Name} {c.Surname}".Trim().ToLowerInvariant());
                    return cleanedCustomerName == cleanedSearchName;
                });
                
                if (customer != null) return customer;
            }
            
            // Try partial match (first name + surname)
            var nameParts = normalizedName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (nameParts.Length >= 2)
            {
                var firstName = nameParts[0];
                var surname = string.Join(" ", nameParts.Skip(1));
                
                customer = customers.FirstOrDefault(c => 
                    c.Name?.ToLowerInvariant() == firstName &&
                    c.Surname?.ToLowerInvariant() == surname);
                
                if (customer != null) return customer;
            }
            
            // Try partial match with cleaned names
            if (!string.IsNullOrWhiteSpace(cleanedSearchName))
            {
                var cleanedParts = cleanedSearchName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (cleanedParts.Length >= 2)
                {
                    var firstName = cleanedParts[0];
                    var surname = string.Join(" ", cleanedParts.Skip(1));
                    
                    customer = customers.FirstOrDefault(c =>
                    {
                        var cleanedCustName = RemoveSpecialCharsAndNumbers(c.Name ?? "").ToLowerInvariant();
                        var cleanedCustSurname = RemoveSpecialCharsAndNumbers(c.Surname ?? "").ToLowerInvariant();
                        return cleanedCustName == firstName && cleanedCustSurname == surname;
                    });
                }
            }
            
            return customer;
        }

        /// <summary>
        /// Finds a doctor by name with improved matching logic
        /// </summary>
        private Doctor? FindDoctorByName(List<Doctor> doctors, string doctorName)
        {
            if (string.IsNullOrWhiteSpace(doctorName))
                return null;

            var normalizedName = doctorName.Trim().ToLowerInvariant();
            
            // Try exact match first
            var doctor = doctors.FirstOrDefault(d => 
                $"{d.Name} {d.Surname}".Trim().ToLowerInvariant() == normalizedName);
            
            if (doctor != null) return doctor;
            
            // Try partial match (first name + surname)
            var nameParts = normalizedName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (nameParts.Length >= 2)
            {
                var firstName = nameParts[0];
                var surname = string.Join(" ", nameParts.Skip(1));
                
                doctor = doctors.FirstOrDefault(d => 
                    d.Name?.ToLowerInvariant() == firstName &&
                    d.Surname?.ToLowerInvariant() == surname);
            }
            
            return doctor;
        }

        /// <summary>
        /// Parses frequency string to DosageFrequency enum
        /// </summary>
        private DosageFrequency ParseFrequency(string frequency)
        {
            if (string.IsNullOrWhiteSpace(frequency))
                return DosageFrequency.OnceDaily;

            var normalized = frequency.Trim().ToLowerInvariant();
            
            return normalized switch
            {
                "once daily" or "once a day" or "1x daily" or "1x" => DosageFrequency.OnceDaily,
                "twice daily" or "twice a day" or "2x daily" or "2x" => DosageFrequency.TwiceDaily,
                "three times daily" or "three times a day" or "3x daily" or "3x" => DosageFrequency.ThreeTimesDaily,
                "four times daily" or "four times a day" or "4x daily" or "4x" => DosageFrequency.FourTimesDaily,
                "as needed" or "prn" or "when needed" => DosageFrequency.AsNeeded,
                _ => DosageFrequency.OnceDaily // Default fallback
            };
        }

        /// <summary>
        /// Normalizes dosage form names to handle singular/plural variations
        /// </summary>
        private string NormalizeDosageForm(string form)
        {
            if (string.IsNullOrEmpty(form))
                return form;

            // Convert to lowercase for normalization
            var normalized = form.ToLowerInvariant().Trim();
            
            // Handle common plural to singular conversions
            if (normalized.EndsWith("s") && normalized.Length > 1 && !normalized.EndsWith("ss"))
            {
                // Check if it's a known plural word
                if (normalized == "tablets")
                    normalized = "tablet";
                else if (normalized == "capsules")
                    normalized = "capsule";
                else if (normalized == "drops")
                    normalized = "drop";
                else if (normalized == "patches")
                    normalized = "patch";
                else if (normalized == "powders")
                    normalized = "powder";
                else if (normalized == "solutions")
                    normalized = "solution";
                else if (normalized == "suspensions")
                    normalized = "suspension";
                else if (normalized == "injections")
                    normalized = "injection";
                else if (normalized == "syrups")
                    normalized = "syrup";
                else if (normalized == "cream" || normalized == "creams")
                    normalized = "cream";
                else if (normalized == "inhaler" || normalized == "inhalers")
                    normalized = "inhaler";
                // For other words ending in 's', remove the 's'
                else
                {
                    normalized = normalized.Substring(0, normalized.Length - 1);
                }
            }

            return normalized;
        }

        // POST: Upload and Parse XLSX
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadXlsx(IFormFile xlsxFile)
        {
            if (xlsxFile == null || xlsxFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Please select an XLSX file to upload.";
                return RedirectToAction("Index");
            }

            // Validate file
            var allowedExtensions = new[] { ".xlsx", ".xls" };
            var ext = Path.GetExtension(xlsxFile.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(ext))
            {
                TempData["ErrorMessage"] = "Only XLSX files are allowed.";
                return RedirectToAction("Index");
            }

            if (xlsxFile.Length > 50 * 1024 * 1024) // 50MB limit
            {
                TempData["ErrorMessage"] = "File size cannot exceed 50MB.";
                return RedirectToAction("Index");
            }

            try
            {
                // Parse XLSX content - now returns comprehensive data
                var parsedData = await _xlsxImportService.ParseXlsxAsync(xlsxFile);
                
                _logger.LogInformation("Parsed XLSX: {CustomerCount} customers, {StockOrderCount} stock orders, {PrescriptionCount} prescriptions, {OrderCount} orders",
                    parsedData.Customers.Count, parsedData.StockOrders.Count, parsedData.Prescriptions.Count, parsedData.Orders.Count);
                
                // Store parsed data in session for preview
                HttpContext.Session.SetString("XlsxParsedData", System.Text.Json.JsonSerializer.Serialize(parsedData));
                
                // Redirect to preview page
                return RedirectToAction("PreviewXlsxData");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing XLSX file");
                TempData["ErrorMessage"] = $"An error occurred while parsing the XLSX file: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // GET: Preview customers
        public IActionResult PreviewCustomers()
        {
            var jsonData = HttpContext.Session.GetString("XlsxCustomers");
            if (string.IsNullOrEmpty(jsonData))
            {
                TempData["ErrorMessage"] = "No parsed data found. Please upload an XLSX file first.";
                return RedirectToAction("Index");
            }

            var customers = System.Text.Json.JsonSerializer.Deserialize<List<CustomerData>>(jsonData);
            
            return View(customers);
        }

        // GET: Preview comprehensive XLSX data
        public IActionResult PreviewXlsxData()
        {
            var jsonData = HttpContext.Session.GetString("XlsxParsedData");
            if (string.IsNullOrEmpty(jsonData))
            {
                TempData["ErrorMessage"] = "No parsed data found. Please upload an XLSX file first.";
                return RedirectToAction("Index");
            }

            var parsedData = System.Text.Json.JsonSerializer.Deserialize<XlsxParsedData>(jsonData);
            
            return View(parsedData);
        }

        // POST: Execute comprehensive import
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExecuteImportXlsxData()
        {
            var jsonData = HttpContext.Session.GetString("XlsxParsedData");
            if (string.IsNullOrEmpty(jsonData))
            {
                TempData["ErrorMessage"] = "No parsed data found. Please upload an XLSX file first.";
                return RedirectToAction("Index");
            }

            var parsedData = System.Text.Json.JsonSerializer.Deserialize<XlsxParsedData>(jsonData);
            
            try
            {
                _logger.LogInformation("Starting comprehensive import. Customers: {C}, StockOrders: {SO}, Prescriptions: {P}, Orders: {O}",
                    parsedData.Customers.Count, parsedData.StockOrders.Count, parsedData.Prescriptions.Count, parsedData.Orders.Count);
                
                var results = new ImportResultsViewModel();
                
                
                // Import Customers
                if (parsedData.Customers.Any())
                {
                    try
                    {
                        _logger.LogInformation("Importing {Count} customers...", parsedData.Customers.Count);
                        results.CustomersResult = await ImportCustomers(parsedData.Customers);
                        _logger.LogInformation("Customers imported: Success={Success}, Skipped={Skipped}, Errors={Errors}",
                            results.CustomersResult.SuccessCount, results.CustomersResult.SkippedCount, results.CustomersResult.ErrorCount);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to import customers");
                        results.CustomersResult.ErrorCount = parsedData.Customers.Count;
                        results.CustomersResult.Errors.Add($"Failed to import customers: {ex.Message}");
                    }
                }
                
                // Import Stock Orders
                if (parsedData.StockOrders.Any())
                {
                    try
                    {
                        _logger.LogInformation("Importing {Count} stock orders...", parsedData.StockOrders.Count);
                        results.StockOrdersResult = await ImportStockOrders(parsedData.StockOrders);
                        _logger.LogInformation("Stock orders imported: Success={Success}, Skipped={Skipped}, Errors={Errors}",
                            results.StockOrdersResult.SuccessCount, results.StockOrdersResult.SkippedCount, results.StockOrdersResult.ErrorCount);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to import stock orders");
                        results.StockOrdersResult.ErrorCount = parsedData.StockOrders.Count;
                        results.StockOrdersResult.Errors.Add($"Failed to import stock orders: {ex.Message}");
                    }
                }
                
                // Import Prescriptions
                if (parsedData.Prescriptions.Any())
                {
                    try
                    {
                        _logger.LogInformation("Importing {Count} prescriptions...", parsedData.Prescriptions.Count);
                        results.PrescriptionsResult = await ImportPrescriptions(parsedData.Prescriptions);
                        _logger.LogInformation("Prescriptions imported: Success={Success}, Skipped={Skipped}, Errors={Errors}",
                            results.PrescriptionsResult.SuccessCount, results.PrescriptionsResult.SkippedCount, results.PrescriptionsResult.ErrorCount);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to import prescriptions");
                        results.PrescriptionsResult.ErrorCount = parsedData.Prescriptions.Count;
                        results.PrescriptionsResult.Errors.Add($"Failed to import prescriptions: {ex.Message}");
                    }
                }
                
                // Import Customer Orders (kept as Pending to surface in OrdersForCustomers)
                if (parsedData.Orders.Any())
                {
                    try
                    {
                        _logger.LogInformation("Importing {Count} customer orders...", parsedData.Orders.Count);
                        results.OrdersResult = await ImportOrders(parsedData.Orders);
                        _logger.LogInformation("Customer orders imported: Success={Success}, Skipped={Skipped}, Errors={Errors}",
                            results.OrdersResult.SuccessCount, results.OrdersResult.SkippedCount, results.OrdersResult.ErrorCount);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to import customer orders");
                        results.OrdersResult.ErrorCount = parsedData.Orders.Count;
                        results.OrdersResult.Errors.Add($"Failed to import customer orders: {ex.Message}");
                    }
                }
                
                // Clear session data
                HttpContext.Session.Remove("XlsxParsedData");
                
                // Store results in Session (not TempData to avoid size limits)
                var sessionKey = $"ImportResults_{Guid.NewGuid()}";
                HttpContext.Session.SetString(sessionKey, System.Text.Json.JsonSerializer.Serialize(results));
                
                // Store the session key in TempData (this is small)
                TempData["ImportResultsKey"] = sessionKey;
                _logger.LogInformation("Import completed. Redirecting to results page with key: {Key}", sessionKey);
                
                return RedirectToAction("ImportResults");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during import. Stack trace: {StackTrace}", ex.StackTrace);
                TempData["ErrorMessage"] = $"An error occurred during import: {ex.Message}";
                return RedirectToAction("PreviewXlsxData");
            }
        }

        // GET: Display import results
        public IActionResult ImportResults()
        {
            var sessionKey = TempData["ImportResultsKey"] as string;
            if (string.IsNullOrEmpty(sessionKey))
            {
                TempData["ErrorMessage"] = "No import results found. Please perform an import first.";
                return RedirectToAction("Index");
            }

            var resultsJson = HttpContext.Session.GetString(sessionKey);
            if (string.IsNullOrEmpty(resultsJson))
            {
                TempData["ErrorMessage"] = "Import results have expired or were not found. Please perform a new import.";
                return RedirectToAction("Index");
            }

            var model = System.Text.Json.JsonSerializer.Deserialize<ImportResultsViewModel>(resultsJson);
            
            // Clear the session data after reading
            HttpContext.Session.Remove(sessionKey);
            
            return View(model);
        }

        // POST: Execute customer import
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExecuteImportCustomers()
        {
            var jsonData = HttpContext.Session.GetString("XlsxCustomers");
            if (string.IsNullOrEmpty(jsonData))
            {
                TempData["ErrorMessage"] = "No parsed data found. Please upload an XLSX file first.";
                return RedirectToAction("Index");
            }

            var customers = System.Text.Json.JsonSerializer.Deserialize<List<CustomerData>>(jsonData);
            
            try
            {
                var result = await ImportCustomers(customers);
                
                // Clear session data
                HttpContext.Session.Remove("XlsxCustomers");
                
                TempData["SuccessMessage"] = $"Successfully imported {result.SuccessCount} customers. {result.SkippedCount} were skipped.";
                if (result.ErrorCount > 0)
                {
                    TempData["WarningMessage"] = $"{result.ErrorCount} customers had errors during import.";
                }
                
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during customer import");
                TempData["ErrorMessage"] = $"An error occurred during import: {ex.Message}";
                return RedirectToAction("PreviewCustomers");
            }
        }

        private async Task<ImportResult> ImportCustomers(List<CustomerData> customers)
        {
            var result = new ImportResult();
            var existingCustomers = await _context.Customers.Select(c => c.IDNumber).ToListAsync();

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                foreach (var customerData in customers)
                {
                    // Check if customer already exists by ID number
                    if (existingCustomers.Contains(customerData.IDNumber))
                    {
                        result.SkippedCount++;
                        _logger.LogInformation("Skipping existing customer: {IDNumber}", customerData.IDNumber);
                        continue;
                    }

                    // Check if user exists with this email
                    var existingUser = await _userManager.FindByEmailAsync(customerData.Email);
                    if (existingUser != null)
                    {
                        _logger.LogWarning("User already exists with email: {Email}", customerData.Email);
                        result.ErrorCount++;
                        result.Errors.Add($"User with email {customerData.Email} already exists");
                        continue;
                    }

                    // Generate password based on surname
                    var password = $"{customerData.Surname}123!";

                    try
                    {
                        // Create a new user for this customer
                        var newUser = new Users
                        {
                            UserName = customerData.Email,
                            Email = customerData.Email,
                            FullName = $"{customerData.FirstName} {customerData.Surname}",
                            EmailConfirmed = false,
                            LockoutEnabled = false
                        };

                        var createResult = await _userManager.CreateAsync(newUser, password);
                        if (!createResult.Succeeded)
                        {
                            result.ErrorCount++;
                            var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                            result.Errors.Add($"Failed to create user for {customerData.FullName}: {errors}");
                            _logger.LogWarning("Failed to create user for customer {Name}: {Errors}",
                                customerData.FullName, errors);
                            continue;
                        }

                        // Assign Customer role
                        if (!await _roleManager.RoleExistsAsync("Customer"))
                        {
                            await _roleManager.CreateAsync(new IdentityRole("Customer"));
                        }
                        await _userManager.AddToRoleAsync(newUser, "Customer");

                        // Create customer
                        var customer = new Customer
                        {
                            UserId = newUser.Id,
                            Name = customerData.FirstName,
                            Surname = customerData.Surname,
                            IDNumber = customerData.IDNumber,
                            CellPhoneNumber = customerData.PhoneNumber,
                            Email = customerData.Email,
                            Street = customerData.Street,
                            Suburb = customerData.Suburb,
                            City = customerData.City,
                            Province = customerData.Province,
                            Country = "South Africa",
                            IsWalkInCustomer = false,
                            DateCreated = DateTime.Now
                        };

                        _context.Customers.Add(customer);
                        await _context.SaveChangesAsync(); // Save to get CustomerId

                        // Add allergies if provided
                        if (customerData.AllergyList?.Any() == true)
                        {
                            foreach (var allergyName in customerData.AllergyList)
                            {
                                var allergyNameTrimmed = allergyName.Trim();
                                if (!string.IsNullOrWhiteSpace(allergyNameTrimmed))
                                {
                                    // Find or create active ingredient for the allergy
                                    var activeIngredient = await _context.ActiveIngredients
                                        .FirstOrDefaultAsync(ai => ai.Name != null && ai.Name.ToLower() == allergyNameTrimmed.ToLower());

                                    if (activeIngredient == null)
                                    {
                                        activeIngredient = new ActiveIngredients
                                        {
                                            Name = allergyNameTrimmed,
                                            Description = $"Auto-created from customer import",
                                            Strength = "N/A"
                                        };
                                        _context.ActiveIngredients.Add(activeIngredient);
                                        await _context.SaveChangesAsync(); // Save to get ActiveIngredientId
                                    }

                                    // Create customer allergy
                                    var customerAllergy = new CustomerAllergy
                                    {
                                        CustomerId = customer.CustomerId,
                                        ActiveIngredientId = activeIngredient.ActiveIngredientId,
                                        Severity = "Medium", // Default severity
                                        Description = $"Allergy to {allergyNameTrimmed}"
                                    };

                                    _context.CustomerAllergies.Add(customerAllergy);
                                }
                            }
                        }

                        await _context.SaveChangesAsync();
                        result.SuccessCount++;
                        _logger.LogInformation("Successfully imported customer: {Name} with ID: {IDNumber}", 
                            customerData.FullName, customerData.IDNumber);
                    }
                    catch (Exception ex)
                    {
                        result.ErrorCount++;
                        result.Errors.Add($"Error importing customer {customerData.FullName}: {ex.Message}");
                        _logger.LogError(ex, "Error importing customer {Name}", customerData.FullName);
                    }
                }

                await transaction.CommitAsync();
                return result;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error during customer import transaction");
                throw;
            }
        }

        private async Task<ImportResult> ImportStockOrders(List<StockOrderData> stockOrders)
        {
            var result = new ImportResult();
            
            if (!stockOrders.Any())
                return result;
            
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // Get lookup dictionaries
                var suppliers = await _context.Suppliers.ToDictionaryAsync(s => s.Name.ToLowerInvariant(), s => s.SupplierId);
                var medications = await _context.Medications.ToDictionaryAsync(m => m.Name.ToLowerInvariant(), m => m.MedicationId);
                
                // Group stock orders by supplier, date, and status to create one order per "mega row"
                var groupedOrders = stockOrders
                    .GroupBy(so => new { so.Supplier, so.Date, so.OrderStatus })
                    .ToList();
                
                foreach (var group in groupedOrders)
                {
                    try
                    {
                        // Get the first item in the group to get order-level details
                        var firstItem = group.First();
                        
                        // Find supplier
                        var supplierKey = firstItem.Supplier.ToLowerInvariant();
                        if (!suppliers.TryGetValue(supplierKey, out var supplierId))
                        {
                            result.SkippedCount++;
                            _logger.LogWarning("Supplier not found: {Supplier}", firstItem.Supplier);
                            continue;
                        }
                        
                        // All imported stock orders should be "Pending" regardless of Excel status
                        var orderStatus = StockOrderStatusEnum.Pending;
                        
                        // Create ONE stock order for this group
                        var stockOrder = new StockOrder
                        {
                            StockOrderDate = firstItem.Date,
                            SupplierId = supplierId,
                            StockOrderStatus = orderStatus,
                            Notes = $"Imported from XLSX"
                        };
                        
                        _context.StockOrders.Add(stockOrder);
                        await _context.SaveChangesAsync();
                        
                        int itemsAdded = 0;
                        
                        // Create stock order items for ALL medications in this group
                        foreach (var stockOrderData in group)
                        {
                            // Find medication
                            var medicationKey = stockOrderData.Medication.ToLowerInvariant();
                            if (!medications.TryGetValue(medicationKey, out var medicationId))
                            {
                                result.ErrorCount++;
                                result.Errors.Add($"Medication not found for order from {firstItem.Supplier}: {stockOrderData.Medication}");
                                _logger.LogWarning("Medication not found: {Medication}", stockOrderData.Medication);
                                continue;
                            }
                            
                            // Create stock order item
                            var stockOrderItem = new StockOrderItem
                            {
                                StockOrderId = stockOrder.StockOrderId,
                                MedicationId = medicationId,
                                QuantityOrdered = stockOrderData.Quantity,
                                Notes = $"Medication: {stockOrderData.Medication}"
                            };
                            
                            _context.StockOrderItems.Add(stockOrderItem);
                            itemsAdded++;
                        }
                        
                        await _context.SaveChangesAsync();
                        
                        if (itemsAdded > 0)
                        {
                            result.SuccessCount++;
                            _logger.LogInformation("Created stock order with {ItemCount} items from supplier {Supplier}", 
                                itemsAdded, firstItem.Supplier);
                        }
                        else
                        {
                            result.ErrorCount++;
                            result.Errors.Add($"No valid medications found for order from {firstItem.Supplier}");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.ErrorCount++;
                        result.Errors.Add($"Error importing stock order group: {ex.Message}");
                        _logger.LogError(ex, "Error importing stock order group");
                    }
                }
                
                await transaction.CommitAsync();
                return result;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error during stock order import transaction");
                throw;
            }
        }

        private async Task<ImportResult> ImportPrescriptions(List<PrescriptionData> prescriptions)
        {
            var result = new ImportResult();
            
            if (!prescriptions.Any())
                return result;
            
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // Get medications lookup (should be unique by name) - load full medication objects to access price
                var medications = await _context.Medications.ToDictionaryAsync(m => m.Name.ToLowerInvariant(), m => m);
                var pharmacists = await _context.Pharmacists.Where(p => p.IsActive).ToListAsync();
                
                // Load all customers and doctors once to avoid repeated queries
                var allCustomers = await _context.Customers.ToListAsync();
                var allDoctors = await _context.Doctors.ToListAsync();
                
                foreach (var prescriptionData in prescriptions)
                {
                    try
                    {
                        // Find customer (improved lookup logic)
                        var customer = FindCustomerByName(allCustomers, prescriptionData.Customer);
                        
                        if (customer == null)
                        {
                            result.SkippedCount++;
                            result.Errors.Add($"Customer '{prescriptionData.Customer}' not found");
                            _logger.LogWarning("Customer not found: {Customer}", prescriptionData.Customer);
                            continue;
                        }
                        
                        // Find doctor (improved lookup logic)
                        int? doctorId = null;
                        if (!string.IsNullOrWhiteSpace(prescriptionData.Doctor))
                        {
                            var doctor = FindDoctorByName(allDoctors, prescriptionData.Doctor);
                            if (doctor == null)
                            {
                                result.SkippedCount++;
                                result.Errors.Add($"Doctor '{prescriptionData.Doctor}' not found");
                                _logger.LogWarning("Doctor not found: {Doctor}", prescriptionData.Doctor);
                                continue;
                            }
                            doctorId = doctor.DoctorId;
                        }
                        
                        // Find medication
                        var medicationKey = prescriptionData.Medication.ToLowerInvariant();
                        if (!medications.TryGetValue(medicationKey, out var medication))
                        {
                            result.SkippedCount++;
                            _logger.LogWarning("Medication not found: {Medication}", prescriptionData.Medication);
                            continue;
                        }
                        
                        var medicationId = medication.MedicationId;
                        
                        // Assign pharmacist (round-robin if multiple prescriptions)
                        var pharmacist = pharmacists.Any() ? pharmacists[result.SuccessCount % pharmacists.Count] : null;
                        
                        // Parse frequency from string to enum
                        var frequency = ParseFrequency(prescriptionData.Frequency);
                        
                        // Create the Prescription for import - set as Pending
                        var prescription = new Prescription
                        {
                            CustomerId = customer.CustomerId,
                            DoctorId = doctorId,
                            PharmacistId = null, // Not assigned yet - will be pending
                            UploadId = null,
                            PrescriptionDate = prescriptionData.Date
                        };
                        
                        _context.Prescriptions.Add(prescription);
                        await _context.SaveChangesAsync();
                        
                        // Create prescription line with repeats
                        var prescriptionLine = new PrescriptionLine
                        {
                            PrescriptionId = prescription.PrescriptionId,
                            MedicationId = medicationId,
                            Quantity = prescriptionData.Quantity,
                            Instructions = prescriptionData.Instructions,
                            Frequency = frequency,
                            TotalRepeats = prescriptionData.Repeats,
                            RepeatsRemaining = prescriptionData.Repeats
                        };
                        
                        _context.PrescriptionLines.Add(prescriptionLine);
                        await _context.SaveChangesAsync();
                        
                        // Ensure repeats are created for customer medication history
                        var prescriptionRepeat = new PrescriptionRepeat
                        {
                            PrescriptionLineId = prescriptionLine.PrescriptionLineId,
                            CustomerId = customer.CustomerId,
                            TotalRepeats = prescriptionData.Repeats,
                            RemainingRepeats = prescriptionData.Repeats,
                            QuantityPerRepeat = prescriptionData.Quantity,
                            DispensedCount = 0,
                            CreatedDate = prescriptionData.Date,
                            DateCreated = DateTime.UtcNow,
                            IsActive = prescriptionData.Repeats > 0
                        };
                        _context.PrescriptionRepeats.Add(prescriptionRepeat);
                        await _context.SaveChangesAsync();
                        
                        // Create DispensedPrescription so it appears in the dispensed prescriptions view
                        if (pharmacist != null)
                        {
                            // Calculate amount due based on medication price and quantity
                            var amountDue = (decimal)(medication.Price * prescriptionData.Quantity);
                            
                            var dispensedPrescription = new DispensedPrescription
                            {
                                PrescriptionLineId = prescriptionLine.PrescriptionLineId,
                                PharmacistId = pharmacist.PharmacistId,
                                DispensedDate = prescriptionData.Date,
                                QuantityDispensed = prescriptionData.Quantity,
                                AmountDue = amountDue,
                                IsPaid = false,
                                DispensingNotes = $"Imported from XLSX on {DateTime.UtcNow:yyyy-MM-dd}"
                            };
                            
                            _context.DispensedPrescriptions.Add(dispensedPrescription);
                            await _context.SaveChangesAsync();
                        }
                        
                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        result.ErrorCount++;
                        result.Errors.Add($"Error importing prescription for {prescriptionData.Customer}: {ex.Message}");
                        _logger.LogError(ex, "Error importing prescription");
                    }
                }
            
            await transaction.CommitAsync();
            return result;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error during prescription import transaction");
                throw;
            }
        }

        private async Task<ImportResult> ImportOrders(List<OrderData> orders)
        {
            var result = new ImportResult();
            
            if (!orders.Any())
                return result;
            
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // Get medications lookup (should be unique by name)
                var medications = await _context.Medications.ToDictionaryAsync(m => m.Name.ToLowerInvariant(), m => m);
                var pharmacists = await _context.Pharmacists.Where(p => p.IsActive).ToListAsync();
                
                // Load all customers once to avoid repeated queries
                var allCustomers = await _context.Customers.ToListAsync();
                
                foreach (var orderData in orders)
                {
                    try
                    {
                        // Find customer (improved lookup logic)
                        var customer = FindCustomerByName(allCustomers, orderData.Customer);
                        
                        if (customer == null)
                        {
                            result.SkippedCount++;
                            result.Errors.Add($"Customer '{orderData.Customer}' not found");
                            _logger.LogWarning("Customer not found: {Customer}", orderData.Customer);
                            continue;
                        }
                    
                    if (!orderData.MedicationList.Any())
                    {
                        result.SkippedCount++;
                        _logger.LogWarning("No medications in order for customer: {Customer}", orderData.Customer);
                        continue;
                    }
                    
                    // Assign pharmacist (round-robin)
                    var pharmacist = pharmacists.Any() ? pharmacists[result.SuccessCount % pharmacists.Count] : null;
                    
                    // All imported orders should be "Pending" regardless of Excel status
                    var orderStatus = OrderStatusEnum.Pending;
                    
                    // Create order
                    var order = new Order
                    {
                        CustomerId = customer.CustomerId,
                        PharmacistId = pharmacist?.PharmacistId,
                        OrderDate = orderData.OrderDate ?? DateTime.UtcNow,
                        OrderStatus = orderStatus,
                        PaymentStatus = false,
                        TotalAmount = 0 // Will calculate after items
                    };
                    
                    _context.Orders.Add(order);
                    await _context.SaveChangesAsync();
                    
                    double totalAmount = 0;
                    
                    // Create order items
                    foreach (var medicationName in orderData.MedicationList)
                    {
                        var medicationKey = medicationName.ToLowerInvariant();
                        if (medications.TryGetValue(medicationKey, out var medication))
                        {
                            var orderItem = new OrderItem
                            {
                                OrderId = order.OrderId,
                                MedicationId = medication.MedicationId,
                                QuantityOrdered = 1, // Default quantity
                                QuantityDispensed = 0,
                                UnitPrice = medication.Price,
                                DispensingStatus = DispensingStatusEnum.Pending
                            };
                            
                            _context.OrderItems.Add(orderItem);
                            totalAmount += medication.Price;
                        }
                    }
                    
                    // Update total amount
                    order.TotalAmount = totalAmount;
                    await _context.SaveChangesAsync();
                    
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    result.ErrorCount++;
                    result.Errors.Add($"Error importing order for {orderData.Customer}: {ex.Message}");
                    _logger.LogError(ex, "Error importing order");
                }
            }
            
            await transaction.CommitAsync();
            return result;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error during order import transaction");
                throw;
            }
        }
    }
}
