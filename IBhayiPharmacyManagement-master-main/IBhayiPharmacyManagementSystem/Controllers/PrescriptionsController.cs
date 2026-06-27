using IBhayiPharmacyManagementSystem.Data;
using IBhayiPharmacyManagementSystem.Enums;
using IBhayiPharmacyManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using static IBhayiPharmacyManagementSystem.Models.UnprocessedScript;

namespace IBhayiPharmacyManagementSystem.Controllers
{
    public class PrescriptionsController : Controller
    {
        private readonly SignInManager<Users> _signInManager;
        private readonly UserManager<Users> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _context;
        private readonly ILogger<PrescriptionsController> _logger;

        public PrescriptionsController(SignInManager<Users> signInManager,
            UserManager<Users> userManager,
            RoleManager<IdentityRole> roleManager, AppDbContext context,
            ILogger<PrescriptionsController> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _logger = logger;
        }

        // GET: Prescriptions
        public async Task<IActionResult> Index()
        {
            return View(await _context.Prescriptions.ToListAsync());
        }

        // GET: Prescriptions/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var prescription = await _context.Prescriptions
                .FirstOrDefaultAsync(m => m.PrescriptionId == id);
            if (prescription == null)
            {
                return NotFound();
            }

            return View(prescription);
        }

        // GET: Prescriptions/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Prescriptions/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Prescription prescription)
        {
            if (ModelState.IsValid)
            {
                _context.Add(prescription);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(prescription);
        }

        // GET: Prescriptions/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var prescription = await _context.Prescriptions.FindAsync(id);
            if (prescription == null)
            {
                return NotFound();
            }
            return View(prescription);
        }

        // POST: Prescriptions/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("PrescriptionId,CustomerId,DoctorId,PharmacistId,UploadId")] Prescription prescription)
        {
            if (id != prescription.PrescriptionId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(prescription);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PrescriptionExists(prescription.PrescriptionId))
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
            return View(prescription);
        }

        // GET: Prescriptions/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var prescription = await _context.Prescriptions
                .FirstOrDefaultAsync(m => m.PrescriptionId == id);
            if (prescription == null)
            {
                return NotFound();
            }

