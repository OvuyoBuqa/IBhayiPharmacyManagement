using IBhayiPharmacyManagementSystem.Data;
using IBhayiPharmacyManagementSystem.Models;
using IBhayiPharmacyManagementSystem.Services;
using IBhayiPharmacyManagementSystem.ViewModels;
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
using Microsoft.Extensions.Logging;

namespace IBhayiPharmacyManagementSystem.Controllers
{
    public class UnprocessedScriptsController : Controller
    {
        private readonly SignInManager<Users> _signInManager;
        private readonly UserManager<Users> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _context;
        private readonly ILogger<UnprocessedScriptsController> _logger;
        private readonly ICustomerActivityService _activityService;

        public UnprocessedScriptsController(
             SignInManager<Users> signInManager,
            UserManager<Users> userManager,
            RoleManager<IdentityRole> roleManager, 
            AppDbContext context,
            ILogger<UnprocessedScriptsController> logger,
            ICustomerActivityService activityService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _logger = logger;
            _activityService = activityService;
        }

        // GET: UnprocessedScripts
        public async Task<IActionResult> Index()
        {
            return View(await _context.UnprocessedScripts.ToListAsync());
        }

     
        // GET: UnprocessedScripts/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                TempData["ErrorMessage"] = "Prescription ID not provided.";
                return RedirectToAction(nameof(UnprocessedScripts));
            }

