using IBhayiPharmacyManagementSystem.Data;
using IBhayiPharmacyManagementSystem.Enums;
using IBhayiPharmacyManagementSystem.Models;
using IBhayiPharmacyManagementSystem.ViewModels;
using IBhayiPharmacyManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace IBhayiPharmacyManagementSystem.Controllers
{
    public class OrdersController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly ILogger<OrdersController> _logger;
        private readonly ICustomerActivityService _activityService;

        public OrdersController(
            AppDbContext context,
            UserManager<Users> userManager,
            ILogger<OrdersController> logger,
            ICustomerActivityService activityService)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _activityService = activityService;
        }

        // GET: Orders
        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

            if (customer == null) return Forbid();

            var orders = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Pharmacist)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Medication)
                .Where(o => o.CustomerId == customer.CustomerId && 
                           o.OrderStatus != OrderStatusEnum.Collected) // Exclude collected orders
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        // GET: Orders/CollectedOrders
        public async Task<IActionResult> CollectedOrders()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

            if (customer == null) return Forbid();

            var collectedOrders = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Pharmacist)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Medication)
                .Where(o => o.CustomerId == customer.CustomerId && 
                           o.OrderStatus == OrderStatusEnum.Collected) // Only collected orders
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(collectedOrders);
        }

        // GET: Orders/OrderHistory
        public async Task<IActionResult> OrderHistory()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

            if (customer == null) return Forbid();

            var allOrders = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Pharmacist)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Medication)
                .Where(o => o.CustomerId == customer.CustomerId) // All orders (including collected, cancelled, etc.)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(allOrders);
        }

        // GET: Orders/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            // Check if user is Admin or Pharmacist - they can view any order
            if (User.IsInRole("Admin") || User.IsInRole("Pharmacist"))
            {
                if (id == null)
                {
                    TempData["ErrorMessage"] = "Order ID is required.";
                    return RedirectToAction(nameof(OrdersForCustomers));
                }

                var order = await _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.Pharmacist)
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Medication)
                    .FirstOrDefaultAsync(m => m.OrderId == id);

                if (order == null)
                {
                    return NotFound();
                }

                return View(order);
            }

            // For customers, check if they have a customer record
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

            if (customer == null) return Forbid();

            // If no ID is provided, get the most recent order for this customer
            if (id == null)
            {
                var mostRecentOrder = await _context.Orders
                    .Where(o => o.CustomerId == customer.CustomerId)
                    .OrderByDescending(o => o.OrderDate)
                    .FirstOrDefaultAsync();

                if (mostRecentOrder == null)
                {
                    TempData["InfoMessage"] = "You haven't placed any orders yet.";
                    return RedirectToAction(nameof(Index));
                }

                // Redirect to the most recent order's details
                return RedirectToAction(nameof(Details), new { id = mostRecentOrder.OrderId });
            }

            var customerOrder = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Pharmacist)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Medication)
                .FirstOrDefaultAsync(m => m.OrderId == id);

            if (customerOrder == null)
            {
                return NotFound();
            }

            // Ensure customer can only view their own orders
            if (customerOrder.CustomerId != customer.CustomerId)
            {
                return Forbid();
            }

            return View(customerOrder);
        }

        // GET: Orders/Create
        [Authorize(Roles = "Pharmacist")]
        public async Task<IActionResult> Create()
        {
            // Get all customers for the dropdown
            var customers = await _context.Customers
                .Include(c => c.User)
                .OrderBy(c => c.Name)
                .ToListAsync();

            // Get all medications for the dropdown
            var medications = await _context.Medications
                .Include(m => m.DosageForm)
                .Where(m => m.QuantityInStock > 0) // Only show medications in stock
                .OrderBy(m => m.Name)
                .ToListAsync();

            ViewBag.Customers = customers;
            ViewBag.Medications = medications;

            return View();
        }

        // GET: Orders/CreateCustomer - Customer creates order for themselves
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> CreateCustomer()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

            if (customer == null) return Forbid();

            // Get all medications from completed uploaded prescriptions
            var uploadedPrescribedMedicationsQuery = _context.UnprocessedScripts
                .Include(u => u.Prescription)
                    .ThenInclude(p => p.PrescriptionLines)
                        .ThenInclude(pl => pl.Medication)
                            .ThenInclude(m => m.DosageForm)
                .Where(u => u.CustomerId == customer.CustomerId &&
                           u.Status == UnprocessedScript.PrescriptionStatus.Completed &&
                           u.Prescription != null)
                .SelectMany(u => u.Prescription.PrescriptionLines)
                .Select(pl => pl.Medication);

            // Also include medications from imported prescriptions (UploadId == null)
            var importedPrescribedMedicationsQuery = _context.Prescriptions
                .Include(p => p.PrescriptionLines)
                    .ThenInclude(pl => pl.Medication)
                        .ThenInclude(m => m.DosageForm)
                .Where(p => p.CustomerId == customer.CustomerId && p.UploadId == null)
                .SelectMany(p => p.PrescriptionLines)
                .Select(pl => pl.Medication);

            var prescribedMedications = await uploadedPrescribedMedicationsQuery
                .Union(importedPrescribedMedicationsQuery)
                .Distinct()
                .OrderBy(m => m.Name)
                .ToListAsync();

            ViewBag.Customer = customer;
            ViewBag.Medications = prescribedMedications;

            return View();
        }

        // POST: Orders/CreateCustomer - Customer creates order for themselves
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> CreateCustomer([Bind("OrderItems,OrderNotes")] CustomerOrderCreateRequest request)
        {
            // Debug logging
            _logger.LogInformation("CreateCustomer called with request: {@Request}", request);
            _logger.LogInformation("Request.OrderItems count: {Count}", request?.OrderItems?.Count ?? 0);
            _logger.LogInformation("Request.OrderNotes: {Notes}", request?.OrderNotes);
            _logger.LogInformation("ModelState.IsValid: {IsValid}", ModelState.IsValid);
            
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                _logger.LogWarning("Model validation failed: {Errors}", string.Join(", ", errors));
            }
            
            try
            {
                if (ModelState.IsValid)
                {
                    var currentUser = await _userManager.GetUserAsync(User);
                    if (currentUser == null) return Challenge();

                    var customer = await _context.Customers
                        .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

                    if (customer == null) return Forbid();

                    // Check for duplicate medications in the order
                    var medicationIds = request.OrderItems.Select(i => i.MedicationId).ToList();
                    var duplicateMedications = medicationIds.GroupBy(id => id)
                        .Where(g => g.Count() > 1)
                        .Select(g => g.Key)
                        .ToList();

                    if (duplicateMedications.Any())
                    {
                        var duplicateMedicationNames = await _context.Medications
                            .Where(m => duplicateMedications.Contains(m.MedicationId))
                            .Select(m => m.Name)
                            .ToListAsync();
                        
                        TempData["ErrorMessage"] = $"Duplicate medications found: {string.Join(", ", duplicateMedicationNames)}";
                        
                        // Reload the view data for non-AJAX requests
                        var currentUserReload2 = await _userManager.GetUserAsync(User);
                        var customerReload2 = await _context.Customers
                            .FirstOrDefaultAsync(c => c.UserId == currentUserReload2.Id);

                        var medicationsReload = await _context.Medications
                            .Include(m => m.DosageForm)
                            .Where(m => m.QuantityInStock > 0)
                            .OrderBy(m => m.Name)
                            .ToListAsync();

                        ViewBag.Customer = customerReload2;
                        ViewBag.Medications = medicationsReload;

                        return View(request);
                    }

                    // Calculate total amount
                    double totalAmount = 0;
                    var orderItems = new List<OrderItem>();

                    // Create order items and calculate total
                    foreach (var itemRequest in request.OrderItems)
                    {
                        var medication = await _context.Medications.FindAsync(itemRequest.MedicationId);
                        if (medication != null)
                        {
                            var orderItem = new OrderItem
                            {
                                MedicationId = itemRequest.MedicationId,
                                QuantityOrdered = itemRequest.Quantity,
                                UnitPrice = medication.Price,
                                DispensingStatus = DispensingStatusEnum.Pending
                            };

                            totalAmount += (medication.Price * itemRequest.Quantity);
                            orderItems.Add(orderItem);
                        }
                    }

                    // Create the order
                    var order = new Order
                    {
                        CustomerId = customer.CustomerId,
                        PharmacistId = null, // Will be assigned when pharmacist processes
                        OrderDate = DateTime.UtcNow,
                        OrderStatus = OrderStatusEnum.Pending,
                        PaymentStatus = false,
                        TotalAmount = totalAmount,
                        LastUpdated = DateTime.UtcNow,
                        OrderNotes = request.OrderNotes ?? ""
                    };

                    _context.Add(order);
                    await _context.SaveChangesAsync();

                    // Add order items with the correct OrderId
                    foreach (var orderItem in orderItems)
                    {
                        orderItem.OrderId = order.OrderId;
                        _context.Add(orderItem);
                    }

                    await _context.SaveChangesAsync();

                    // Log activity
                    var itemCount = request.OrderItems?.Count ?? 0;
                    await _activityService.LogActivityAsync(
                        customer.CustomerId,
                        "OrderCreated",
                        $"Created order with {itemCount} item(s) - Total: R{order.TotalAmount:F2}",
                        "Order",
                        order.OrderId
                    );

                    // Group order items by supplier and send email notifications
                    await SendSupplierOrderNotifications(order.OrderId);

                    // Check if it's an AJAX request
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = true, message = "Order created successfully! A pharmacist will process it soon." });
                    }

                    TempData["SuccessMessage"] = "Order created successfully! A pharmacist will process it soon.";
                    return RedirectToAction(nameof(Index)); // Redirect to customer's order list
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating customer order");
                
                // Check if it's an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "An error occurred while creating the order." });
                }
                
                TempData["ErrorMessage"] = "An error occurred while creating the order.";
            }

            // If we get here, something went wrong
            // Check if it's an AJAX request
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                
                var errorMessage = errors.Any() ? string.Join(", ", errors) : "Please check your input and try again.";
                return Json(new { success = false, message = errorMessage });
            }

            // Reload the view data for non-AJAX requests
            var currentUserReload = await _userManager.GetUserAsync(User);
            var customerReload = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserId == currentUserReload.Id);

            // Get all medications from completed uploaded prescriptions
            var uploadedMedicationsReloadQuery = _context.UnprocessedScripts
                .Include(u => u.Prescription)
                    .ThenInclude(p => p.PrescriptionLines)
                        .ThenInclude(pl => pl.Medication)
                            .ThenInclude(m => m.DosageForm)
                .Where(u => u.CustomerId == customerReload.CustomerId &&
                           u.Status == UnprocessedScript.PrescriptionStatus.Completed &&
                           u.Prescription != null)
                .SelectMany(u => u.Prescription.PrescriptionLines)
                .Select(pl => pl.Medication);

            // Also include medications from imported prescriptions (UploadId == null)
            var importedMedicationsReloadQuery = _context.Prescriptions
                .Include(p => p.PrescriptionLines)
                    .ThenInclude(pl => pl.Medication)
                        .ThenInclude(m => m.DosageForm)
                .Where(p => p.CustomerId == customerReload.CustomerId && p.UploadId == null)
                .SelectMany(p => p.PrescriptionLines)
                .Select(pl => pl.Medication);

            var medications = await uploadedMedicationsReloadQuery
                .Union(importedMedicationsReloadQuery)
                .Distinct()
                .OrderBy(m => m.Name)
                .ToListAsync();

            ViewBag.Customer = customerReload;
            ViewBag.Medications = medications;

            return View(request);
        }

        // POST: Orders/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Pharmacist")]
        public async Task<IActionResult> Create([Bind("CustomerId,OrderItems,OrderNotes")] OrderCreateRequest request)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var currentUser = await _userManager.GetUserAsync(User);
                    if (currentUser == null) return Challenge();

                    var pharmacist = await _context.Pharmacists
                        .FirstOrDefaultAsync(p => p.UserId == currentUser.Id);

                    if (pharmacist == null) return Forbid();

                    // Create the order
                    var order = new Order
                    {
                        CustomerId = request.CustomerId,
                        PharmacistId = pharmacist.PharmacistId,
                        OrderDate = DateTime.UtcNow,
                        OrderStatus = OrderStatusEnum.Pending,
                        LastUpdated = DateTime.UtcNow,
                        OrderNotes = request.OrderNotes ?? ""
                    };

                    _context.Add(order);
                    await _context.SaveChangesAsync();

                    // Check for duplicate medications in the order
                    var medicationIds = request.OrderItems.Select(i => i.MedicationId).ToList();
                    var duplicateMedications = medicationIds.GroupBy(id => id)
                        .Where(g => g.Count() > 1)
                        .Select(g => g.Key)
                        .ToList();

                    if (duplicateMedications.Any())
                    {
                        var duplicateMedicationNames = await _context.Medications
                            .Where(m => duplicateMedications.Contains(m.MedicationId))
                            .Select(m => m.Name)
                            .ToListAsync();
                        
                        return Json(new { success = false, message = $"Duplicate medications found: {string.Join(", ", duplicateMedicationNames)}" });
                    }

                    // Create order items
                    foreach (var itemRequest in request.OrderItems)
                    {
                        var medication = await _context.Medications.FindAsync(itemRequest.MedicationId);
                        if (medication != null)
                        {
                            var orderItem = new OrderItem
                            {
                                OrderId = order.OrderId,
                                MedicationId = itemRequest.MedicationId,
                                QuantityOrdered = itemRequest.Quantity,
                                UnitPrice = medication.Price,
                                DispensingStatus = DispensingStatusEnum.Pending
                            };

                            _context.Add(orderItem);
                        }
                    }

                    await _context.SaveChangesAsync();

                    // Group order items by supplier and send email notifications
                    await SendSupplierOrderNotifications(order.OrderId);

                    TempData["MESSAGE"] = "Order created successfully!";
                    return RedirectToAction(nameof(OrdersForCustomers));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order");
                TempData["Error"] = "An error occurred while creating the order.";
            }

            // If we get here, something went wrong, reload the view data
            var customers = await _context.Customers
                .Include(c => c.User)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var medications = await _context.Medications
                .Include(m => m.DosageForm)
                .Where(m => m.QuantityInStock > 0)
                .OrderBy(m => m.Name)
                .ToListAsync();

            ViewBag.Customers = customers;
            ViewBag.Medications = medications;

            return View(request);
        }


        // Edit functionality removed - orders should not be editable once placed

        // GET: Orders/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

            if (customer == null || order.CustomerId != customer.CustomerId)
            {
                return Forbid();
            }

            return View(order);
        }

        // POST: Orders/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) return Challenge();

                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

                if (customer == null) return Forbid();

                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o => o.OrderId == id);

                if (order == null)
                {
                    TempData["ErrorMessage"] = "Order not found.";
                    return RedirectToAction(nameof(Index));
                }

                if (order.CustomerId != customer.CustomerId)
                {
                    TempData["ErrorMessage"] = "You can only delete your own orders.";
                    return RedirectToAction(nameof(Index));
                }

                // Check if order can be deleted (only pending orders)
                if (order.OrderStatus != OrderStatusEnum.Pending)
                {
                    TempData["ErrorMessage"] = "Only pending orders can be deleted.";
                    return RedirectToAction(nameof(Index));
                }

                // First remove order items to avoid foreign key constraint issues
                if (order.OrderItems != null && order.OrderItems.Any())
                {
                    foreach (var item in order.OrderItems)
                    {
                        var medication = await _context.Medications.FindAsync(item.MedicationId);
                        if (medication != null)
                        {
                            medication.QuantityInStock += item.QuantityOrdered;
                            _context.Update(medication);
                        }
                    }
                    
                    // Remove order items first
                    _context.OrderItems.RemoveRange(order.OrderItems);
                }

                // Now remove the order
                _context.Orders.Remove(order);
                
                // Save changes
                var result = await _context.SaveChangesAsync();
                
                if (result > 0)
                {
                    TempData["SuccessMessage"] = "Order deleted successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to delete order. No changes were saved.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting order {OrderId}: {Message}", id, ex.Message);
                TempData["ErrorMessage"] = $"An error occurred while deleting the order: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Orders/Process/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Process(int id)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) return Json(new { success = false, error = "User not authenticated" });

                // Try to find existing pharmacist record
                var pharmacist = await _context.Pharmacists
                    .FirstOrDefaultAsync(p => p.UserId == currentUser.Id);

                // If pharmacist record doesn't exist, create a basic one
                if (pharmacist == null)
                {
                    pharmacist = new Pharmacist
                    {
                        UserId = currentUser.Id,
                        Email = currentUser.Email,
                        Name = currentUser.FullName ?? "Unknown",
                        Surname = "",
                        IDNumber = "",
                        RegistrationNumber = "",
                        CellPhone = "",
                        IsActive = true
                    };

                    _context.Pharmacists.Add(pharmacist);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Created new pharmacist record for user {UserId}", currentUser.Id);
                }

                var order = await _context.Orders.FindAsync(id);
                if (order == null)
                {
                    return Json(new { success = false, error = "Order not found" });
                }

                if (order.OrderStatus != OrderStatusEnum.Pending)
                {
                    return Json(new { success = false, error = "Order cannot be processed in its current status" });
                }

                order.PharmacistId = pharmacist.PharmacistId;
                order.OrderStatus = OrderStatusEnum.Processing;
                order.LastUpdated = DateTime.UtcNow;

                _context.Update(order);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Order is now being processed",
                    orderId = order.OrderId,
                    newStatus = order.OrderStatus.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing order");
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id)
        {
            try
            {
                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Medication)
                    .FirstOrDefaultAsync(o => o.OrderId == id);

                if (order == null)
                {
                    return Json(new { success = false, error = "Order not found" });
                }

                if (order.OrderStatus != OrderStatusEnum.Processing)
                {
                    return Json(new { success = false, error = "Order must be in processing status to complete" });
                }

                // Check if any items are out of stock
                if (order.OrderItems.Any(oi => oi.DispensingStatus == DispensingStatusEnum.OutOfStock))
                {
                    return Json(new
                    {
                        success = false,
                        error = "Cannot complete order - some items are out of stock"
                    });
                }

                // Check if all items have been dispensed (only Filled)
                if (order.OrderItems.Any(oi => oi.DispensingStatus == DispensingStatusEnum.Pending))
                {
                    return Json(new
                    {
                        success = false,
                        error = "Cannot complete order - not all items have been dispensed"
                    });
                }

                order.OrderStatus = OrderStatusEnum.Ready;
                order.LastUpdated = DateTime.UtcNow;

                _context.Update(order);
                await _context.SaveChangesAsync();

                // Send order ready for collection email
                var customer = await _context.Customers.Include(c => c.User).FirstOrDefaultAsync(c => c.CustomerId == order.CustomerId);
                if (customer != null && customer.User != null)
                {
                    var orderDetailsBuilder = new StringBuilder();
                    foreach (var item in order.OrderItems)
                    {
                        orderDetailsBuilder.AppendLine($"• {item.Medication?.Name} (x{item.QuantityOrdered}) - R{item.TotalPrice:F2}");
                    }

                    var emailService = HttpContext.RequestServices.GetRequiredService<IBhayiPharmacyManagementSystem.Services.IEmailService>();
                    await emailService.SendOrderReadyForCollectionNotificationAsync(
                        customer.User.Email,
                        customer.FullName,
                        order.OrderId,
                        orderDetailsBuilder.ToString(),
                        (decimal)order.TotalAmount
                    );
                }

                // Add notification for customer dashboard
                await AddCustomerNotification(order.CustomerId,
                    $"Gr-8 Your order #{order.OrderId} is ready for collection.",
                    "Order", order.OrderId.ToString());

                return Json(new
                {
                    success = true,
                    message = "Order marked as ready for pickup",
                    orderId = order.OrderId,
                    newStatus = order.OrderStatus.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing order");
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DispenseItem(int id, OrderItem item)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) return Json(new { success = false, error = "User not authenticated" });

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    _logger.LogWarning("DispenseItem model validation failed: {Errors}", string.Join(", ", errors));
                    return Json(new { success = false, error = "Invalid item data provided." });
                }

                var pharmacist = await _context.Pharmacists
                    .FirstOrDefaultAsync(p => p.UserId == currentUser.Id);

                if (pharmacist == null) return Json(new { success = false, error = "Pharmacist not found" });

                var orderItem = await _context.OrderItems
                    .Include(oi => oi.Medication)
                    .Include(oi => oi.Order)
                        .ThenInclude(o => o.Customer)
                    .FirstOrDefaultAsync(oi => oi.OrderItemId == id);

                if (orderItem == null)
                {
                    return Json(new { success = false, error = "Order item not found" });
                }

                // Check stock availability before allowing "Filled" status
                if (item.DispensingStatus == DispensingStatusEnum.Filled)
                {
                    if (orderItem.Medication.QuantityInStock < orderItem.QuantityOrdered)
                    {
                        return Json(new
                        {
                            success = false,
                            error = "Insufficient stock",
                            isOutOfStock = true,
                            medicationName = orderItem.Medication.Name,
                            quantityOrdered = orderItem.QuantityOrdered,
                            quantityInStock = orderItem.Medication.QuantityInStock,
                            orderId = orderItem.OrderId
                        });
                    }
                }

                orderItem.DispensedBy = pharmacist.PharmacistId;
                orderItem.DispensedDate = DateTime.UtcNow;
                orderItem.DispensingStatus = item.DispensingStatus;
                orderItem.DispensingNotes = item.DispensingNotes;

                if (item.DispensingStatus == DispensingStatusEnum.Filled)
                {
                    orderItem.QuantityDispensed = orderItem.QuantityOrdered;
                    // Reduce stock when medication is dispensed
                    orderItem.Medication.QuantityInStock -= orderItem.QuantityOrdered;
                }

                _context.Update(orderItem);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    orderId = orderItem.OrderId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating item dispensing status");
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NotifyCustomerOutOfStock(int orderId, int orderItemId)
        {
            try
            {
                var orderItem = await _context.OrderItems
                    .Include(oi => oi.Medication)
                    .Include(oi => oi.Order)
                        .ThenInclude(o => o.Customer)
                    .FirstOrDefaultAsync(oi => oi.OrderItemId == orderItemId && oi.OrderId == orderId);

                if (orderItem == null)
                {
                    return Json(new { success = false, error = "Order item not found" });
                }

                // Set the dispensing status to OutOfStock
                orderItem.DispensingStatus = DispensingStatusEnum.OutOfStock;
                orderItem.DispensingNotes = "Medication out of stock - customer notified";
                orderItem.DispensedDate = DateTime.UtcNow;

                _context.Update(orderItem);
                await _context.SaveChangesAsync();

                // Send email notification to customer
                var emailService = HttpContext.RequestServices.GetRequiredService<IEmailService>();
                await emailService.SendOutOfStockNotificationToCustomerAsync(
                    orderItem.Order.Customer.Email,
                    orderItem.Order.Customer.Name,
                    orderId,
                    orderItem.Medication.Name
                );

                // Send notification to pharmacy manager
                var pharmacyManager = await _context.Users
                    .Where(u => _context.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == "PharmacyManager"))
                    .FirstOrDefaultAsync();

                if (pharmacyManager != null)
                {
                    await emailService.SendOutOfStockNotificationToPharmacyManagerAsync(
                        pharmacyManager.Email,
                        pharmacyManager.FullName ?? "Pharmacy Manager",
                        orderId,
                        orderItem.Medication.Name,
                        orderItem.QuantityOrdered
                    );
                }

                // Add notification to customer's notification list
                var notification = new Notification
                {
                    CustomerId = orderItem.Order.CustomerId,
                    Message = $"Your order #{orderId} - {orderItem.Medication.Name} is currently out of stock. We are working to restock it as soon as possible.",
                    DateSent = DateTime.UtcNow,
                    IsRead = false,
                    RelatedEntityType = "Order",
                    RelatedEntityId = orderId.ToString()
                };
                _context.Notifications.Add(notification);

                // Add notification to pharmacy manager
                if (pharmacyManager != null)
                {
                    var managerNotification = new NotificationPharmacyManager
                    {
                        Title = "URGENT: Medication Out of Stock",
                        Message = $"Order #{orderId} - {orderItem.Medication.Name} (Qty: {orderItem.QuantityOrdered}) is out of stock. Customer has been notified.",
                        Timestamp = DateTime.UtcNow,
                        Type = NotificationType.Critical,
                        IsRead = false,
                        UserId = pharmacyManager.Id
                    };
                    _context.NotificationP.Add(managerNotification);
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Customer has been notified about the out of stock medication"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying customer about out of stock medication");
                return Json(new { success = false, error = ex.Message });
            }
        }

        // GET: Orders/Dispense
        public async Task<IActionResult> Dispense()
        {
            // Get all pending orders that need dispensing
            var pendingOrders = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Medication)
                .Where(o => o.OrderStatus == OrderStatusEnum.Pending || o.OrderStatus == OrderStatusEnum.Processing)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(pendingOrders);
        }

        // POST: Orders/Dispense
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Dispense(int orderId, int orderItemId, DispensingStatusEnum dispensingStatus, int? quantityDispensed, string dispensingNotes)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) return Json(new { success = false, error = "User not authenticated" });

                var pharmacist = await _context.Pharmacists
                    .FirstOrDefaultAsync(p => p.UserId == currentUser.Id);

                if (pharmacist == null) return Json(new { success = false, error = "Pharmacist not found" });

                var orderItem = await _context.OrderItems
                    .Include(oi => oi.Order)
                    .Include(oi => oi.Medication)
                    .FirstOrDefaultAsync(oi => oi.OrderItemId == orderItemId);

                if (orderItem == null)
                {
                    return Json(new { success = false, error = "Order item not found" });
                }

                // Check stock availability before allowing "Filled" status
                if (dispensingStatus == DispensingStatusEnum.Filled)
                {
                    if (orderItem.Medication.QuantityInStock < orderItem.QuantityOrdered)
                    {
                        return Json(new
                        {
                            success = false,
                            error = "Insufficient stock",
                            isOutOfStock = true,
                            medicationName = orderItem.Medication.Name,
                            quantityOrdered = orderItem.QuantityOrdered,
                            quantityInStock = orderItem.Medication.QuantityInStock,
                            orderId = orderItem.OrderId,
                            orderItemId = orderItem.OrderItemId
                        });
                    }
                }

                // Update dispensing information
                orderItem.DispensedBy = pharmacist.PharmacistId;
                orderItem.DispensedDate = DateTime.UtcNow;
                orderItem.DispensingStatus = dispensingStatus;
                orderItem.DispensingNotes = dispensingNotes ?? "";

                if (dispensingStatus == DispensingStatusEnum.Filled)
                {
                    orderItem.QuantityDispensed = quantityDispensed ?? orderItem.QuantityOrdered;
                    // Reduce stock when medication is dispensed
                    orderItem.Medication.QuantityInStock -= orderItem.QuantityOrdered;
                }
                else
                {
                    orderItem.QuantityDispensed = 0;
                }

                // Update order status to Processing if it was Pending
                if (orderItem.Order.OrderStatus == OrderStatusEnum.Pending)
                {
                    orderItem.Order.OrderStatus = OrderStatusEnum.Processing;
                    orderItem.Order.PharmacistId = pharmacist.PharmacistId;
                    orderItem.Order.LastUpdated = DateTime.UtcNow;
                }

                _context.Update(orderItem);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Medication dispensed successfully",
                    orderId = orderItem.OrderId,
                    orderItemId = orderItem.OrderItemId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error dispensing medication");
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDispenseStatus(int orderId, List<DispenseUpdateRequest> dispenseUpdates)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) return Json(new { success = false, error = "User not authenticated" });

                var pharmacist = await _context.Pharmacists
                    .FirstOrDefaultAsync(p => p.UserId == currentUser.Id);

                if (pharmacist == null) return Json(new { success = false, error = "Pharmacist not found" });

                // Get the order to verify access
                var order = await _context.Orders.FindAsync(orderId);
                if (order == null)
                {
                    return Json(new { success = false, error = "Order not found" });
                }

                if (order.OrderStatus != OrderStatusEnum.Processing)
                {
                    return Json(new { success = false, error = "Order must be in processing status to update dispense status" });
                }

                // Update each order item
                foreach (var update in dispenseUpdates)
                {
                    var orderItem = await _context.OrderItems
                        .Include(oi => oi.Medication)
                        .FirstOrDefaultAsync(oi => oi.OrderItemId == update.OrderItemId);
                    
                    if (orderItem != null && orderItem.OrderId == orderId)
                    {
                        // Check stock availability before allowing "Filled" status
                        if (update.DispensingStatus == DispensingStatusEnum.Filled)
                        {
                            if (orderItem.Medication.QuantityInStock < orderItem.QuantityOrdered)
                            {
                                return Json(new
                                {
                                    success = false,
                                    error = "Insufficient stock",
                                    isOutOfStock = true,
                                    medicationName = orderItem.Medication.Name,
                                    quantityOrdered = orderItem.QuantityOrdered,
                                    quantityInStock = orderItem.Medication.QuantityInStock,
                                    orderId = orderItem.OrderId,
                                    orderItemId = orderItem.OrderItemId
                                });
                            }
                        }

                        orderItem.DispensingStatus = update.DispensingStatus;
                        orderItem.DispensedBy = pharmacist.PharmacistId;
                        orderItem.DispensedDate = DateTime.UtcNow;

                        if (update.DispensingStatus == DispensingStatusEnum.Filled)
                        {
                            orderItem.QuantityDispensed = orderItem.QuantityOrdered;
                            // Reduce stock when medication is dispensed
                            orderItem.Medication.QuantityInStock -= orderItem.QuantityOrdered;
                        }
                        else
                        {
                            orderItem.QuantityDispensed = 0;
                        }

                        _context.Update(orderItem);
                    }
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Dispense status updated successfully",
                    orderId = orderId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating dispense status for order {OrderId}", orderId);
                return Json(new { success = false, error = ex.Message });
            }
        }

        private bool OrderExists(int id)
        {
            return _context.Orders.Any(e => e.OrderId == id);
        }

        private async Task SendSupplierOrderNotifications(int orderId)
        {
            try
            {
                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Medication)
                            .ThenInclude(m => m.Supplier)
                    .FirstOrDefaultAsync(o => o.OrderId == orderId);

                if (order == null) return;

                var groupedBySupplier = order.OrderItems
                    .Where(oi => oi.Medication?.Supplier != null)
                    .GroupBy(oi => oi.Medication.Supplier)
                    .ToList();

                var emailService = HttpContext.RequestServices.GetRequiredService<IBhayiPharmacyManagementSystem.Services.IEmailService>();

                foreach (var supplierGroup in groupedBySupplier)
                {
                    var supplier = supplierGroup.Key;
                    var supplierEmail = supplier.Email;
                    var supplierName = supplier.Name;

                    var orderDetailsBuilder = new StringBuilder();
                    orderDetailsBuilder.AppendLine($"Order ID: {order.OrderId}");
                    orderDetailsBuilder.AppendLine($"Date: {order.OrderDate.ToShortDateString()}");
                    orderDetailsBuilder.AppendLine("Medications:");
                    foreach (var item in supplierGroup)
                    {
                        orderDetailsBuilder.AppendLine($"• {item.Medication.Name} (Quantity: {item.QuantityOrdered})");
                    }

                    await emailService.SendSupplierOrderNotificationAsync(
                        supplierEmail,
                        supplierName,
                        order.OrderId,
                        orderDetailsBuilder.ToString()
                    );
                }
                _logger.LogInformation($"Supplier order notifications sent for Order ID: {orderId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending supplier order notifications for Order ID: {orderId}");
            }
        }

        public async Task<IActionResult> OrdersForCustomers (OrderStatusEnum? status = null)
        {
            var ordersQuery = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Medication)
                .OrderByDescending(o => o.OrderDate)
                .AsQueryable();

            if (status.HasValue)
            {
                ordersQuery = ordersQuery.Where(o => o.OrderStatus == status.Value);
            }

            var orders = await ordersQuery.ToListAsync();

            ViewBag.StatusFilter = status;
            return View(orders);
        }

        // GET: Orders/Review/5 - Review order details before processing
        public async Task<IActionResult> Review(int? id)
        {
            if (id == null)
            {
                TempData["ErrorMessage"] = "Order ID is required.";
                return RedirectToAction(nameof(OrdersForCustomers));
            }

            var order = await _context.Orders
                .Include(o => o.Customer)
                    .ThenInclude(c => c.User)
                .Include(o => o.Customer)
                    .ThenInclude(c => c.Allergies)
                        .ThenInclude(ca => ca.ActiveIngredient)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Medication)
                        .ThenInclude(m => m.DosageForm)
                .Include(o => o.Pharmacist)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                TempData["ErrorMessage"] = "Order not found.";
                return RedirectToAction(nameof(OrdersForCustomers));
            }

            // Check if user has permission to view this order
            if (!User.IsInRole("Admin") && !User.IsInRole("Pharmacist"))
            {
                TempData["ErrorMessage"] = "You don't have permission to review this order.";
                return RedirectToAction(nameof(OrdersForCustomers));
            }

            return View(order);
        }

        // GET: Orders/PendingOrders
        public async Task<IActionResult> PendingOrders()
        {
            var ordersQuery = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Medication)
                .Where(o => o.OrderStatus == OrderStatusEnum.Pending)
                .OrderByDescending(o => o.OrderDate)
                .AsQueryable();

            var orders = await ordersQuery.ToListAsync();
            ViewBag.StatusFilter = OrderStatusEnum.Pending;
            return View("OrdersForCustomers", orders);
        }

        // GET: Orders/CompletedOrders
        public async Task<IActionResult> CompletedOrders()
        {
            var ordersQuery = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Medication)
                .Where(o => o.OrderStatus == OrderStatusEnum.Ready)
                .OrderByDescending(o => o.OrderDate)
                .AsQueryable();

            var orders = await ordersQuery.ToListAsync();
            ViewBag.StatusFilter = OrderStatusEnum.Ready;
            return View("OrdersForCustomers", orders);
        }

        // POST: Orders/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);
            if (customer == null) return Forbid();

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null) return NotFound();

            // Verify customer owns this order
            if (order.CustomerId != customer.CustomerId)
                return Forbid();

            // Only allow deletion of pending orders
            if (order.OrderStatus != OrderStatusEnum.Pending)
            {
                TempData["ErrorMessage"] = "Only pending orders can be deleted.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // Remove order items first
                _context.OrderItems.RemoveRange(order.OrderItems);
                
                // Remove the order
                _context.Orders.Remove(order);
                
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Order #{id} has been deleted successfully.";
                _logger.LogInformation($"Customer {customer.CustomerId} deleted order {id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting order {id}");
                TempData["ErrorMessage"] = "An error occurred while deleting the order. Please try again.";
            }

            return RedirectToAction(nameof(Index));
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

        // POST: Orders/MarkAsCollected/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Pharmacist")]
        public async Task<IActionResult> MarkAsCollected(int id)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) return Challenge();

                var pharmacist = await _context.Pharmacists
                    .FirstOrDefaultAsync(p => p.UserId == currentUser.Id);

                if (pharmacist == null) return Forbid();

                var order = await _context.Orders
                    .Include(o => o.Customer)
                    .FirstOrDefaultAsync(o => o.OrderId == id);

                if (order == null)
                {
                    TempData["ErrorMessage"] = "Order not found.";
                    return RedirectToAction(nameof(Index));
                }

                if (order.OrderStatus != OrderStatusEnum.Ready)
                {
                    TempData["ErrorMessage"] = "Order is not ready for collection.";
                    return RedirectToAction(nameof(Index));
                }

                // Mark order as collected
                order.OrderStatus = OrderStatusEnum.Collected;
                order.CollectedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Order #{order.OrderId} has been marked as collected successfully!";
                _logger.LogInformation($"Order {order.OrderId} marked as collected by pharmacist {pharmacist.PharmacistId}");

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking order {OrderId} as collected", id);
                TempData["ErrorMessage"] = "An error occurred while marking the order as collected.";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}