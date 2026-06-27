using IBhayiPharmacyManagementSystem.Data;
using IBhayiPharmacyManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace IBhayiPharmacyManagementSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class DeletedRecordsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly ILogger<DeletedRecordsController> _logger;

        public DeletedRecordsController(AppDbContext context, UserManager<Users> userManager, ILogger<DeletedRecordsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: DeletedRecords
        public async Task<IActionResult> Index(string entityType, string searchTerm, int page = 1, int pageSize = 20)
        {
            entityType = string.IsNullOrEmpty(entityType) ? "All" : entityType; // default to All when null/empty
            ViewBag.EntityType = entityType;
            ViewBag.SearchTerm = searchTerm;
            ViewBag.CurrentPage = page;

            var deletedRecords = await GetDeletedRecords(entityType, searchTerm);

            var totalItems = deletedRecords.Count();
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            page = Math.Max(1, Math.Min(page, totalPages > 0 ? totalPages : 1));

            var paginatedRecords = deletedRecords
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;

            return View(paginatedRecords);
        }

        private async Task<List<DeletedRecordViewModel>> GetDeletedRecords(string entityType, string searchTerm)
        {
            var records = new List<DeletedRecordViewModel>();

            // Get all deleted records based on entity type
            if (entityType == "All" || entityType == "Medications")
            {
                var medications = await GetDeletedMedicationsAsync(searchTerm);
                records.AddRange(medications);
            }

            if (entityType == "All" || entityType == "Suppliers")
            {
                var suppliers = await GetDeletedSuppliersAsync(searchTerm);
                records.AddRange(suppliers);
            }

            if (entityType == "All" || entityType == "Pharmacists")
            {
                var pharmacists = await GetDeletedPharmacistsAsync(searchTerm);
                records.AddRange(pharmacists);
            }

            if (entityType == "All" || entityType == "PharmacyManagers")
            {
                var managers = await GetDeletedPharmacyManagersAsync(searchTerm);
                records.AddRange(managers);
            }

            if (entityType == "All" || entityType == "Pharmacies")
            {
                var pharmacies = await GetDeletedPharmaciesAsync(searchTerm);
                records.AddRange(pharmacies);
            }

            if (entityType == "All" || entityType == "Doctors")
            {
                var doctors = await GetDeletedDoctorsAsync(searchTerm);
                records.AddRange(doctors);
            }

            if (entityType == "All" || entityType == "Customers")
            {
                var customers = await GetDeletedCustomersAsync(searchTerm);
                records.AddRange(customers);
            }

            if (entityType == "All" || entityType == "Orders")
            {
                var orders = await GetDeletedOrdersAsync(searchTerm);
                records.AddRange(orders);
            }

            if (entityType == "All" || entityType == "ActiveIngredients")
            {
                var ingredients = await GetDeletedActiveIngredientsAsync(searchTerm);
                records.AddRange(ingredients);
            }

            if (entityType == "All" || entityType == "DosageForms")
            {
                var dosageForms = await GetDeletedDosageFormsAsync(searchTerm);
                records.AddRange(dosageForms);
            }

            return records.OrderByDescending(r => r.DeletedAt).ToList();
        }

        private async Task<List<DeletedRecordViewModel>> GetDeletedMedicationsAsync(string searchTerm)
        {
            var query = _context.Medications
                .IgnoreQueryFilters()
                .Where(m => EF.Property<bool>(m, "IsDeleted") == true)
                .Include(m => m.Supplier)
                .Include(m => m.DosageForm)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(m => m.Name.Contains(searchTerm));
            }

            return await query
                .Select(m => new DeletedRecordViewModel
                {
                    Id = m.MedicationId,
                    Name = m.Name,
                    EntityType = "Medication",
                    DeletedAt = EF.Property<DateTime?>(m, "DeletedAt") ?? DateTime.UtcNow,
                    DeletedBy = EF.Property<string>(m, "DeletedBy"),
                    Description = $"Schedule: {m.Schedule}, Price: R{m.Price}, Stock: {m.QuantityInStock}"
                })
                .ToListAsync();
        }

        private async Task<List<DeletedRecordViewModel>> GetDeletedSuppliersAsync(string searchTerm)
        {
            var query = _context.Suppliers
                .IgnoreQueryFilters()
                .Where(s => EF.Property<bool>(s, "IsDeleted") == true)
                .AsNoTracking();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(s => s.Name.Contains(searchTerm) || s.Email.Contains(searchTerm));
            }

            return await query.Select(s => new DeletedRecordViewModel
            {
                Id = s.SupplierId,
                Name = s.Name,
                EntityType = "Supplier",
                DeletedAt = EF.Property<DateTime?>(s, "DeletedAt") ?? DateTime.UtcNow,
                DeletedBy = EF.Property<string>(s, "DeletedBy"),
                Description = $"Contact: {s.ContactPerson}, Email: {s.Email}"
            }).ToListAsync();
        }

        private async Task<List<DeletedRecordViewModel>> GetDeletedPharmacistsAsync(string searchTerm)
        {
            var query = _context.Pharmacists
                .IgnoreQueryFilters()
                .Where(p => EF.Property<bool>(p, "IsDeleted") == true)
                .Include(p => p.User)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(p => p.Name.Contains(searchTerm) || p.Email.Contains(searchTerm));
            }

            return await query.Select(p => new DeletedRecordViewModel
            {
                Id = p.PharmacistId,
                Name = $"{p.Name} {p.Surname}",
                EntityType = "Pharmacist",
                DeletedAt = EF.Property<DateTime?>(p, "DeletedAt") ?? DateTime.UtcNow,
                DeletedBy = EF.Property<string>(p, "DeletedBy"),
                Description = $"Email: {p.Email}, Registration: {p.RegistrationNumber}"
            }).ToListAsync();
        }

        private async Task<List<DeletedRecordViewModel>> GetDeletedPharmacyManagersAsync(string searchTerm)
        {
            var query = _context.PharmacyManagers
                .IgnoreQueryFilters()
                .Where(pm => EF.Property<bool>(pm, "IsDeleted") == true)
                .Include(pm => pm.User)
                .AsNoTracking();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(pm => pm.Name.Contains(searchTerm) || pm.Email.Contains(searchTerm));
            }

            return await query.Select(pm => new DeletedRecordViewModel
            {
                Id = pm.PharmacyManagerId,
                Name = $"{pm.Name} {pm.Surname}",
                EntityType = "PharmacyManager",
                DeletedAt = EF.Property<DateTime?>(pm, "DeletedAt") ?? DateTime.UtcNow,
                DeletedBy = EF.Property<string>(pm, "DeletedBy"),
                Description = $"Email: {pm.Email}"
            }).ToListAsync();
        }

        private async Task<List<DeletedRecordViewModel>> GetDeletedPharmaciesAsync(string searchTerm)
        {
            var query = _context.Pharmacies
                .IgnoreQueryFilters()
                .Where(p => EF.Property<bool>(p, "IsDeleted") == true)
                .AsNoTracking();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(p => p.Name.Contains(searchTerm));
            }

            return await query.Select(p => new DeletedRecordViewModel
            {
                Id = p.PharmacyId,
                Name = p.Name,
                EntityType = "Pharmacy",
                DeletedAt = EF.Property<DateTime?>(p, "DeletedAt") ?? DateTime.UtcNow,
                DeletedBy = EF.Property<string>(p, "DeletedBy"),
                Description = $"Email: {p.Email}, Registration: {p.HealthcareCouncilRegistrationNumber}"
            }).ToListAsync();
        }

        private async Task<List<DeletedRecordViewModel>> GetDeletedDoctorsAsync(string searchTerm)
        {
            var query = _context.Doctors
                .IgnoreQueryFilters()
                .Where(d => EF.Property<bool>(d, "IsDeleted") == true)
                .AsNoTracking();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(d => d.Name.Contains(searchTerm) || d.Surname.Contains(searchTerm));
            }

            return await query.Select(d => new DeletedRecordViewModel
            {
                Id = d.DoctorId,
                Name = $"{d.Name} {d.Surname}",
                EntityType = "Doctor",
                DeletedAt = EF.Property<DateTime?>(d, "DeletedAt") ?? DateTime.UtcNow,
                DeletedBy = EF.Property<string>(d, "DeletedBy"),
                Description = $"Practice: {d.PracticeNumber}, Email: {d.Email}"
            }).ToListAsync();
        }

        private async Task<List<DeletedRecordViewModel>> GetDeletedCustomersAsync(string searchTerm)
        {
            var query = _context.Customers
                .IgnoreQueryFilters()
                .Where(c => EF.Property<bool>(c, "IsDeleted") == true)
                .AsNoTracking();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(c => c.Name.Contains(searchTerm) || c.Surname.Contains(searchTerm));
            }

            return await query.Select(c => new DeletedRecordViewModel
            {
                Id = c.CustomerId,
                Name = $"{c.Name} {c.Surname}",
                EntityType = "Customer",
                DeletedAt = EF.Property<DateTime?>(c, "DeletedAt") ?? DateTime.UtcNow,
                DeletedBy = EF.Property<string>(c, "DeletedBy"),
                Description = $"Email: {c.Email}, ID: {c.IDNumber}"
            }).ToListAsync();
        }

        private async Task<List<DeletedRecordViewModel>> GetDeletedOrdersAsync(string searchTerm)
        {
            var query = _context.Orders
                .IgnoreQueryFilters()
                .Where(o => EF.Property<bool>(o, "IsDeleted") == true)
                .Include(o => o.Customer)
                .AsNoTracking();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(o => o.OrderId.ToString().Contains(searchTerm));
            }

            return await query.Select(o => new DeletedRecordViewModel
            {
                Id = o.OrderId,
                Name = $"Order #{o.OrderId}",
                EntityType = "Order",
                DeletedAt = EF.Property<DateTime?>(o, "DeletedAt") ?? DateTime.UtcNow,
                DeletedBy = EF.Property<string>(o, "DeletedBy"),
                Description = $"Customer: {o.Customer!.Name}, Total: R{o.TotalAmount}"
            }).ToListAsync();
        }

        private async Task<List<DeletedRecordViewModel>> GetDeletedActiveIngredientsAsync(string searchTerm)
        {
            var query = _context.ActiveIngredients
                .IgnoreQueryFilters()
                .Where(ai => EF.Property<bool>(ai, "IsDeleted") == true)
                .AsNoTracking();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(ai => ai.Name.Contains(searchTerm));
            }

            return await query.Select(ai => new DeletedRecordViewModel
            {
                Id = ai.ActiveIngredientId,
                Name = ai.Name,
                EntityType = "ActiveIngredient",
                DeletedAt = EF.Property<DateTime?>(ai, "DeletedAt") ?? DateTime.UtcNow,
                DeletedBy = EF.Property<string>(ai, "DeletedBy"),
                Description = $"Strength: {ai.Strength}"
            }).ToListAsync();
        }

        private async Task<List<DeletedRecordViewModel>> GetDeletedDosageFormsAsync(string searchTerm)
        {
            var query = _context.Dosages
                .IgnoreQueryFilters()
                .Where(df => EF.Property<bool>(df, "IsDeleted") == true)
                .AsNoTracking();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(df => df.Type.Contains(searchTerm));
            }

            return await query.Select(df => new DeletedRecordViewModel
            {
                Id = df.DosageFormId,
                Name = df.Type,
                EntityType = "DosageForm",
                DeletedAt = EF.Property<DateTime?>(df, "DeletedAt") ?? DateTime.UtcNow,
                DeletedBy = EF.Property<string>(df, "DeletedBy"),
                Description = df.Description
            }).ToListAsync();
        }

        // POST: DeletedRecords/Restore/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id, string entityType)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var restoredBy = currentUser?.UserName ?? "Admin";

                object? entity = null;

                switch (entityType)
                {
                    case "Medication":
                        entity = await _context.Medications.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.MedicationId == id);
                        break;
                    case "Supplier":
                        entity = await _context.Suppliers.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.SupplierId == id);
                        break;
                    case "Pharmacist":
                        entity = await _context.Pharmacists.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.PharmacistId == id);
                        break;
                    case "PharmacyManager":
                        entity = await _context.PharmacyManagers.IgnoreQueryFilters().FirstOrDefaultAsync(pm => pm.PharmacyManagerId == id);
                        break;
                    case "Pharmacy":
                        entity = await _context.Pharmacies.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.PharmacyId == id);
                        break;
                    case "Doctor":
                        entity = await _context.Doctors.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.DoctorId == id);
                        break;
                    case "Customer":
                        entity = await _context.Customers.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.CustomerId == id);
                        break;
                    case "Order":
                        entity = await _context.Orders.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.OrderId == id);
                        break;
                    case "ActiveIngredient":
                        entity = await _context.ActiveIngredients.IgnoreQueryFilters().FirstOrDefaultAsync(ai => ai.ActiveIngredientId == id);
                        break;
                    case "DosageForm":
                        entity = await _context.Dosages.IgnoreQueryFilters().FirstOrDefaultAsync(df => df.DosageFormId == id);
                        break;
                }

                if (entity == null)
                {
                    TempData["ErrorMessage"] = "Record not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Restore the record
                var entry = _context.Entry(entity);
                entry.Property("IsDeleted").CurrentValue = false;
                entry.Property("DeletedAt").CurrentValue = null;
                entry.Property("DeletedBy").CurrentValue = null;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Record restored: {EntityType} ID {Id} by {User}", entityType, id, restoredBy);
                TempData["SuccessMessage"] = $"{entityType} restored successfully!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring record ID {Id} of type {EntityType}", id, entityType);
                TempData["ErrorMessage"] = "An error occurred while restoring the record.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: DeletedRecords/PermanentlyDelete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PermanentlyDelete(int id, string entityType)
        {
            try
            {
                _context.BypassSoftDelete = true; // ensure hard delete
                object? entity = null;

                switch (entityType)
                {
                    case "Medication":
                        entity = await _context.Medications.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.MedicationId == id);
                        if (entity != null) _context.Medications.Remove((Medication)entity);
                        break;
                    case "Supplier":
                        entity = await _context.Suppliers.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.SupplierId == id);
                        if (entity != null) _context.Suppliers.Remove((Supplier)entity);
                        break;
                    case "Pharmacist":
                        entity = await _context.Pharmacists.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.PharmacistId == id);
                        if (entity != null) _context.Pharmacists.Remove((Pharmacist)entity);
                        break;
                    case "PharmacyManager":
                        entity = await _context.PharmacyManagers.IgnoreQueryFilters().FirstOrDefaultAsync(pm => pm.PharmacyManagerId == id);
                        if (entity != null) _context.PharmacyManagers.Remove((PharmacyManager)entity);
                        break;
                    case "Pharmacy":
                        entity = await _context.Pharmacies.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.PharmacyId == id);
                        if (entity != null) _context.Pharmacies.Remove((Pharmacy)entity);
                        break;
                    case "Doctor":
                        entity = await _context.Doctors.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.DoctorId == id);
                        if (entity != null) _context.Doctors.Remove((Doctor)entity);
                        break;
                    case "Customer":
                        entity = await _context.Customers.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.CustomerId == id);
                        if (entity != null) _context.Customers.Remove((Customer)entity);
                        break;
                    case "Order":
                        entity = await _context.Orders.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.OrderId == id);
                        if (entity != null) _context.Orders.Remove((Order)entity);
                        break;
                    case "ActiveIngredient":
                        entity = await _context.ActiveIngredients.IgnoreQueryFilters().FirstOrDefaultAsync(ai => ai.ActiveIngredientId == id);
                        if (entity != null) _context.ActiveIngredients.Remove((ActiveIngredients)entity);
                        break;
                    case "DosageForm":
                        entity = await _context.Dosages.IgnoreQueryFilters().FirstOrDefaultAsync(df => df.DosageFormId == id);
                        if (entity != null) _context.Dosages.Remove((DosageForm)entity);
                        break;
                }

                if (entity == null)
                {
                    TempData["ErrorMessage"] = "Record not found.";
                    return RedirectToAction(nameof(Index));
                }

                await _context.SaveChangesAsync();

                _logger.LogWarning("Record permanently deleted: {EntityType} ID {Id}", entityType, id);
                TempData["SuccessMessage"] = $"{entityType} permanently deleted.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error permanently deleting record ID {Id} of type {EntityType}", id, entityType);
                TempData["ErrorMessage"] = "An error occurred while deleting the record.";
            }
            finally
            {
                _context.BypassSoftDelete = false;
            }

            return RedirectToAction(nameof(Index));
        }
    }

    public class DeletedRecordViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public DateTime DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}