            return View(prescription);
        }

        // POST: Prescriptions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var prescription = await _context.Prescriptions.FindAsync(id);
                if (prescription == null)
                {
                    TempData["ErrorMessage"] = "Prescription not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Check for linked prescription lines or dispensation requests
                var hasPrescriptionLines = await _context.PrescriptionLines.AnyAsync(pl => pl.PrescriptionId == id);
                var hasDispensationRequests = await _context.DispensationRequests.AnyAsync(dr => dr.PrescriptionRepeat.PrescriptionLine.PrescriptionId == id); // Assuming this link path

                if (hasPrescriptionLines || hasDispensationRequests)
                {
                    TempData["ErrorMessage"] = "Cannot delete prescription because it is linked to existing prescription lines or dispensation requests.";
                    return RedirectToAction(nameof(Delete), new { id = id }); // Redirect back to the GET Delete view with error
                }

                _context.Prescriptions.Remove(prescription);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Prescription deleted successfully!";
            }
            catch (DbUpdateException ex)
            {
                TempData["ErrorMessage"] = "A database error occurred while deleting the prescription. It might be referenced by other records.";
                _logger.LogError(ex, "Error deleting prescription {PrescriptionId}", id);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An unexpected error occurred while deleting the prescription.";
                _logger.LogError(ex, "Error deleting prescription {PrescriptionId}", id);
            }

            return RedirectToAction(nameof(Index));
        }

        private bool PrescriptionExists(int id)
        {
            return _context.Prescriptions.Any(e => e.PrescriptionId == id);
        }

        // GET: Test endpoint accessibility
        [HttpGet]
        public IActionResult RegisterDoctorTest()
        {
            return Json(new { success = true, message = "RegisterDoctor endpoint is accessible", timestamp = DateTime.UtcNow });
        }

        // GET: Health check for PrescriptionsController
        [HttpGet]
        public IActionResult HealthCheck()
        {
            return Json(new { 
                success = true, 
                message = "PrescriptionsController is accessible", 
                timestamp = DateTime.UtcNow,
                controller = "PrescriptionsController",
                endpoints = new[] { "RegisterDoctor", "HealthCheck" }
            });
        }

        // POST: Register new doctor
        [HttpPost]
        public async Task<IActionResult> RegisterDoctor()
        {
            Console.WriteLine("=== RegisterDoctor method called ===");

            // Check if this is an AJAX request
            bool isAjaxRequest = Request.Headers.ContainsKey("X-Requested-With") &&
                                Request.Headers["X-Requested-With"].ToString() == "XMLHttpRequest";

            Console.WriteLine($"Is AJAX request: {isAjaxRequest}");

            try
            {
                // Parse form data manually since we're not using model binding
                var formData = Request.Form;

                // Extract doctor data from form
                string name = formData["NewDoctorName"];
                string surname = formData["NewDoctorSurname"];
                string practiceNumberStr = formData["NewDoctorPracticeNumber"];
                string email = formData["NewDoctorEmail"];
                string phoneNumber = formData["NewDoctorPhone"];

                Console.WriteLine($"Received data - Name: '{name}', Surname: '{surname}', PracticeNumber: '{practiceNumberStr}', Email: '{email}', Phone: '{phoneNumber}'");
                Console.WriteLine($"Form data keys: {string.Join(", ", formData.Keys)}");

                // Validate required fields
                if (string.IsNullOrWhiteSpace(name) ||
                    string.IsNullOrWhiteSpace(surname) ||
                    string.IsNullOrWhiteSpace(practiceNumberStr))
                {
                    Console.WriteLine("Validation failed - missing required fields");
                    if (isAjaxRequest)
                    {
                        return Json(new { success = false, message = "Name, surname, and practice number are required fields." });
                    }
                    TempData["ErrorMessage"] = "Name, surname, and practice number are required fields.";
                    return RedirectToAction("Index");
                }

                // Parse practice number
                if (!int.TryParse(practiceNumberStr, out int practiceNumber))
                {
                    Console.WriteLine("Validation failed - invalid practice number format");
                    if (isAjaxRequest)
                    {
                        return Json(new { success = false, message = "Practice number must be a valid number." });
                    }
                    TempData["ErrorMessage"] = "Practice number must be a valid number.";
                    return RedirectToAction("Index");
                }

                // Check if practice number already exists
                var existingDoctor = await _context.Doctors
                    .FirstOrDefaultAsync(d => d.PracticeNumber == practiceNumber);

                if (existingDoctor != null)
                {
                    Console.WriteLine($"Validation failed - practice number {practiceNumber} already exists for doctor: {existingDoctor.Name} {existingDoctor.Surname}");
                    if (isAjaxRequest)
                    {
                        return Json(new { success = false, message = $"Practice number {practiceNumber} is already registered to Dr. {existingDoctor.Name} {existingDoctor.Surname}. Please use a unique practice number." });
                    }
                    TempData["ErrorMessage"] = $"Practice number {practiceNumber} is already registered to Dr. {existingDoctor.Name} {existingDoctor.Surname}. Please use a unique practice number.";
                    return RedirectToAction("Index");
                }

                Console.WriteLine("Validation passed - creating doctor");

                // Create doctor record
                var doctor = new Doctor
                {
                    Name = name.Trim(),
                    Surname = surname.Trim(),
                    Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
                    PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim(),
                    PracticeNumber = practiceNumber
                };

                Console.WriteLine($"Created doctor object: Name={doctor.Name}, Surname={doctor.Surname}, PracticeNumber={doctor.PracticeNumber}");

                // Validate the doctor object
                var validationContext = new ValidationContext(doctor);
                var validationResults = new List<ValidationResult>();
                bool isValid = Validator.TryValidateObject(doctor, validationContext, validationResults, true);

                if (!isValid)
                {
                    var errorMessages = validationResults.Select(vr => vr.ErrorMessage).ToList();
                    Console.WriteLine($"Model validation failed: {string.Join(", ", errorMessages)}");
                    Console.WriteLine($"Doctor object: Name='{doctor.Name}', Surname='{doctor.Surname}', PracticeNumber={doctor.PracticeNumber}, Email='{doctor.Email}', PhoneNumber='{doctor.PhoneNumber}'");
                    if (isAjaxRequest)
                    {
                        return Json(new { success = false, message = string.Join(", ", errorMessages) });
                    }
                    TempData["ErrorMessage"] = string.Join(", ", errorMessages);
                    return RedirectToAction("Index");
                }

                // Save doctor to database
                Console.WriteLine($"About to save doctor to database: {doctor.Name} {doctor.Surname}, PracticeNumber: {doctor.PracticeNumber}");
                _context.Doctors.Add(doctor);
                try
                {
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"Doctor saved successfully with ID: {doctor.DoctorId}");
                }
                catch (DbUpdateException dbEx)
                {
                    Console.WriteLine($"Database error: {dbEx.Message}");
                    if (dbEx.InnerException != null)
                    {
                        Console.WriteLine($"Inner exception: {dbEx.InnerException.Message}");
                    }
                    
                    // Check for unique constraint violations
                    if (dbEx.InnerException?.Message.Contains("UNIQUE") == true || 
                        dbEx.InnerException?.Message.Contains("duplicate") == true ||
                        dbEx.InnerException?.Message.Contains("PRIMARY KEY") == true)
                    {
                        if (isAjaxRequest)
                        {
                            return Json(new { success = false, message = "A doctor with this practice number already exists." });
                        }
                        TempData["ErrorMessage"] = "A doctor with this practice number already exists.";
                        return RedirectToAction("Index");
                    }
                    
                    throw; // Re-throw if it's not a constraint violation
                }

                Console.WriteLine($"Doctor saved successfully with ID: {doctor.DoctorId}");

                TempData["SuccessMessage"] = $"Doctor Dr. {doctor.Name} {doctor.Surname} registered successfully!";

                if (isAjaxRequest)
                {
                    return Json(new
                    {
                        success = true,
                        message = $"Doctor Dr. {doctor.Name} {doctor.Surname} registered successfully!",
                        doctorId = doctor.DoctorId,
                        doctorName = $"Dr. {doctor.Name} {doctor.Surname}"
                    });
                }
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Doctor registration error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");

                if (isAjaxRequest)
                {
                    return Json(new { success = false, message = $"An error occurred while registering the doctor: {ex.Message}" });
                }

                TempData["ErrorMessage"] = $"An error occurred while registering the doctor: {ex.Message}";
                return RedirectToAction("Index");
            }
        }



        // GET: View prescriptions with status filter
        public async Task<IActionResult> PendingScripts(PrescriptionStatus? status = null, int page = 1, int pageSize = 10)
        {
            var query = _context.UnprocessedScripts
                .Include(u => u.Customer)
                .Include(u => u.ProcessedBy)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(u => u.Status == status.Value);
            }
            else
            {
                query = query.Where(u => u.Status == PrescriptionStatus.Pending ||
                                       u.Status == PrescriptionStatus.Processing);
            }

            var scripts = await query
                .OrderByDescending(u => u.UploadDate)
                .ToListAsync();

            // Also surface imported prescriptions (which typically have no UploadId/UnprocessedScript) under Completed view
            if (status.HasValue && status.Value == PrescriptionStatus.Completed)
            {
                var importedPrescriptions = await _context.Prescriptions
                    .Include(p => p.Customer)
                    .Include(p => p.Doctor)
                    .Include(p => p.PrescriptionLines)
                    .Where(p => p.UploadId == null) // likely imported or created outside upload flow
                    .ToListAsync();

                foreach (var p in importedPrescriptions)
                {
                    // Skip if an UnprocessedScript already exists for this prescription
                    var hasScript = await _context.UnprocessedScripts.AnyAsync(u => u.Prescription != null && u.Prescription.PrescriptionId == p.PrescriptionId);
                    if (hasScript)
                        continue;

                    scripts.Add(new UnprocessedScript
                    {
                        // Use PrescriptionId as a display/reference id; ScriptDetails handles this case
                        UnploadId = p.PrescriptionId,
                        CustomerId = p.CustomerId,
                        Customer = p.Customer,
                        DoctorId = p.DoctorId,
                        Doctor = p.Doctor,
                        UploadDate = DateOnly.FromDateTime(p.PrescriptionDate),
                        Status = PrescriptionStatus.Completed,
                        ProcessedDate = p.PrescriptionDate,
                        Prescription = p,
                        ProcessedById = null,
                        ProcessedBy = null,
                        RequestDispensation = false
                    });
                }

                // Keep ordering consistent
                scripts = scripts
                    .OrderByDescending(u => u.UploadDate)
                    .ToList();
            }

            // Ensure latest completed stays at the very top: sort by completion date (fallback to upload date)
            // This applies both when viewing only Completed and when mixed
            Func<UnprocessedScript, DateTime> sortKey = u =>
                (u.Status == PrescriptionStatus.Completed
                    ? (u.ProcessedDate ?? u.UploadDate.ToDateTime(TimeOnly.MinValue))
                    : u.UploadDate.ToDateTime(TimeOnly.MinValue));

            var totalItems = scripts.Count;
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            page = Math.Max(1, Math.Min(page, totalPages == 0 ? 1 : totalPages));

            var pagedScripts = scripts
                .OrderByDescending(sortKey)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.StatusFilter = status;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;

            return View(pagedScripts);
        }

        // POST: Update prescription status
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, PrescriptionStatus status, string? notes)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Invalid data provided for updating prescription status.";
                return RedirectToAction(nameof(PendingScripts));
            }
            try
            {
                var script = await _context.UnprocessedScripts.FindAsync(id);
                if (script == null)
                {
                    return NotFound();
                }

                script.Status = status;
                script.ProcessedDate = DateTime.UtcNow;

                switch (status)
                {
                    case PrescriptionStatus.Rejected:
                        script.RejectionReason = notes;
                        break;
                    case PrescriptionStatus.Processing:
                        script.ProcessingNotes = notes;
                        break;
                    case PrescriptionStatus.Completed:
                        // Additional processing for completed prescriptions
                        break;
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Prescription status updated to {status}";
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, $"Error completing prescription {id}");
                return Json(new { success = false, message = "Error completing prescription: " + ex.Message });
            }

            return RedirectToAction(nameof(PendingScripts));
        }

        // GET: Download prescription file
        public IActionResult DownloadPrescription(int id)
        {
            var prescription = _context.UnprocessedScripts
                .FirstOrDefault(p => p.UnploadId == id);

            if (prescription == null)
            {
                TempData["ErrorMessage"] = "Prescription not found.";
                return RedirectToAction(nameof(PendingScripts));
            }

            // Serve file from database
            if (prescription.FileContent != null && prescription.FileContent.Length > 0)
            {
                var contentType = prescription.ContentType ?? "application/octet-stream";
                var fileName = prescription.FileName ?? $"prescription_{id}.pdf";
                
                return File(prescription.FileContent, contentType, fileName);
            }

            // Fallback to file system for backward compatibility
            if (!string.IsNullOrEmpty(prescription.ScriptImagePath))
            {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(),
                                      "wwwroot",
                                      prescription.ScriptImagePath.TrimStart('/'));

                if (System.IO.File.Exists(filePath))
                {
                    var contentType = GetContentType(filePath);
                    var fileName = Path.GetFileName(filePath);
                    return PhysicalFile(filePath, contentType, fileName);
                }
            }

            TempData["ErrorMessage"] = "The prescription file could not be found.";
            return RedirectToAction(nameof(PendingScripts));
        }

        // GET: Serve prescription file for display
        public IActionResult ServePrescriptionFile(int id)
        {
            var prescription = _context.UnprocessedScripts
                .FirstOrDefault(p => p.UnploadId == id);

            if (prescription == null)
            {
                return NotFound();
            }

            // Serve file from database
            if (prescription.FileContent != null && prescription.FileContent.Length > 0)
            {
                var contentType = prescription.ContentType ?? "application/octet-stream";
                return File(prescription.FileContent, contentType);
            }

            // Fallback to file system for backward compatibility
            if (!string.IsNullOrEmpty(prescription.ScriptImagePath))
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(),
                                          "wwwroot",
                                          prescription.ScriptImagePath.TrimStart('/'));

                if (System.IO.File.Exists(filePath))
                {
                    var contentType = GetContentType(filePath);
                    return PhysicalFile(filePath, contentType);
                }
            }

            return NotFound();
        }

        private string GetContentType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".heic" => "image/heic",
                _ => "application/octet-stream"
            };
        }


        // GET: Prescriptions/ScriptDetails/5 (by Upload/UnploadId or PrescriptionId)
        public async Task<IActionResult> ScriptDetails(int id)
        {
            // First try to find an UnprocessedScript with this ID
            var script = await _context.UnprocessedScripts
                .Include(u => u.Customer)
                    .ThenInclude(c => c.Allergies)
                        .ThenInclude(ca => ca.ActiveIngredient)
                .Include(u => u.ProcessedBy)
                .Include(u => u.Doctor)
                .FirstOrDefaultAsync(u => u.UnploadId == id);

            if (script != null)
            {
                // This is a file-uploaded prescription, load its prescription with proper includes
                var prescription = await _context.Prescriptions
                    .Include(p => p.PrescriptionLines)
                        .ThenInclude(pl => pl.Medication)
                    .Include(p => p.Doctor)
                    .FirstOrDefaultAsync(p => p.UploadId == id);

                script.Prescription = prescription;
                
                // Debug logging
                _logger.LogInformation($"Found UnprocessedScript {id}, Prescription: {prescription?.PrescriptionId}, Lines: {prescription?.PrescriptionLines?.Count ?? 0}");
                
                // Additional debug: Check if prescription lines exist in database
                if (prescription != null)
                {
                    var linesCount = await _context.PrescriptionLines.CountAsync(pl => pl.PrescriptionId == prescription.PrescriptionId);
                    var linesCountIgnoringSoftDelete = await _context.PrescriptionLines.IgnoreQueryFilters().CountAsync(pl => pl.PrescriptionId == prescription.PrescriptionId);
                    _logger.LogInformation($"Direct query for PrescriptionLines count: {linesCount}, Ignoring soft delete: {linesCountIgnoringSoftDelete}");
                    
                    // If prescription lines exist but weren't loaded, try to reload them
                    if (linesCount > 0 && (prescription.PrescriptionLines == null || prescription.PrescriptionLines.Count == 0))
                    {
                        _logger.LogInformation("Reloading prescription lines...");
                        await _context.Entry(prescription).Collection(p => p.PrescriptionLines).LoadAsync();
                        foreach (var line in prescription.PrescriptionLines)
                        {
                            await _context.Entry(line).Reference(pl => pl.Medication).LoadAsync();
                        }
                        _logger.LogInformation($"After reload, Lines: {prescription.PrescriptionLines?.Count ?? 0}");
                    }
                    
                    // Add debug info to ViewBag for display
                    ViewBag.DebugInfo = $"PrescriptionId: {prescription.PrescriptionId}, UploadId: {prescription.UploadId}, LinesInDB: {linesCount}, LinesIgnoringSoftDelete: {linesCountIgnoringSoftDelete}";
                }
                
                return View(script);
            }

            // If no UnprocessedScript found, try to find a direct Prescription
            var directPrescription = await _context.Prescriptions
                .Include(p => p.Customer)
                    .ThenInclude(c => c.Allergies)
                        .ThenInclude(ca => ca.ActiveIngredient)
                .Include(p => p.PrescriptionLines)
                    .ThenInclude(pl => pl.Medication)
                .Include(p => p.Doctor)
                .FirstOrDefaultAsync(p => p.PrescriptionId == id);

            if (directPrescription != null)
            {
                // Debug logging
                _logger.LogInformation($"Found direct Prescription {id}, Lines: {directPrescription.PrescriptionLines?.Count ?? 0}");
                
                // Additional debug: Check if prescription lines exist in database
                var linesCount = await _context.PrescriptionLines.CountAsync(pl => pl.PrescriptionId == directPrescription.PrescriptionId);
                var linesCountIgnoringSoftDelete = await _context.PrescriptionLines.IgnoreQueryFilters().CountAsync(pl => pl.PrescriptionId == directPrescription.PrescriptionId);
                _logger.LogInformation($"Direct query for PrescriptionLines count: {linesCount}, Ignoring soft delete: {linesCountIgnoringSoftDelete}");
                
                // If prescription lines exist but weren't loaded, try to reload them
                if (linesCount > 0 && (directPrescription.PrescriptionLines == null || directPrescription.PrescriptionLines.Count == 0))
                {
                    _logger.LogInformation("Reloading prescription lines for direct prescription...");
                    await _context.Entry(directPrescription).Collection(p => p.PrescriptionLines).LoadAsync();
                    foreach (var line in directPrescription.PrescriptionLines)
                    {
                        await _context.Entry(line).Reference(pl => pl.Medication).LoadAsync();
                    }
                    _logger.LogInformation($"After reload, Lines: {directPrescription.PrescriptionLines?.Count ?? 0}");
                }
                
                // Add debug info to ViewBag for display
                ViewBag.DebugInfo = $"PrescriptionId: {directPrescription.PrescriptionId}, UploadId: {directPrescription.UploadId}, LinesInDB: {linesCount}, LinesIgnoringSoftDelete: {linesCountIgnoringSoftDelete}";
                
                // Create a mock UnprocessedScript for the view
                var mockScript = new UnprocessedScript
                {
                    UnploadId = directPrescription.PrescriptionId, // Use PrescriptionId as UnploadId for display
                    CustomerId = directPrescription.CustomerId,
                    Customer = directPrescription.Customer,
                    DoctorId = directPrescription.DoctorId,
                    Doctor = directPrescription.Doctor,
                    UploadDate = DateOnly.FromDateTime(directPrescription.PrescriptionDate),
                    Status = UnprocessedScript.PrescriptionStatus.Completed,
                    Prescription = directPrescription
                };

                return View(mockScript);
            }

            _logger.LogWarning($"No UnprocessedScript or Prescription found with ID {id}");
            return NotFound();
        }


        // GET: Prescriptions/Process/5
        public async Task<IActionResult> Process(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var unprocessedScript = await _context.UnprocessedScripts
                .Include(u => u.Customer)
                .Include(u => u.Customer)
                    .ThenInclude(c => c.Allergies)
                        .ThenInclude(ca => ca.ActiveIngredient)
                .Include(u => u.ProcessedBy)
                .Include(u => u.Prescription)
                    .ThenInclude(p => p.PrescriptionLines)
                        .ThenInclude(pl => pl.Medication)
                .Include(u => u.Prescription)
                    .ThenInclude(p => p.Doctor) // Add this line to include Doctor
                .FirstOrDefaultAsync(m => m.UnploadId == id);

            if (unprocessedScript == null)
            {
                // If no UnprocessedScript exists, try to load a direct Prescription (e.g., imported)
                var directPrescription = await _context.Prescriptions
                    .Include(p => p.Customer)
                        .ThenInclude(c => c.Allergies)
                            .ThenInclude(ca => ca.ActiveIngredient)
                    .Include(p => p.PrescriptionLines)
                        .ThenInclude(pl => pl.Medication)
                    .Include(p => p.Doctor)
                    .FirstOrDefaultAsync(p => p.PrescriptionId == id.Value);

                if (directPrescription == null)
                {
                    return NotFound();
                }

                // Create a corresponding UnprocessedScript so the Process flow works uniformly
                var createdScript = new UnprocessedScript
                {
                    CustomerId = directPrescription.CustomerId,
                    DoctorId = directPrescription.DoctorId,
                    UploadDate = DateOnly.FromDateTime(directPrescription.PrescriptionDate),
                    Status = PrescriptionStatus.Processing, // allow submit to mark Completed
                    Prescription = directPrescription
                };

                _context.UnprocessedScripts.Add(createdScript);
                await _context.SaveChangesAsync();

                // Link back from Prescription for future lookups
                directPrescription.UploadId = createdScript.UnploadId;
                await _context.SaveChangesAsync();

                // Redirect to the canonical Process route with the new UnprocessedScript id
                return RedirectToAction(nameof(Process), new { id = createdScript.UnploadId });
            }

            // Ensure prescription exists
            if (unprocessedScript.Prescription == null)
            {
                unprocessedScript.Prescription = new Prescription
                {
                    CustomerId = unprocessedScript.CustomerId,
                    PrescriptionDate = DateTime.UtcNow,
                    UploadId = unprocessedScript.UnploadId,

                };
                await _context.SaveChangesAsync();
            }

            ViewBag.Medications = await _context.Medications
                .OrderBy(m => m.Name)
                .ToListAsync();

            // Add doctors to ViewBag
            ViewBag.Doctors = await _context.Doctors
                .OrderBy(d => d.Name)
                .ToListAsync();

            return View(unprocessedScript);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignDoctor(int id, int doctorId)
        {
            try
            {
                _logger.LogInformation($"AssignDoctor called: id={id}, doctorId={doctorId}");

                var unprocessedScript = await _context.UnprocessedScripts
                    .Include(u => u.Prescription)
                    .FirstOrDefaultAsync(u => u.UnploadId == id);

                if (unprocessedScript == null)
                {
                    _logger.LogWarning($"UnprocessedScript {id} not found");
                    return Json(new { success = false, message = "Prescription not found" });
                }

                _logger.LogInformation($"Found UnprocessedScript {id}, current DoctorId: {unprocessedScript.DoctorId}");

                // Create prescription if it doesn't exist
                if (unprocessedScript.Prescription == null)
                {
                    _logger.LogInformation($"Creating new Prescription for UnprocessedScript {id}");
                    unprocessedScript.Prescription = new Prescription
                    {
                        CustomerId = unprocessedScript.CustomerId,
                        PrescriptionDate = DateTime.UtcNow,
                        UploadId = unprocessedScript.UnploadId
                    };
                }

                // Get the doctor
                var doctor = await _context.Doctors.FindAsync(doctorId);
                if (doctor == null && doctorId != 0) // 0 means no doctor selected
                {
                    _logger.LogWarning($"Doctor {doctorId} not found");
                    return Json(new { success = false, message = "Doctor not found" });
                }

                if (doctor != null)
                {
                    _logger.LogInformation($"Found doctor: {doctor.Name} {doctor.Surname} (ID: {doctor.DoctorId})");
                }

                // Assign the doctor to both Prescription and UnprocessedScript
                var newDoctorId = doctorId == 0 ? (int?)null : doctorId;
                unprocessedScript.Prescription.DoctorId = newDoctorId;
                unprocessedScript.DoctorId = newDoctorId;
                
                _logger.LogInformation($"Before SaveChanges - UnprocessedScript {unprocessedScript.UnploadId}: Prescription DoctorId: {unprocessedScript.Prescription.DoctorId}, UnprocessedScript DoctorId: {unprocessedScript.DoctorId}");
                
                await _context.SaveChangesAsync();

                // Verify the save worked
                var verifyScript = await _context.UnprocessedScripts
                    .Include(u => u.Doctor)
                    .FirstOrDefaultAsync(u => u.UnploadId == id);
                
                _logger.LogInformation($"After SaveChanges - UnprocessedScript {id}: DoctorId: {verifyScript?.DoctorId}, Doctor: {verifyScript?.Doctor?.Name} {verifyScript?.Doctor?.Surname}");

                return Json(new
                {
                    success = true,
                    message = "Doctor assigned successfully",
                    doctorName = doctor?.Name + " " + doctor?.Surname,
                    practiceNumber = doctor?.PracticeNumber
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error assigning doctor: {ex.Message}");
                return Json(new { success = false, message = "Error assigning doctor: " + ex.Message });
            }
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompletePrescription(int id, string finalNotes)
        {
            try
            {
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var script = await _context.UnprocessedScripts
                    .Include(u => u.Prescription)
                        .ThenInclude(p => p.PrescriptionLines)
                            .ThenInclude(pl => pl.Medication)
                                .ThenInclude(m => m.ActiveIngredients)
                                    .ThenInclude(mi => mi.ActiveIngredient)
                    .Include(u => u.Customer)
                        .ThenInclude(c => c.Allergies)
                            .ThenInclude(a => a.ActiveIngredient)
                    .Include(u => u.ProcessedBy)
                    .FirstOrDefaultAsync(u => u.UnploadId == id);

                if (script == null)
                    return Json(new { success = false, message = "Prescription not found" });

                if (script.Prescription == null || !script.Prescription.PrescriptionLines.Any())
                    return Json(new { success = false, message = "Cannot complete prescription with no items" });

                if (script.Status == PrescriptionStatus.Completed)
                    return Json(new { success = false, message = "Prescription is already completed" });

                // Check for allergy conflicts before completing prescription
                var allergyCheckResult = await CheckCustomerAllergies(script.CustomerId, script.Prescription.PrescriptionLines);
                if (!allergyCheckResult.IsSafe)
                {
                    return Json(new { 
                        success = false, 
                        hasAllergyConflict = true,
                        message = "Customer has allergies to selected medications",
                        conflictingMedications = allergyCheckResult.ConflictingMedications,
                        safeAlternatives = allergyCheckResult.SafeAlternatives
                    });
                }

                // Update prescription
                script.Status = PrescriptionStatus.Completed;
                script.ProcessedDate = DateTime.UtcNow;
                script.ProcessingNotes = finalNotes;
                script.ProcessedById = currentUserId;
                
                // Ensure doctor information is synchronized from Prescription to UnprocessedScript
                if (script.Prescription?.DoctorId != null)
                {
                    script.DoctorId = script.Prescription.DoctorId;
                    _logger.LogInformation($"Synchronized doctor information: UnprocessedScript {script.UnploadId} now has DoctorId {script.DoctorId}");
                }
                else if (script.Prescription?.DoctorId == null && script.DoctorId != null)
                {
                    // If prescription has no doctor but unprocessed script does, clear it
                    script.DoctorId = null;
                    _logger.LogInformation($"Cleared doctor information: UnprocessedScript {script.UnploadId} doctor cleared");
                }

                await _context.SaveChangesAsync();
                
                // Only create DispensedPrescription records if customer requested dispensation
                if (script.RequestDispensation)
                {
                    // Create DispensedPrescription records for all prescription lines (both one-time and repeat prescriptions)
                    await CreateDispensedPrescriptions(script.Prescription, currentUserId);
                }
                
                // Create prescription repeats for each line that has repeats
                await CreatePrescriptionRepeats(script.Prescription);
                
                await NotifyCustomer(script);

                // Add notification for customer dashboard
                await AddCustomerNotification(script.CustomerId,
                    $"Gr-8 Your prescription #{script.UnploadId} is ready for collection.",
                    "Prescription", script.UnploadId.ToString());

                return Json(new
                {
                    success = true,
                    message = "Prescription completed successfully. Customer has been notified.",
                    status = "Completed",
                    redirectUrl = Url.Action("PendingScripts", "Prescriptions", new { status = "Completed" })
                });
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, $"Error completing prescription {id}");
                return Json(new { success = false, message = "Error completing prescription: " + ex.Message });
            }
        }

        private async Task NotifyCustomer(UnprocessedScript script)
        {
            try
            {
                var medications = new StringBuilder();
                decimal totalAmount = 0;

                foreach (var item in script.Prescription.PrescriptionLines)
                {
                    var medicationPrice = item.Medication?.Price ?? 0;
                    var lineTotal = medicationPrice * item.Quantity;
                    totalAmount += (decimal)lineTotal;

                    medications.AppendLine($"• {item.Medication?.Name} {item.Medication?.ActiveIngredients?.FirstOrDefault()?.Strength ?? ""}")
                              .AppendLine($"  Quantity: {item.Quantity}")
                              .AppendLine($"  Instructions: {item.Instructions} {GetFrequencyDisplay(item.Frequency)}")
                              .AppendLine($"  Price: R{medicationPrice:F2} each")
                              .AppendLine($"  Line Total: R{lineTotal:F2}")
                              .AppendLine();
                }

                if (!string.IsNullOrEmpty(script.ProcessingNotes))
                {
                    medications.AppendLine($"Notes: {script.ProcessingNotes}");
                }

                // Check if the prescription was requested to be dispensed
                var wasDispensationRequested = script.RequestDispensation;

                // Send email notification
                var emailService = HttpContext.RequestServices.GetRequiredService<Services.IEmailService>();
                
                if (wasDispensationRequested)
                {
                    // Prescription was requested to be dispensed - send "Ready for Collection" email
                    await emailService.SendPrescriptionReadyNotificationAsync(
                        script.Customer.Email,
                        script.Customer.FullName,
                        script.UnploadId,
                        medications.ToString(),
                        totalAmount
                    );
                    _logger.LogInformation($"Prescription ready for collection notification sent to {script.Customer.Email} for prescription #{script.UnploadId}");
                }
                else
                {
                    // Prescription was only processed for review - send "Ready" email
                    await emailService.SendPrescriptionReadyForReviewAsync(
                        script.Customer.Email,
                        script.Customer.FullName,
                        script.UnploadId,
                        medications.ToString()
                    );
                    _logger.LogInformation($"Prescription ready (for review) notification sent to {script.Customer.Email} for prescription #{script.UnploadId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending prescription ready notification");
            }
        }

        private async Task CreatePrescriptionRepeats(Prescription prescription)
        {
            try
            {
                _logger.LogInformation($"Creating prescription repeats for prescription {prescription.PrescriptionId}");
                _logger.LogInformation($"Prescription has {prescription.PrescriptionLines?.Count ?? 0} lines");
                
                foreach (var line in prescription.PrescriptionLines)
                {
                    _logger.LogInformation($"Processing line {line.PrescriptionLineId}: TotalRepeats = {line.TotalRepeats}");
                    
                    // Only create repeats if the prescription line has repeats allowed
                    if (line.TotalRepeats > 0)
                    {
                        // Check if a repeat already exists for this line
                        var existingRepeat = await _context.PrescriptionRepeats
                            .FirstOrDefaultAsync(pr => pr.PrescriptionLineId == line.PrescriptionLineId);

                        if (existingRepeat == null)
                        {
                            var prescriptionRepeat = new PrescriptionRepeat
                            {
                                PrescriptionLineId = line.PrescriptionLineId,
                                CustomerId = prescription.CustomerId,
                                TotalRepeats = line.TotalRepeats,
                                RemainingRepeats = line.TotalRepeats,
                                QuantityPerRepeat = line.Quantity,
                                DispensedCount = 0,
                                DateCreated = DateTime.UtcNow,
                                CreatedDate = DateTime.UtcNow,
                                IsActive = true
                            };

                            _context.PrescriptionRepeats.Add(prescriptionRepeat);
                            _logger.LogInformation($"Added PrescriptionRepeat for line {line.PrescriptionLineId} with {line.TotalRepeats} repeats");
                        }
                        else
                        {
                            _logger.LogInformation($"PrescriptionRepeat already exists for line {line.PrescriptionLineId}");
                        }
                    }
                    else
                    {
                        _logger.LogInformation($"Skipping line {line.PrescriptionLineId} - no repeats (TotalRepeats = {line.TotalRepeats})");
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Successfully created prescription repeats for prescription {prescription.PrescriptionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating prescription repeats for prescription {prescription.PrescriptionId}");
            }
        }

        private string GetFrequencyDisplay(DosageFrequency frequency)
        {
            var fieldInfo = frequency.GetType().GetField(frequency.ToString());
            var displayAttribute = fieldInfo?.GetCustomAttributes(typeof(DisplayAttribute), false)
                                 .FirstOrDefault() as DisplayAttribute;

            return displayAttribute?.Name ?? frequency.ToString();
        }

        private async Task AddCustomerNotification(int customerId, string message, string relatedEntityType, string relatedEntityId)
        {
            var notification = new Notification
            {
                CustomerId = customerId,
                Message = message,
                DateSent = DateTime.UtcNow,
                IsRead = false,
                RelatedEntityType = relatedEntityType,
                RelatedEntityId = relatedEntityId
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        private async Task CreateDispensedPrescriptions(Prescription prescription, string currentUserId)
        {
            // Get the pharmacist who processed the prescription
            var pharmacist = await _context.Pharmacists
                .FirstOrDefaultAsync(p => p.UserId == currentUserId);

            if (pharmacist == null)
            {
                _logger.LogError("Pharmacist record not found for user {UserId}", currentUserId);
                return;
            }

            // Create DispensedPrescription records for all prescription lines
            foreach (var prescriptionLine in prescription.PrescriptionLines)
            {
                var dispensedPrescription = new DispensedPrescription
                {
                    PrescriptionLineId = prescriptionLine.PrescriptionLineId,
                    PharmacistId = pharmacist.PharmacistId,
                    DispensedDate = DateTime.UtcNow,
                    QuantityDispensed = prescriptionLine.Quantity,
                    AmountDue = CalculateAmountDue(prescriptionLine),
                    IsPaid = false,
                    DispensingNotes = "Dispensed via prescription processing",
                    PatientInstructions = prescriptionLine.Instructions ?? ""
                };

                _context.DispensedPrescriptions.Add(dispensedPrescription);
            }

            await _context.SaveChangesAsync();
        }

        private decimal CalculateAmountDue(PrescriptionLine prescriptionLine)
        {
            // Calculate amount based on medication price and quantity
            var medication = prescriptionLine.Medication;
            if (medication != null)
            {
                return (decimal)(medication.Price * prescriptionLine.Quantity);
            }
            return 0;
        }

        private async Task<AllergyCheckResult> CheckCustomerAllergies(int customerId, ICollection<PrescriptionLine> prescriptionLines)
        {
            var result = new AllergyCheckResult { IsSafe = true };

            // Get customer allergies
            var customerAllergies = await _context.CustomerAllergies
                .Include(ca => ca.ActiveIngredient)
                .Where(ca => ca.CustomerId == customerId)
                .ToListAsync();

            if (!customerAllergies.Any())
            {
                return result; // No allergies, safe to proceed
            }

            var allergicIngredientIds = customerAllergies.Select(ca => ca.ActiveIngredientId).ToList();

            // Check each prescription line for allergy conflicts
            foreach (var prescriptionLine in prescriptionLines)
            {
                var medicationIngredients = prescriptionLine.Medication?.ActiveIngredients?.Select(mi => mi.ActiveIngredientId).ToList() ?? new List<int>();
                
                var conflictingIngredients = medicationIngredients.Intersect(allergicIngredientIds).ToList();
                
                if (conflictingIngredients.Any())
                {
                    result.IsSafe = false;
                    result.ConflictingMedications.Add(new ConflictingMedication
                    {
                        PrescriptionLineId = prescriptionLine.PrescriptionLineId,
                        MedicationId = prescriptionLine.MedicationId,
                        MedicationName = prescriptionLine.Medication?.Name ?? "Unknown",
                        ConflictingIngredients = customerAllergies
                            .Where(ca => conflictingIngredients.Contains(ca.ActiveIngredientId))
                            .Select(ca => new ConflictingIngredient
                            {
                                IngredientId = ca.ActiveIngredientId,
                                IngredientName = ca.ActiveIngredient?.Name ?? "Unknown",
                                Severity = ca.Severity,
                                Description = ca.Description
                            }).ToList()
                    });
                }
            }

            // If there are conflicts, find safe alternatives
            if (!result.IsSafe)
            {
                result.SafeAlternatives = await FindSafeAlternativeMedications(allergicIngredientIds);
            }

            return result;
        }

        private async Task<List<SafeMedication>> FindSafeAlternativeMedications(List<int> allergicIngredientIds)
        {
            // Find medications that don't contain any of the allergic ingredients
            var safeMedications = await _context.Medications
                .Include(m => m.ActiveIngredients)
                    .ThenInclude(mi => mi.ActiveIngredient)
                .Include(m => m.DosageForm)
                .Include(m => m.Supplier)
                .Where(m => !m.ActiveIngredients.Any(mi => allergicIngredientIds.Contains(mi.ActiveIngredientId)))
                .Select(m => new SafeMedication
                {
                    MedicationId = m.MedicationId,
                    Name = m.Name,
                    Description = m.Description,
                    Price = m.Price,
                    DosageForm = m.DosageForm != null ? m.DosageForm.Type : "Unknown",
                    Supplier = m.Supplier != null ? m.Supplier.Name : "Unknown",
                    ActiveIngredients = m.ActiveIngredients.Select(mi => new SafeIngredient
                    {
                        IngredientId = mi.ActiveIngredientId,
                        Name = mi.ActiveIngredient.Name,
                        Strength = mi.Strength
                    }).ToList()
                })
                .OrderBy(m => m.Name)
                .Take(20) // Limit to 20 alternatives for performance
                .ToListAsync();

            return safeMedications;
        }

        [HttpPost]
        public async Task<IActionResult> ReplacePrescriptionMedication(int prescriptionLineId, int newMedicationId)
        {
            try
            {
                var prescriptionLine = await _context.PrescriptionLines
                    .Include(pl => pl.Medication)
                    .FirstOrDefaultAsync(pl => pl.PrescriptionLineId == prescriptionLineId);

                if (prescriptionLine == null)
                {
                    return Json(new { success = false, message = "Prescription line not found" });
                }

                var newMedication = await _context.Medications
                    .Include(m => m.DosageForm)
                    .Include(m => m.Supplier)
                    .FirstOrDefaultAsync(m => m.MedicationId == newMedicationId);

                if (newMedication == null)
                {
                    return Json(new { success = false, message = "New medication not found" });
                }

                // Store the old medication name for logging
                var oldMedicationName = prescriptionLine.Medication?.Name ?? "Unknown";

                // Replace the medication
                prescriptionLine.MedicationId = newMedicationId;
                prescriptionLine.Medication = newMedication;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Replaced medication '{OldMedication}' with '{NewMedication}' in prescription line {PrescriptionLineId}", 
                    oldMedicationName, newMedication.Name, prescriptionLineId);

                return Json(new { 
                    success = true, 
                    message = $"Successfully replaced '{oldMedicationName}' with '{newMedication.Name}'",
                    newMedication = new {
                        medicationId = newMedication.MedicationId,
                        name = newMedication.Name,
                        description = newMedication.Description,
                        price = newMedication.Price,
                        dosageForm = newMedication.DosageForm?.Type ?? "Unknown",
                        supplier = newMedication.Supplier?.Name ?? "Unknown"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error replacing prescription medication");
                return Json(new { success = false, message = "Error replacing medication: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SearchSafeMedications(string searchTerm, int customerId)
        {
            try
            {
                // Get customer allergies
                var customerAllergies = await _context.CustomerAllergies
                    .Where(ca => ca.CustomerId == customerId)
                    .Select(ca => ca.ActiveIngredientId)
                    .ToListAsync();

                var query = _context.Medications
                    .Include(m => m.ActiveIngredients)
                        .ThenInclude(mi => mi.ActiveIngredient)
                    .Include(m => m.DosageForm)
                    .Include(m => m.Supplier)
                    .Where(m => !m.ActiveIngredients.Any(mi => customerAllergies.Contains(mi.ActiveIngredientId)));

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    query = query.Where(m => m.Name.Contains(searchTerm) || m.Description.Contains(searchTerm));
                }

                var safeMedications = await query
                    .Select(m => new SafeMedication
                    {
                        MedicationId = m.MedicationId,
                        Name = m.Name,
                        Description = m.Description,
                        Price = m.Price,
                        DosageForm = m.DosageForm != null ? m.DosageForm.Type : "Unknown",
                        Supplier = m.Supplier != null ? m.Supplier.Name : "Unknown",
                        ActiveIngredients = m.ActiveIngredients.Select(mi => new SafeIngredient
                        {
                            IngredientId = mi.ActiveIngredientId,
                            Name = mi.ActiveIngredient.Name,
                            Strength = mi.Strength
                        }).ToList()
                    })
                    .OrderBy(m => m.Name)
                    .Take(10)
                    .ToListAsync();

                return Json(new { success = true, medications = safeMedications });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching safe medications");
                return Json(new { success = false, message = "Error searching medications" });
            }
        }

        // GET: Customer View - Dispensed Prescriptions
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> DispensedPrescriptions()
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

                if (customer == null)
                {
                    return Forbid();
                }

                // Get all dispensed prescriptions for this customer that haven't been paid yet
                var dispensedPrescriptions = await _context.DispensedPrescriptions
                    .Include(dp => dp.PrescriptionLine)
                        .ThenInclude(pl => pl.Prescription)
                            .ThenInclude(p => p.Doctor)
                    .Include(dp => dp.PrescriptionLine)
                        .ThenInclude(pl => pl.Medication)
                    .Include(dp => dp.Pharmacist)
                    .Where(dp => dp.PrescriptionLine.Prescription.CustomerId == customer.CustomerId && 
                                 dp.IsPaid == false)
                    .OrderByDescending(dp => dp.DispensedDate)
                    .ToListAsync();

                return View(dispensedPrescriptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dispensed prescriptions");
                TempData["ErrorMessage"] = "Error loading dispensed prescriptions. Please try again.";
                return RedirectToAction("Customer", "Home");
            }
        }
    }

    // Supporting classes for allergy checking
    public class AllergyCheckResult
    {
        public bool IsSafe { get; set; }
        public List<ConflictingMedication> ConflictingMedications { get; set; } = new List<ConflictingMedication>();
        public List<SafeMedication> SafeAlternatives { get; set; } = new List<SafeMedication>();
    }

    public class ConflictingMedication
    {
        public int PrescriptionLineId { get; set; }
        public int MedicationId { get; set; }
        public string MedicationName { get; set; }
        public List<ConflictingIngredient> ConflictingIngredients { get; set; } = new List<ConflictingIngredient>();
    }

    public class ConflictingIngredient
    {
        public int IngredientId { get; set; }
        public string IngredientName { get; set; }
        public string Severity { get; set; }
        public string Description { get; set; }
    }

    public class SafeMedication
    {
        public int MedicationId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public double Price { get; set; }
        public string DosageForm { get; set; }
        public string Supplier { get; set; }
        public List<SafeIngredient> ActiveIngredients { get; set; } = new List<SafeIngredient>();
    }

    public class SafeIngredient
    {
        public int IngredientId { get; set; }
        public string Name { get; set; }
        public string Strength { get; set; }
    }
}