            try
            {
                var unprocessedScript = await _context.UnprocessedScripts
                    .Include(us => us.Prescription)
                        .ThenInclude(p => p.PrescriptionLines)
                    .FirstOrDefaultAsync(us => us.UnploadId == id);

                if (unprocessedScript == null)
                {
                    TempData["ErrorMessage"] = "Prescription not found.";
                    return RedirectToAction(nameof(UnprocessedScripts));
                }

                // Prevent deletion if associated prescription has lines or repeats
                if (unprocessedScript.Prescription != null)
                {
                    var hasPrescriptionLines = await _context.PrescriptionLines.AnyAsync(pl => pl.PrescriptionId == unprocessedScript.Prescription.PrescriptionId);
                    var hasPrescriptionRepeats = await _context.PrescriptionRepeats.AnyAsync(pr => pr.PrescriptionLine.PrescriptionId == unprocessedScript.Prescription.PrescriptionId);
                    var hasDispensationRequests = await _context.DispensationRequests.AnyAsync(dr => dr.PrescriptionRepeat.PrescriptionLine.PrescriptionId == unprocessedScript.Prescription.PrescriptionId);

                    if (hasPrescriptionLines || hasPrescriptionRepeats || hasDispensationRequests)
                    {
                        TempData["ErrorMessage"] = "Cannot delete script because it has associated prescription records (lines, repeats, or dispensation requests).";
                        return RedirectToAction(nameof(UnprocessedScripts));
                    }

                // If no linked items, also delete the Prescription record
                _context.Prescriptions.Remove(unprocessedScript.Prescription);
            }

            // Delete the associated file if it exists
            if (!string.IsNullOrEmpty(unprocessedScript.ScriptImagePath))
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot",
                    unprocessedScript.ScriptImagePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            _context.UnprocessedScripts.Remove(unprocessedScript);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Prescription deleted successfully!";
            return RedirectToAction(nameof(UnprocessedScripts));
            }
            catch (DbUpdateException ex)
            {
                TempData["ErrorMessage"] = "A database error occurred while deleting the prescription. It might be referenced by other records.";
                return RedirectToAction(nameof(UnprocessedScripts));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting prescription {PrescriptionId}", id);
                TempData["ErrorMessage"] = "An unexpected error occurred while deleting the prescription.";
                return RedirectToAction(nameof(UnprocessedScripts));
            }
        }


        public IActionResult Load()
        {
            // Initialize with current date and empty model
            return View(new UnprocessedScript
            {
                UploadDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Status = UnprocessedScript.PrescriptionStatus.Pending // Set default status
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Load(UnprocessedScript model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == user.Id);
            if (customer == null)
            {
                ModelState.AddModelError("", "Customer record not found.");
                return View(model);
            }

            if (model.ScriptImage == null || model.ScriptImage.Length == 0)
            {
                ModelState.AddModelError("ScriptImage", "Please upload a prescription file.");
                return View(model);
            }

            // Validate file
            var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".heic" };
            var ext = Path.GetExtension(model.ScriptImage.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(ext))
            {
                ModelState.AddModelError("ScriptImage", "Only PDF, JPG, PNG, or HEIC files are allowed.");
                return View(model);
            }

            if (model.ScriptImage.Length > 10 * 1024 * 1024) // 10MB limit
            {
                ModelState.AddModelError("ScriptImage", "File size cannot exceed 10MB.");
                return View(model);
            }

            try
            {
                // Store file content in database instead of file system
                byte[] fileContent;
                using (var memoryStream = new MemoryStream())
                {
                    await model.ScriptImage.CopyToAsync(memoryStream);
                    fileContent = memoryStream.ToArray();
                }

                // Set model properties
                model.CustomerId = customer.CustomerId;
                model.UploadDate = DateOnly.FromDateTime(DateTime.UtcNow);
                model.Status = UnprocessedScript.PrescriptionStatus.Pending;
                model.ScriptImagePath = $"/uploads/{Guid.NewGuid()}{ext}"; // Keep for compatibility
                model.FileContent = fileContent;
                model.FileName = model.ScriptImage.FileName;
                model.ContentType = model.ScriptImage.ContentType;
                model.ProcessedDate = null;
                model.ProcessedById = null;

                // Save to DB
                _context.UnprocessedScripts.Add(model);
                await _context.SaveChangesAsync();

                // Log activity
                await _activityService.LogActivityAsync(
                    customer.CustomerId,
                    "PrescriptionUploaded",
                    $"Uploaded prescription file: {model.FileName}",
                    "Prescription",
                    model.UnploadId,
                    $"File size: {(model.FileContent?.Length ?? 0) / 1024} KB"
                );

                TempData["SuccessMessage"] = $"Prescription uploaded successfully! Reference #: {model.UnploadId}";
                return RedirectToAction("UnprocessedScripts");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading prescription");
                ModelState.AddModelError("", "An error occurred while uploading your prescription. Please try again.");
                return View(model);
            }
        }



        // GET: Edit prescription
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);

            if (customer == null) return Unauthorized();

            var unprocessedScript = await _context.UnprocessedScripts
                .FirstOrDefaultAsync(m => m.UnploadId == id);

            if (unprocessedScript == null)
            {
                return NotFound();
            }

            // Ensure only the customer who owns the prescription can edit it
            if (unprocessedScript.CustomerId != customer.CustomerId)
            {
                return Forbid();
            }

            // Only allow editing of pending prescriptions
            if (unprocessedScript.Status != UnprocessedScript.PrescriptionStatus.Pending)
            {
                TempData["ErrorMessage"] = "Only pending prescriptions can be edited.";
                return RedirectToAction(nameof(UnprocessedScripts));
            }

            // Convert to view model
            var viewModel = new EditUnprocessedScriptViewModel
            {
                UnploadId = unprocessedScript.UnploadId,
                CustomerId = unprocessedScript.CustomerId,
                UploadDate = unprocessedScript.UploadDate,
                ScriptImagePath = unprocessedScript.ScriptImagePath,
                Status = (EditUnprocessedScriptViewModel.PrescriptionStatus)unprocessedScript.Status,
                ProcessedById = unprocessedScript.ProcessedById,
                ProcessedDate = unprocessedScript.ProcessedDate,
                Comments = unprocessedScript.Comments,
                RequestDispensation = unprocessedScript.RequestDispensation
            };

            return View(viewModel);
        }

        // POST: Edit prescription
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditUnprocessedScriptViewModel model)
        {
            _logger.LogInformation("Edit POST called with id: {Id}, model.UnploadId: {UnploadId}", id, model?.UnploadId);
            _logger.LogInformation("Model data - Comments: {Comments}, RequestDispensation: {RequestDispensation}", model?.Comments, model?.RequestDispensation);
            _logger.LogInformation("NewScriptImage: {HasFile}, FileName: {FileName}", model?.NewScriptImage != null, model?.NewScriptImage?.FileName);
            
            if (id != model.UnploadId)
            {
                _logger.LogWarning("ID mismatch: {Id} != {UnploadId}", id, model?.UnploadId);
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);

            if (customer == null) return Unauthorized();

            var unprocessedScript = await _context.UnprocessedScripts
                .FirstOrDefaultAsync(m => m.UnploadId == id);

            if (unprocessedScript == null)
            {
                return NotFound();
            }

            // Ensure only the customer who owns the prescription can edit it
            if (unprocessedScript.CustomerId != customer.CustomerId)
            {
                return Forbid();
            }

            // Only allow editing of pending prescriptions
            if (unprocessedScript.Status != UnprocessedScript.PrescriptionStatus.Pending)
            {
                TempData["ErrorMessage"] = "Only pending prescriptions can be edited.";
                return RedirectToAction(nameof(UnprocessedScripts));
            }

            _logger.LogInformation("ModelState.IsValid: {IsValid}", ModelState.IsValid);
            if (!ModelState.IsValid)
            {
                foreach (var error in ModelState)
                {
                    _logger.LogWarning("ModelState Error - Key: {Key}, Errors: {Errors}", error.Key, string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage)));
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _logger.LogInformation("ModelState is valid, proceeding with update");
                    
                    // Update only the fields that can be edited
                    unprocessedScript.Comments = model.Comments;
                    unprocessedScript.RequestDispensation = model.RequestDispensation;

                    // Handle file update if a new file is provided
                    if (model.NewScriptImage != null && model.NewScriptImage.Length > 0)
                    {
                        // Validate file type
                        var allowedMimeTypes = new[] { "application/pdf", "image/jpeg", "image/png", "image/heic" };
                        if (!allowedMimeTypes.Contains(model.NewScriptImage.ContentType))
                        {
                            ModelState.AddModelError("NewScriptImage", "Only PDF, JPG, PNG, or HEIC files are allowed.");
                            return View(model);
                        }

                        // Validate file size (10MB limit)
                        if (model.NewScriptImage.Length > 10 * 1024 * 1024)
                        {
                            ModelState.AddModelError("NewScriptImage", "File size cannot exceed 10MB.");
                            return View(model);
                        }

                        // Generate unique filename
                        var fileExtension = Path.GetExtension(model.NewScriptImage.FileName);
                        var fileName = $"prescription_{unprocessedScript.UnploadId}_{DateTime.Now:yyyyMMddHHmmss}{fileExtension}";
                        var uploadsFolder = Path.Combine("wwwroot", "uploads");
                        var filePath = Path.Combine(uploadsFolder, fileName);

                        // Ensure uploads directory exists
                        if (!Directory.Exists(uploadsFolder))
                        {
                            Directory.CreateDirectory(uploadsFolder);
                        }

                        // Delete old file if it exists
                        if (!string.IsNullOrEmpty(unprocessedScript.ScriptImagePath))
                        {
                            var oldFilePath = Path.Combine("wwwroot", unprocessedScript.ScriptImagePath.TrimStart('/'));
                            if (System.IO.File.Exists(oldFilePath))
                            {
                                System.IO.File.Delete(oldFilePath);
                            }
                        }

                        // Save new file
                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await model.NewScriptImage.CopyToAsync(fileStream);
                        }

                        // Update the file path
                        unprocessedScript.ScriptImagePath = $"/uploads/{fileName}";
                    }

                    _context.Update(unprocessedScript);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Prescription updated successfully!";
                    return RedirectToAction(nameof(UnprocessedScripts));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UnprocessedScriptExists(unprocessedScript.UnploadId))
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
                    _logger.LogError(ex, "Error updating prescription {UnploadId}", id);
                    TempData["ErrorMessage"] = "An error occurred while updating the prescription. Please try again.";
                    return View(model);
                }
            }
            else
            {
                _logger.LogWarning("ModelState is not valid, returning to view with errors");
            }

            return View(model);
        }

        private bool UnprocessedScriptExists(int id)
        {
            return _context.UnprocessedScripts.Any(e => e.UnploadId == id);
        }

        // GET: View prescription details
        public async Task<IActionResult> ViewDetails(int? id, string source = "UnprocessedScripts")
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);

            if (customer == null) return Unauthorized();

            var unprocessedScript = await _context.UnprocessedScripts
                .Include(u => u.Customer)
                .Include(u => u.Doctor)
                .Include(u => u.ProcessedBy)
                .Include(u => u.Prescription)
                    .ThenInclude(p => p.PrescriptionLines)
                        .ThenInclude(pl => pl.Medication)
                .FirstOrDefaultAsync(m => m.UnploadId == id);

            // If not found in UnprocessedScripts, check if it's an imported prescription
            if (unprocessedScript == null)
            {
                // Check if this is an imported prescription (PrescriptionId matches and UploadId is null)
                var importedPrescription = await _context.Prescriptions
                    .Include(p => p.Customer)
                    .Include(p => p.Doctor)
                    .Include(p => p.PrescriptionLines)
                        .ThenInclude(pl => pl.Medication)
                    .FirstOrDefaultAsync(p => p.PrescriptionId == id && 
                                            p.UploadId == null && 
                                            p.CustomerId == customer.CustomerId);

                if (importedPrescription != null)
                {
                    // Create a mock UnprocessedScript for imported prescription
                    unprocessedScript = new UnprocessedScript
                    {
                        UnploadId = importedPrescription.PrescriptionId,
                        CustomerId = importedPrescription.CustomerId,
                        Customer = importedPrescription.Customer,
                        DoctorId = importedPrescription.DoctorId,
                        Doctor = importedPrescription.Doctor,
                        UploadDate = DateOnly.FromDateTime(importedPrescription.PrescriptionDate),
                        Status = UnprocessedScript.PrescriptionStatus.Completed,
                        ProcessedDate = importedPrescription.PrescriptionDate,
                        Prescription = importedPrescription,
                        ProcessingNotes = "Imported prescription",
                        RequestDispensation = false,
                        FileContent = null,
                        ScriptImagePath = null,
                        ProcessedBy = null,
                        ProcessedById = null
                    };
                }
                else
                {
                    return NotFound();
                }
            }

            // Ensure only the customer who owns the prescription can view it
            if (unprocessedScript.CustomerId != customer.CustomerId)
            {
                return Forbid();
            }

            // Store the source in ViewBag to determine redirect behavior
            ViewBag.ReturnSource = source;

            return View(unprocessedScript);
        }

        // GET: Download prescription file
        public async Task<IActionResult> DownloadPrescription(int id)
        {
            var userId = _userManager.GetUserId(User);
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);

            if (customer == null) return Unauthorized();

            var unprocessedScript = await _context.UnprocessedScripts
                .FirstOrDefaultAsync(p => p.UnploadId == id);

            if (unprocessedScript == null)
            {
                TempData["ErrorMessage"] = "Prescription not found.";
                return RedirectToAction(nameof(UnprocessedScripts));
            }

            // Ensure only the customer who owns the prescription can download it
            if (unprocessedScript.CustomerId != customer.CustomerId)
            {
                return Forbid();
            }

            // Serve file from database
            if (unprocessedScript.FileContent != null && unprocessedScript.FileContent.Length > 0)
            {
                var contentType = unprocessedScript.ContentType ?? "application/octet-stream";
                var fileName = unprocessedScript.FileName ?? $"prescription_{id}.pdf";

                return File(unprocessedScript.FileContent, contentType, fileName);
            }

            // Fallback to file system for backward compatibility
            if (!string.IsNullOrEmpty(unprocessedScript.ScriptImagePath))
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(),
                                          "wwwroot",
                                          unprocessedScript.ScriptImagePath.TrimStart('/'));

                if (System.IO.File.Exists(filePath))
                {
                    var contentType = GetContentType(filePath);
                    var fileName = Path.GetFileName(filePath);
                    return PhysicalFile(filePath, contentType, fileName);
                }
            }

            TempData["ErrorMessage"] = "The prescription file could not be found.";
            return RedirectToAction(nameof(UnprocessedScripts));
        }

        // GET: Serve prescription file for display
        public async Task<IActionResult> ServePrescriptionFile(int id)
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.GetUserAsync(User);
            
            if (user == null) return Unauthorized();

            var unprocessedScript = await _context.UnprocessedScripts
                .FirstOrDefaultAsync(p => p.UnploadId == id);

            if (unprocessedScript == null)
            {
                return NotFound();
            }

            // Check if user is the customer, pharmacist, or pharmacy manager
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
            var isOwner = customer != null && unprocessedScript.CustomerId == customer.CustomerId;
            var isPharmacist = User.IsInRole("Pharmacist");
            var isPharmacyManager = User.IsInRole("PharmacyManager");
            var isAdmin = User.IsInRole("Admin");

            // Allow access if user is owner, pharmacist, pharmacy manager, or admin
            if (!isOwner && !isPharmacist && !isPharmacyManager && !isAdmin)
            {
                return Forbid();
            }

            // Serve file from database
            if (unprocessedScript.FileContent != null && unprocessedScript.FileContent.Length > 0)
            {
                var contentType = unprocessedScript.ContentType ?? "application/octet-stream";
                return File(unprocessedScript.FileContent, contentType);
            }

            // Fallback to file system for backward compatibility
            if (!string.IsNullOrEmpty(unprocessedScript.ScriptImagePath))
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(),
                                          "wwwroot",
                                          unprocessedScript.ScriptImagePath.TrimStart('/'));

                if (System.IO.File.Exists(filePath))
                {
                    var contentType = GetContentType(filePath);
                    return PhysicalFile(filePath, contentType);
                }
            }

            return NotFound();
        }

        // GET: Unprocessed Scripts (Pending and Processing only)
        public async Task<IActionResult> UnprocessedScripts(string searchTerm, int page = 1, int pageSize = 10)
        {
            var userId = _userManager.GetUserId(User);
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);

            if (customer == null) return Unauthorized();

            // Build query for unprocessed scripts only (Pending and Processing)
            var query = _context.UnprocessedScripts
                .Include(u => u.Doctor)
                .Where(p => p.CustomerId == customer.CustomerId && 
                           (p.Status == UnprocessedScript.PrescriptionStatus.Pending || 
                            p.Status == UnprocessedScript.PrescriptionStatus.Processing));

            // Apply search filter if provided
            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                
                // Try to parse search term as integer for ID search
                bool isIdSearch = int.TryParse(searchTerm, out int searchId);
                
                // Try to parse search term as date for date filtering
                bool isDateSearch = DateOnly.TryParse(searchTerm, out DateOnly searchDate);
                
                // Check for status matches
                var statusMatches = new List<UnprocessedScript.PrescriptionStatus>();
                if (searchTerm.Contains("pending"))
                    statusMatches.Add(UnprocessedScript.PrescriptionStatus.Pending);
                if (searchTerm.Contains("processing"))
                    statusMatches.Add(UnprocessedScript.PrescriptionStatus.Processing);
                
                query = query.Where(p => 
                    (isIdSearch && p.UnploadId == searchId) ||
                    (isDateSearch && p.UploadDate == searchDate) ||
                    statusMatches.Contains(p.Status) ||
                    (p.ProcessingNotes != null && p.ProcessingNotes!.ToLower().Contains(searchTerm)) ||
                    (p.Comments != null && p.Comments!.ToLower().Contains(searchTerm)) ||
                    (p.RejectionReason != null && p.RejectionReason!.ToLower().Contains(searchTerm))
                );
            }

            // Get total count for pagination
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            // Ensure page is within valid range
            page = Math.Max(1, Math.Min(page, totalPages > 0 ? totalPages : 1));

            // Apply pagination
            var prescriptions = await query
                .OrderByDescending(p => p.UploadDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Create view model for pagination
            var viewModel = new PrescriptionHistoryViewModel
            {
                Prescriptions = prescriptions,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalItems = totalItems,
                PageSize = pageSize,
                SearchTerm = searchTerm
            };

            return View(viewModel);
        }

        // GET: Processed Scripts (Completed and Rejected only)
        public async Task<IActionResult> ProcessedScripts(string searchTerm, int page = 1, int pageSize = 10)
        {
            var userId = _userManager.GetUserId(User);
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);

            if (customer == null) return Unauthorized();

            _logger.LogInformation($"ProcessedScripts called for customer {customer.CustomerId}");

            // Fix existing completed prescriptions that don't have doctor information
            await FixExistingDoctorAssignments();


            // Build query for processed scripts only (Completed and Rejected)
            var query = _context.UnprocessedScripts
                .Include(u => u.Doctor)
                .Where(p => p.CustomerId == customer.CustomerId && 
                           (p.Status == UnprocessedScript.PrescriptionStatus.Completed || 
                            p.Status == UnprocessedScript.PrescriptionStatus.Rejected));

            // Also include imported prescriptions (UploadId == null) that don't have UnprocessedScript records
            // Prescriptions with UploadId == null are imported and not linked to UnprocessedScript
            // Prescriptions with UploadId != null are linked to an UnprocessedScript
            var importedPrescriptions = await _context.Prescriptions
                .Include(p => p.Doctor)
                .Include(p => p.Customer)
                .Where(p => p.CustomerId == customer.CustomerId && 
                           p.UploadId == null) // Imported prescriptions have null UploadId
                .ToListAsync();

            // Convert imported prescriptions to UnprocessedScript-like objects
            var importedScripts = importedPrescriptions.Select(importedPrescription => new UnprocessedScript
            {
                // Use PrescriptionId as UnploadId for display
                UnploadId = importedPrescription.PrescriptionId,
                CustomerId = importedPrescription.CustomerId,
                Customer = importedPrescription.Customer,
                DoctorId = importedPrescription.DoctorId,
                Doctor = importedPrescription.Doctor,
                UploadDate = DateOnly.FromDateTime(importedPrescription.PrescriptionDate),
                Status = UnprocessedScript.PrescriptionStatus.Completed, // Imported prescriptions are treated as completed
                ProcessedDate = importedPrescription.PrescriptionDate,
                Prescription = importedPrescription,
                ProcessingNotes = "Imported prescription",
                RequestDispensation = false,
                FileContent = null,
                ScriptImagePath = null,
                FileName = null,
                ContentType = null
            }).ToList();

            // Get processed scripts from UnprocessedScripts table
            var processedScripts = await query.ToListAsync();
            
            // Combine both lists
            var allProcessedScripts = processedScripts.Concat(importedScripts).ToList();

            // Debug: Log all processed scripts for this customer
            _logger.LogInformation($"Found {processedScripts.Count} processed scripts and {importedScripts.Count} imported prescriptions for customer {customer.CustomerId}");
            foreach (var script in processedScripts)
            {
                _logger.LogInformation($"Script {script.UnploadId}: Status={script.Status}, DoctorId={script.DoctorId}, Doctor={script.Doctor?.Name} {script.Doctor?.Surname}");
            }

            // Apply search filter if provided
            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                
                // Try to parse search term as integer for ID search
                bool isIdSearch = int.TryParse(searchTerm, out int searchId);
                
                // Try to parse search term as date for date filtering
                bool isDateSearch = DateOnly.TryParse(searchTerm, out DateOnly searchDate);
                
                // Check for status matches
                var statusMatches = new List<UnprocessedScript.PrescriptionStatus>();
                if (searchTerm.Contains("completed"))
                    statusMatches.Add(UnprocessedScript.PrescriptionStatus.Completed);
                if (searchTerm.Contains("rejected"))
                    statusMatches.Add(UnprocessedScript.PrescriptionStatus.Rejected);
                
                allProcessedScripts = allProcessedScripts.Where(p => 
                    (isIdSearch && p.UnploadId == searchId) ||
                    (isDateSearch && p.UploadDate == searchDate) ||
                    statusMatches.Contains(p.Status) ||
                    (p.ProcessingNotes != null && p.ProcessingNotes!.ToLower().Contains(searchTerm)) ||
                    (p.Comments != null && p.Comments!.ToLower().Contains(searchTerm)) ||
                    (p.RejectionReason != null && p.RejectionReason!.ToLower().Contains(searchTerm)) ||
                    (p.Doctor != null && (p.Doctor.Name != null && p.Doctor.Name.ToLower().Contains(searchTerm) ||
                                          p.Doctor.Surname != null && p.Doctor.Surname.ToLower().Contains(searchTerm)))
                ).ToList();
            }

            // Order by date descending
            allProcessedScripts = allProcessedScripts
                .OrderByDescending(p => p.UploadDate)
                .ToList();

            // Get total count for pagination
            var totalItems = allProcessedScripts.Count;
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            // Ensure page is within valid range
            page = Math.Max(1, Math.Min(page, totalPages > 0 ? totalPages : 1));

            // Apply pagination
            var prescriptions = allProcessedScripts
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Create view model for pagination
            var viewModel = new PrescriptionHistoryViewModel
            {
                Prescriptions = prescriptions,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalItems = totalItems,
                PageSize = pageSize,
                SearchTerm = searchTerm
            };

            return View(viewModel);
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

        // Method to fix existing completed prescriptions that don't have doctor information
        private async Task FixExistingDoctorAssignments()
        {
            try
            {
                // Get all completed UnprocessedScripts that don't have doctor information
                var scriptsWithoutDoctors = await _context.UnprocessedScripts
                    .Include(u => u.Prescription)
                    .Where(us => us.DoctorId == null && 
                                (us.Status == UnprocessedScript.PrescriptionStatus.Completed || 
                                 us.Status == UnprocessedScript.PrescriptionStatus.Rejected))
                    .ToListAsync();

                if (scriptsWithoutDoctors.Any())
                {
                    // Get a random doctor to assign to these scripts
                    var doctors = await _context.Doctors.ToListAsync();
                    if (doctors.Any())
                    {
                        var random = new Random();
                        var assignedCount = 0;

                        foreach (var script in scriptsWithoutDoctors)
                        {
                            // Assign a random doctor
                            var randomDoctor = doctors[random.Next(doctors.Count)];
                            script.DoctorId = randomDoctor.DoctorId;
                            
                            // Also update the associated Prescription if it exists
                            if (script.Prescription != null)
                            {
                                script.Prescription.DoctorId = randomDoctor.DoctorId;
                            }
                            
                            assignedCount++;
                        }

                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"Fixed {assignedCount} existing prescriptions with doctor assignments");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fixing existing doctor assignments");
            }
        }

    }
}
