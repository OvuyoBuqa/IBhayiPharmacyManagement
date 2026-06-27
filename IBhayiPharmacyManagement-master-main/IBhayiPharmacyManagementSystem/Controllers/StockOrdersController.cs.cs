using IBhayiPharmacyManagementSystem.Data;
using IBhayiPharmacyManagementSystem.Models;
using IBhayiPharmacyManagementSystem.Services;
using IBhayiPharmacyManagementSystem.ViewModels;
using IBhayiPharmacyManagementSystem.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace IBhayiPharmacyManagementSystem.Controllers
{
    [Authorize(Roles = "PharmacyManager")]
    public class StockOrdersController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<StockOrdersController> _logger;

        public StockOrdersController(AppDbContext context, IEmailSender emailSender, ILogger<StockOrdersController> logger)
        {
            _context = context;
            _emailSender = emailSender;
            _logger = logger;
        }

        // GET: StockOrders
        public async Task<IActionResult> Index()
        {
            var orders = await _context.StockOrders
                .Include(s => s.Supplier)
                .Include(s => s.StockOrderItems)
                .ThenInclude(i => i.Medication)
                .ToListAsync();
            _logger.LogInformation("Accessed StockOrders Index page.");
            return View(orders);
        }

        // GET: StockOrders/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.StockOrders
                .Include(s => s.Supplier)
                .Include(s => s.StockOrderItems)
                .ThenInclude(i => i.Medication)
                .FirstOrDefaultAsync(s => s.StockOrderId == id);

            if (order == null)
            {
                _logger.LogWarning("Stock Order with ID {StockOrderId} not found for Details (GET).", id);
                return NotFound();
            }
            _logger.LogInformation("Accessed Stock Order Details (GET) for order (ID: {StockOrderId}).", id);
            return View(order);
        }

        // GET: StockOrders/Create
        public async Task<IActionResult> Create()
        {
            var model = new CreateStockOrderViewModel();

            // Populate suppliers dropdown
            model.Suppliers = await _context.Suppliers
                                            .Select(s => new SelectListItem { Value = s.SupplierId.ToString(), Text = s.Name })
                                            .ToListAsync();

            // Populate Dosage Forms dropdown
            model.DosageForms = await _context.Dosages
                                            .Select(df => new SelectListItem { Value = df.DosageFormId.ToString(), Text = df.Type })
                                            .ToListAsync();

            // Initially, no medications are loaded until a supplier is selected, or we can load all for initial view
            // For now, let's load all to allow the user to see existing medications if they choose not to add a new one.
            model.Medications = await _context.Medications
                                            .Include(m => m.Supplier)
                                            .OrderBy(m => m.Name)
                                            .Select(m => new SelectListItem
                                            {
                                                Value = m.MedicationId.ToString(),
                                                Text = $"{m.Name} (Current Stock: {m.QuantityInStock}, Re-order Level: {m.MinStockLevel}) - Supplier: {m.Supplier.Name}"
                                            })
                                            .ToListAsync();
            _logger.LogInformation("Accessed StockOrders Create (GET) action.");
            return View(model);
        }

        // POST: StockOrders/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateStockOrderViewModel model)
        {
        // Log the incoming model data for debugging
        _logger.LogInformation("Stock Order Create (POST) received with {ItemCount} items", model.StockOrderItems?.Count ?? 0);
        
        if (model.StockOrderItems != null)
        {
            for (int i = 0; i < model.StockOrderItems.Count; i++)
            {
                var item = model.StockOrderItems[i];
                _logger.LogInformation("Item {Index}: MedicationId={MedicationId}, IsNewMedication={IsNewMedication}, Name={Name}, QuantityOrdered={QuantityOrdered}", 
                    i, item.MedicationId, item.IsNewMedication, item.Name, item.QuantityOrdered);
            }
        }
        else
        {
            _logger.LogWarning("StockOrderItems is null - this is the problem!");
        }
        
        // Log the model state for debugging
        _logger.LogInformation("ModelState.IsValid: {IsValid}", ModelState.IsValid);
        if (!ModelState.IsValid)
        {
            foreach (var key in ModelState.Keys)
            {
                var errors = ModelState[key].Errors;
                if (errors.Any())
                {
                    _logger.LogWarning("ModelState Error - Key: {Key}, Errors: {Errors}", key, string.Join(", ", errors.Select(e => e.ErrorMessage)));
                }
            }
        }
            
            if (model.StockOrderItems != null)
            {
                for (int i = 0; i < model.StockOrderItems.Count; i++)
                {
                    var item = model.StockOrderItems[i];
                    _logger.LogInformation("Item {Index}: MedicationId={MedicationId}, IsNewMedication={IsNewMedication}, Name={Name}", 
                        i, item.MedicationId, item.IsNewMedication, item.Name);
                }
            }
            
            if (!ModelState.IsValid)
            {
                // Don't return immediately; we will validate per-item and attempt to process valid items.
                _logger.LogWarning("Stock Order Create (POST) received with invalid ModelState; will validate items individually.");
            }

            StockOrder stockOrder = null; // Declare stockOrder outside try block
            try
            {
                stockOrder = new StockOrder
                {
                    SupplierId = model.SupplierId,
                    StockOrderDate = DateTime.UtcNow,
                    StockOrderStatus = Enums.StockOrderStatusEnum.Pending,
                    StockOrderItems = new List<StockOrderItem>()
                };

                if (model.StockOrderItems != null)
                {
                    foreach (var item in model.StockOrderItems)
                    {
                        Medication medication;

                        if (item.IsNewMedication) // Handle new medication creation
                        {
                            _logger.LogInformation("Processing new medication: {Name}, MedicationId: {MedicationId}, Schedule: {Schedule}, MinStockLevel: {MinStockLevel}, QuantityInStock: {QuantityInStock}, DosageFormId: {DosageFormId}", 
                                item.Name, item.MedicationId, item.Schedule, item.MinStockLevel, item.QuantityInStock, item.DosageFormId);
                            
                            // Use TryValidateModel to apply validation attributes defined in CreateStockOrderItemViewModel
                            if (!TryValidateModel(item, nameof(model.StockOrderItems)))
                            {
                                // If validation fails for a new medication item, collect errors without modifying ModelState during iteration
                                var validationErrors = ModelState.Where(s => s.Key.StartsWith(nameof(model.StockOrderItems))).ToList();
                                var errorsToAdd = new List<(string key, string message)>();
                                
                                foreach (var state in validationErrors)
                                {
                                    foreach (var error in state.Value.Errors)
                                    {
                                        errorsToAdd.Add((state.Key, error.ErrorMessage));
                                        _logger.LogWarning("Validation error for {Key}: {Message}", state.Key, error.ErrorMessage);
                                    }
                                }
                                
                                // Add errors after iteration is complete
                                foreach (var (key, message) in errorsToAdd)
                                {
                                    ModelState.AddModelError(key, message);
                                }
                                
                                _logger.LogWarning("Validation failed for new medication item: {ItemName}", item.Name);
                                continue; // Skip this invalid item
                            }

                            medication = new Medication
                            {
                                Name = item.Name,
                                Description = item.Description,
                                Schedule = item.Schedule,
                                MinStockLevel = item.MinStockLevel,
                                QuantityInStock = item.QuantityInStock, // Initial quantity when created
                                DosageFormId = item.DosageFormId,
                                SupplierId = model.SupplierId, // Assign to the selected supplier for the order
                                IsNewMedication = true
                            };
                            _context.Medications.Add(medication);
                            await _context.SaveChangesAsync(); // Save to get the new MedicationId

                            // Add active ingredients for the new medication
                            foreach (var activeIngredientVm in item.ActiveIngredients)
                            {
                                if (activeIngredientVm.ActiveIngredientId > 0 && !string.IsNullOrEmpty(activeIngredientVm.Strength))
                                {
                                    var medicationIngredient = new MedicationIngredient
                                    {
                                        MedicationId = medication.MedicationId,
                                        ActiveIngredientId = activeIngredientVm.ActiveIngredientId,
                                        Strength = activeIngredientVm.Strength
                                    };
                                    _context.MedicationIngredients.Add(medicationIngredient);
                                }
                                else
                                {
                                    // Add a model error for invalid active ingredient if needed
                                    ModelState.AddModelError("", $"Invalid active ingredient details for new medication '{item.Name}'.");
                                }
                            }
                            // Save changes for medication ingredients
                            await _context.SaveChangesAsync();

                            _logger.LogInformation("New medication '{MedicationName}' created with ID {MedicationId}.", medication.Name, medication.MedicationId);
                        }
                        else // Handle existing medication
                        {
                            if (item.MedicationId == 0 || item.MedicationId == null)
                            {
                                ModelState.AddModelError("", "Medication is required for existing stock order items.");
                                _logger.LogWarning("Stock order item has MedicationId = {MedicationId} for existing medication", item.MedicationId);
                                continue;
                            }
                            medication = await _context.Medications.FindAsync(item.MedicationId);
                            if (medication == null)
                            {
                                ModelState.AddModelError("", $"Medication with ID '{item.MedicationId}' not found.");
                                _logger.LogWarning("Medication with ID {MedicationId} not found", item.MedicationId);
                                continue; // Skip this item or handle error appropriately
                            }
                        }

                        stockOrder.StockOrderItems.Add(new StockOrderItem
                        {
                            MedicationId = medication.MedicationId,
                            QuantityOrdered = item.QuantityOrdered,
                            Notes = item.Notes
                        });
                    }
                }

                // Add a specific error if no valid stock order items are submitted after processing
                if (stockOrder.StockOrderItems == null || !stockOrder.StockOrderItems.Any())
                {
                    ModelState.AddModelError("", "Please add at least one valid medication to the stock order.");
                    TempData["ErrorMessage"] = "Failed to create stock order. Please add at least one valid medication.";
                    _logger.LogWarning("Stock Order Create (POST) failed because no valid items were added.");
                    // Re-populate dropdowns before returning the view
                    model.Suppliers = await _context.Suppliers.Select(s => new SelectListItem { Value = s.SupplierId.ToString(), Text = s.Name }).ToListAsync();
                    model.DosageForms = await _context.Dosages.Select(df => new SelectListItem { Value = df.DosageFormId.ToString(), Text = df.Type }).ToListAsync();
                    model.Medications = await _context.Medications.Include(m => m.Supplier).OrderBy(m => m.Name).Select(m => new SelectListItem
                    {
                        Value = m.MedicationId.ToString(),
                        Text = $"{m.Name} (Current Stock: {m.QuantityInStock}, Re-order Level: {m.MinStockLevel}) - Supplier: {m.Supplier.Name}"
                    }).ToListAsync();
                    return View(model);
                }

               

                _context.StockOrders.Add(stockOrder);
                await _context.SaveChangesAsync();

              

                // Send email to supplier
                var supplier = await _context.Suppliers.FindAsync(stockOrder.SupplierId);
                if (supplier != null && !string.IsNullOrEmpty(supplier.Email))
                {
                    var subject = $"GRP-04-08 - New Stock Order #{stockOrder.StockOrderId} from IBhayi Pharmacy";
                    var body = new System.Text.StringBuilder();
                    body.AppendLine($"Dear {supplier.ContactPerson ?? supplier.Name},");
                    body.AppendLine($"\nWe have placed a new stock order with you. Here are the details for Order #{stockOrder.StockOrderId}:\n");
                    body.AppendLine($"Order Date: {stockOrder.StockOrderDate:yyyy-MM-dd HH:mm}\n");
                    body.AppendLine("Medication Details:\n");

                    foreach (var item in stockOrder.StockOrderItems)
                    {
                        var medication = await _context.Medications.FindAsync(item.MedicationId);
                        if (medication != null)
                        {
                            // Re-fetch medication with active ingredients for correct strength display in email
                            medication = await _context.Medications
                                                .Include(m => m.ActiveIngredients)
                                                .ThenInclude(mi => mi.ActiveIngredient)
                                                .FirstOrDefaultAsync(m => m.MedicationId == item.MedicationId);

                            body.AppendLine($"- {medication.Name} ({(medication.ActiveIngredients != null && medication.ActiveIngredients.Any() ? $"{medication.ActiveIngredients.First().ActiveIngredient.Name} {medication.ActiveIngredients.First().Strength}" : "No Strength")}): {item.QuantityOrdered} units");
                        }
                    }
                    body.AppendLine($"\nPlease provide a quote for the above items.");
                    body.AppendLine("\nThank you,\nIBhayi Pharmacy Management System");

                    await _emailSender.SendEmailAsync(supplier.Email, subject, body.ToString());
                    _logger.LogInformation("Email sent to supplier {SupplierEmail} for Stock Order ID: {StockOrderId}.", supplier.Email, stockOrder.StockOrderId);
                }
                else
                {
                    _logger.LogWarning("Could not send email for Stock Order ID: {StockOrderId}. Supplier email not found or supplier is null.", stockOrder.StockOrderId);
                }

                TempData["SuccessMessage"] = $"Stock order #{stockOrder.StockOrderId} created successfully! An email has been sent to the supplier.";
                _logger.LogInformation("Stock order {StockOrderId} created successfully.", stockOrder.StockOrderId);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while creating the stock order: " + ex.Message);
                _logger.LogError(ex, "Error creating stock order.");
                // Re-populate dropdowns if validation fails
                model.Suppliers = await _context.Suppliers
                                                .Select(s => new SelectListItem { Value = s.SupplierId.ToString(), Text = s.Name })
                                                .ToListAsync();

                // Re-populate Dosage Forms dropdown
                model.DosageForms = await _context.Dosages
                                                .Select(df => new SelectListItem { Value = df.DosageFormId.ToString(), Text = df.Type })
                                                .ToListAsync();

                // Re-populate medications based on the selected supplier, if any
                if (model.SupplierId > 0)
                {
                    model.Medications = await _context.Medications
                        .Where(m => m.SupplierId == model.SupplierId)
                        .OrderBy(m => m.Name)
                        .Select(m => new SelectListItem
                        {
                            Value = m.MedicationId.ToString(),
                            Text = $"{m.Name} (Current Stock: {m.QuantityInStock}, Re-order Level: {m.MinStockLevel})"
                        })
                        .ToListAsync();
                }
                else
                {
                    // If no supplier selected, provide all medications (or an empty list, depending on desired UX)
                    // For consistency with initial GET, providing all medications (filtered by stock status).
                    var lowStockMedications = await _context.Medications
                        .Include(m => m.Supplier)
                        .Where(m => m.QuantityInStock <= (m.MinStockLevel + 10))
                        .OrderBy(m => m.Name)
                        .ToListAsync();

                    model.Medications = lowStockMedications.Select(m => new SelectListItem
                    {
                        Value = m.MedicationId.ToString(),
                        Text = $"{m.Name} (Current Stock: {m.QuantityInStock}, Re-order Level: {m.MinStockLevel}) - Supplier: {m.Supplier.Name}",
                        Selected = true // Pre-select low stock items
                    }).ToList();

                    var allOtherMedications = await _context.Medications
                        .Include(m => m.Supplier)
                        .Where(m => m.QuantityInStock > (m.MinStockLevel + 10))
                        .OrderBy(m => m.Name)
                        .ToListAsync();

                    model.Medications.AddRange(allOtherMedications.Select(m => new SelectListItem
                    {
                        Value = m.MedicationId.ToString(),
                        Text = $"{m.Name} (Current Stock: {m.QuantityInStock}, Re-order Level: {m.MinStockLevel}) - Supplier: {m.Supplier.Name}",
                        Selected = false
                    }));
                }

                // Add a specific error if no stock order items are submitted.
                if (!model.StockOrderItems.Any())
                {
                    ModelState.AddModelError("", "Please add at least one medication to the stock order.");
                }

                // Log all model state errors for debugging
                var modelStateErrors = ModelState.ToList(); // Create a copy to avoid modification during iteration
                foreach (var state in modelStateErrors)
                {
                    foreach (var error in state.Value.Errors)
                    {
                        _logger.LogError("Validation Error: Field '{FieldName}', Error: '{ErrorMessage}'", state.Key, error.ErrorMessage);
                    }
                }

                TempData["ErrorMessage"] = "An error occurred while creating the stock order: " + ex.Message;
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetMedicationsBySupplier(int supplierId)
        {
            var medications = await _context.Medications
                .Where(m => m.SupplierId == supplierId)
                .Include(m => m.ActiveIngredients)
                    .ThenInclude(ai => ai.ActiveIngredient)
                .Include(m => m.DosageForm)
                .OrderBy(m => m.Name) // Order by Name before projecting to anonymous type
                .Select(m => new
                {
                    medicationId = m.MedicationId,
                    name = m.Name,
                    description = m.Description,
                    schedule = m.Schedule,
                    currentStock = m.QuantityInStock,
                    minStockLevel = m.MinStockLevel,
                    dosageForm = m.DosageForm != null ? m.DosageForm.Type : "N/A",
                    dosageFormId = m.DosageFormId, // Add this field
                    activeIngredients = m.ActiveIngredients.Select(ai => new
                    {
                        name = ai.ActiveIngredient.Name,
                        strength = ai.Strength
                    }).ToList(),
                    isLowStock = m.QuantityInStock <= m.MinStockLevel,
                    // Keep the old format for backward compatibility
                    value = m.MedicationId.ToString(),
                    text = $"{m.Name} (Current Stock: {m.QuantityInStock}, Re-order Level: {m.MinStockLevel})"
                })
                .ToListAsync();

            return Json(medications);
        }


        // GET: StockOrders/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                _logger.LogWarning("Delete (GET) for Stock Order called with null ID.");
                return NotFound();
            }

            var stockOrder = await _context.StockOrders
                .Include(s => s.Supplier)
                .Include(s => s.StockOrderItems)
                .ThenInclude(i => i.Medication)
                .FirstOrDefaultAsync(m => m.StockOrderId == id);
            if (stockOrder == null)
            {
                _logger.LogWarning("Stock Order with ID {StockOrderId} not found for Delete (GET).", id);
                return NotFound();
            }
            _logger.LogInformation("Accessed Stock Order Delete (GET) for order (ID: {StockOrderId}).", id);
            return View(stockOrder);
        }

        // POST: StockOrders/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var stockOrder = await _context.StockOrders
                .Include(o => o.StockOrderItems)
                .FirstOrDefaultAsync(o => o.StockOrderId == id);

            if (stockOrder == null)
            {
                _logger.LogWarning("Attempted to confirm deletion of non-existent stock order (ID: {StockOrderId}).", id);
                return NotFound();
            }

            try
            {
                // Remove associated stock order items first
                _context.StockOrderItems.RemoveRange(stockOrder.StockOrderItems);
                _context.StockOrders.Remove(stockOrder);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Stock order deleted successfully!";
                _logger.LogInformation("Stock order {StockOrderId} deleted successfully.", id);
            }
            catch (DbUpdateException ex)
            {
                TempData["ErrorMessage"] = "A database error occurred while deleting the stock order. It might be referenced by other records.";
                _logger.LogError(ex, "Database error deleting stock order (ID: {StockOrderId}).", id);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An unexpected error occurred while deleting the stock order.";
                _logger.LogError(ex, "Unexpected error deleting stock order (ID: {StockOrderId}).", id);
            }

            return RedirectToAction(nameof(Index));
        }

        private bool StockOrderExists(int id)
        {
            return _context.StockOrders.Any(e => e.StockOrderId == id);
        }

        // POST: StockOrders/Receive/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Receive(int id)
        {
            try
            {
                var stockOrder = await _context.StockOrders
                    .Include(o => o.StockOrderItems)
                    .ThenInclude(i => i.Medication)
                    .FirstOrDefaultAsync(o => o.StockOrderId == id);

                if (stockOrder == null)
                {
                    _logger.LogWarning("Receive (POST) for Stock Order called with non-existent ID {StockOrderId}.", id);
                    return NotFound();
                }

                if (stockOrder.StockOrderStatus == Enums.StockOrderStatusEnum.Received)
                {
                    TempData["ErrorMessage"] = "This order has already been received.";
                    _logger.LogInformation("Attempted to receive already received stock order (ID: {StockOrderId}).", id);
                    return RedirectToAction(nameof(Details), new { id = stockOrder.StockOrderId });
                }

                stockOrder.StockOrderStatus = Enums.StockOrderStatusEnum.Received;
                stockOrder.StockOrderDate = DateTime.UtcNow; // Update received date - should probably be a 'DateReceived' property

                foreach (var item in stockOrder.StockOrderItems)
                {
                    var medication = await _context.Medications.FindAsync(item.MedicationId);
                    if (medication != null)
                    {
                        medication.QuantityInStock += item.QuantityOrdered;
                        _context.Update(medication);

                        // Record stock movement
                        var stockMovement = new StockMovement
                        {
                            MedicationId = medication.MedicationId,
                            MovementType = "Received",
                            QuantityChanged = item.QuantityOrdered,
                            Timestamp = DateTime.UtcNow,
                            Reason = $"Stock received from order #{stockOrder.StockOrderId}",
                            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        };
                        _context.StockMovements.Add(stockMovement);
                    }
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Stock Order #{stockOrder.StockOrderId} marked as received and stock updated.";
                _logger.LogInformation("Stock Order {StockOrderId} marked as received and stock updated by user {UserId}.", id, User.FindFirstValue(ClaimTypes.NameIdentifier));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while receiving the order: " + ex.Message;
                _logger.LogError(ex, "Error receiving stock order (ID: {StockOrderId}).", id);
                // Log the exception
            }

            return RedirectToAction(nameof(Details), new { id = id });
        }

        public async Task<IActionResult> CreateGroupedOrderForSupplier(int supplierId)
        {
            var viewModel = new CreateStockOrderViewModel
            {
                SupplierId = supplierId,
                Suppliers = await _context.Suppliers
                                        .Select(s => new SelectListItem { Value = s.SupplierId.ToString(), Text = s.Name })
                                        .ToListAsync()
            };

            var lowStockMedications = await _context.Medications
                .Include(m => m.Supplier)
                .Where(m => m.SupplierId == supplierId && m.QuantityInStock <= (m.MinStockLevel + 10))
                .ToListAsync();

            foreach (var med in lowStockMedications)
            {
                viewModel.StockOrderItems.Add(new CreateStockOrderItemViewModel
                {
                    MedicationId = med.MedicationId,
                    QuantityOrdered = Math.Max(1, med.MinStockLevel + 5 - med.QuantityInStock), // Suggest to order enough to reach MinStockLevel + 5
                    Notes = $"Suggested reorder for {med.Name}",
                    Name = med.Name, // Pre-fill name for display purposes
                    IsNewMedication = false
                });
            }
            // Populate all medications for dropdown in case user wants to add more
            viewModel.Medications = await _context.Medications
                                            .Include(m => m.Supplier)
                                            .OrderBy(m => m.Name)
                                            .Select(m => new SelectListItem
                                            {
                                                Value = m.MedicationId.ToString(),
                                                Text = $"{m.Name} (Current Stock: {m.QuantityInStock}, Re-order Level: {m.MinStockLevel}) - Supplier: {m.Supplier.Name}"
                                            })
                                            .ToListAsync();

            viewModel.DosageForms = await _context.Dosages
                                            .Select(df => new SelectListItem { Value = df.DosageFormId.ToString(), Text = df.Type })
                                            .ToListAsync();

            _logger.LogInformation("Accessed CreateGroupedOrderForSupplier for supplier ID: {SupplierId}. Found {NumMedications} low stock medications.", supplierId, lowStockMedications.Count);
            return View("Create", viewModel);
        }

        [HttpGet]
        public async Task<JsonResult> GetActiveIngredients()
        {
            var activeIngredients = await _context.ActiveIngredients
                                        .Select(ai => new { id = ai.ActiveIngredientId, name = ai.Name })
                                        .OrderBy(ai => ai.name)
                                        .ToListAsync();
            return Json(activeIngredients);
        }

        // GET: StockOrders/UpdateQuote/5
        public async Task<IActionResult> UpdateQuote(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var stockOrder = await _context.StockOrders
                .Include(s => s.Supplier)
                .Include(s => s.StockOrderItems)
                    .ThenInclude(si => si.Medication)
                .FirstOrDefaultAsync(m => m.StockOrderId == id);

            if (stockOrder == null)
            {
                return NotFound();
            }

            return View(stockOrder);
        }

        // POST: StockOrders/UpdateQuote/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateQuote(int id, decimal quoteAmount, string? quoteNotes)
        {
            var stockOrder = await _context.StockOrders.FindAsync(id);
            if (stockOrder == null)
            {
                return NotFound();
            }

            stockOrder.QuoteAmount = quoteAmount;
            stockOrder.QuoteNotes = quoteNotes;
            stockOrder.QuoteReceivedDate = DateTime.UtcNow;
            stockOrder.StockOrderStatus = StockOrderStatusEnum.QuoteReceived;

            try
            {
                _context.Update(stockOrder);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Quote updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!StockOrderExists(stockOrder.StockOrderId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
        }

        // POST: StockOrders/AcceptQuote/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptQuote(int id)
        {
            var stockOrder = await _context.StockOrders.FindAsync(id);
            if (stockOrder == null)
            {
                return NotFound();
            }

            stockOrder.StockOrderStatus = StockOrderStatusEnum.QuoteAccepted;

            try
            {
                _context.Update(stockOrder);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Quote accepted successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!StockOrderExists(stockOrder.StockOrderId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
        }

        // POST: StockOrders/RejectQuote/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectQuote(int id)
        {
            var stockOrder = await _context.StockOrders.FindAsync(id);
            if (stockOrder == null)
            {
                return NotFound();
            }

            stockOrder.StockOrderStatus = StockOrderStatusEnum.QuoteRejected;

            try
            {
                _context.Update(stockOrder);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Quote rejected successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!StockOrderExists(stockOrder.StockOrderId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
        }

        // AJAX action to add medication to order
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMedicationToOrder(int medicationId, int quantity = 1, string notes = "")
        {
            try
            {
                var medication = await _context.Medications
                    .Include(m => m.ActiveIngredients)
                        .ThenInclude(ai => ai.ActiveIngredient)
                    .Include(m => m.DosageForm)
                    .FirstOrDefaultAsync(m => m.MedicationId == medicationId);

                if (medication == null)
                {
                    return Json(new { success = false, message = "Medication not found" });
                }

                // Calculate suggested quantity based on stock levels
                int suggestedQuantity = quantity;
                if (medication.QuantityInStock <= medication.MinStockLevel)
                {
                    suggestedQuantity = Math.Max(1, medication.MinStockLevel + 10 - medication.QuantityInStock);
                }
                else if (medication.QuantityInStock <= medication.MinStockLevel + 10)
                {
                    suggestedQuantity = Math.Max(1, medication.MinStockLevel + 5 - medication.QuantityInStock);
                }

                var medicationData = new
                {
                    MedicationId = medication.MedicationId,
                    Name = medication.Name,
                    Description = medication.Description,
                    Schedule = medication.Schedule,
                    DosageForm = medication.DosageForm?.Type ?? "N/A",
                    CurrentStock = medication.QuantityInStock,
                    MinStockLevel = medication.MinStockLevel,
                    DosageFormId = medication.DosageFormId,
                    ActiveIngredients = medication.ActiveIngredients?.Select(ai => new
                    {
                        Name = ai.ActiveIngredient.Name,
                        Strength = ai.Strength
                    }).Cast<object>().ToList() ?? new List<object>(),
                    SuggestedQuantity = suggestedQuantity,
                    Notes = notes
                };

                return Json(new { success = true, medication = medicationData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding medication to order");
                return Json(new { success = false, message = "Error adding medication to order" });
            }
        }
    }
}


