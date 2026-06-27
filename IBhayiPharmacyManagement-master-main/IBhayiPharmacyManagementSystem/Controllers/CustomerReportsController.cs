using IBhayiPharmacyManagementSystem.Data;
using IBhayiPharmacyManagementSystem.Models;
using IBhayiPharmacyManagementSystem.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.IO;

namespace IBhayiPharmacyManagementSystem.Controllers
{
    [Authorize(Roles = "Customer")]
    public class CustomerReportsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly ILogger<CustomerReportsController> _logger;

        public CustomerReportsController(
            AppDbContext context,
            UserManager<Users> userManager,
            ILogger<CustomerReportsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: CustomerReports - Show report options
        public IActionResult Index()
        {
            return View();
        }

        // POST: CustomerReports/GenerateReport - Generate unified report
        [HttpPost]
        public async Task<IActionResult> GenerateReport(DateTime startDate, DateTime endDate, string groupBy,
            bool includePrescriptions = false, bool includeOrders = false)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);
            if (customer == null) return Forbid();

            // Validate inputs
            if (startDate > endDate)
            {
                TempData["InfoMessage"] = "Start date cannot be after end date.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrEmpty(groupBy))
            {
                TempData["InfoMessage"] = "Please select a grouping option.";
                return RedirectToAction(nameof(Index));
            }

            if (!includePrescriptions && !includeOrders)
            {
                TempData["InfoMessage"] = "Please select at least one report type.";
                return RedirectToAction(nameof(Index));
            }

            // Get data based on selections
            List<DispensedPrescription> dispensedPrescriptions = new List<DispensedPrescription>();
            List<Order> orders = new List<Order>();

            // Always get dispensed medications when includePrescriptions is true
            if (includePrescriptions)
            {
                // Get dispensed prescriptions from repeat requests
                var dispensedPrescriptionsFromRepeats = await _context.DispensedPrescriptions
                    .Include(dp => dp.PrescriptionLine)
                        .ThenInclude(pl => pl.Prescription)
                            .ThenInclude(p => p.Doctor)
                    .Include(dp => dp.PrescriptionLine)
                        .ThenInclude(pl => pl.Medication)
                    .Include(dp => dp.Pharmacist)
                    .Where(dp => dp.PrescriptionLine.Prescription.CustomerId == customer.CustomerId)
                    .Where(dp => dp.DispensedDate >= startDate && dp.DispensedDate <= endDate.AddDays(1))
                    .OrderBy(dp => dp.DispensedDate)
                    .ToListAsync();

                dispensedPrescriptions.AddRange(dispensedPrescriptionsFromRepeats);
            }

            // Note: Do not include order-based dispensations in the prescriptions report; prescriptions section must only reflect actual dispensed prescriptions tied to a prescription.

            // Sort all dispensed prescriptions by date
            dispensedPrescriptions = dispensedPrescriptions.OrderBy(dp => dp.DispensedDate).ToList();

            if (includeOrders)
            {
                orders = await _context.Orders
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Medication)
                    .Where(o => o.CustomerId == customer.CustomerId)
                    .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate.AddDays(1))
                    .OrderBy(o => o.OrderDate)
                    .ToListAsync();
            }

            // Check if any data exists
            if (!dispensedPrescriptions.Any() && !orders.Any())
            {
                TempData["InfoMessage"] = "No data found for the selected date range.";
                return RedirectToAction(nameof(Index));
            }

            // Generate appropriate report
            byte[] reportBytes;
            string fileName;

            if (includePrescriptions && includeOrders)
            {
                // Generate combined report (both prescriptions and orders)
                reportBytes = GenerateCombinedReport(dispensedPrescriptions, orders, startDate, endDate, groupBy, customer);
                fileName = $"CombinedReport_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf";
            }
            else if (includePrescriptions)
            {
                // Generate prescriptions only report
                reportBytes = GeneratePrescriptionsReport(dispensedPrescriptions, startDate, endDate, groupBy, customer);
                fileName = $"PrescriptionsReport_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf";
            }
            else if (includeOrders)
            {
                // Generate orders only report
                reportBytes = GenerateOrdersReport(orders, startDate, endDate, groupBy, customer);
                fileName = $"OrdersReport_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf";
            }
            else
            {
                TempData["InfoMessage"] = "Invalid report selection.";
                return RedirectToAction(nameof(Index));
            }

            return File(reportBytes, "application/pdf", fileName);
        }

        private byte[] GeneratePrescriptionsReport(List<DispensedPrescription> prescriptions, DateTime startDate, DateTime endDate, string groupBy, Customer customer)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                Document document = new Document(PageSize.A4, 25, 25, 30, 30);
                PdfWriter writer = PdfWriter.GetInstance(document, ms);

                // Add header and footer
                writer.PageEvent = new PrescriptionReportPageEvent(customer.FullName);

                document.Open();

                // Title
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18);
                var title = new Paragraph("DISPENSED PRESCRIPTIONS BY DOCTOR", titleFont);
                title.Alignment = Element.ALIGN_CENTER;
                document.Add(title);

                document.Add(new Paragraph(" ")); // Spacing

                // Date range
                var dateFont = FontFactory.GetFont(FontFactory.HELVETICA, 12);
                var dateRange = new Paragraph($"Date Range: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}", dateFont);
                dateRange.Alignment = Element.ALIGN_CENTER;
                document.Add(dateRange);

                document.Add(new Paragraph(" ")); // Spacing

                // Customer info
                var customerInfo = new Paragraph($"Customer: {customer.FullName}", dateFont);
                document.Add(customerInfo);
                document.Add(new Paragraph(" ")); // Spacing

                if (groupBy.ToLower() == "doctor")
                {
                    GeneratePrescriptionsByDoctorReport(document, prescriptions);
                }
                else if (groupBy.ToLower() == "medication")
                {
                    GeneratePrescriptionsByMedicationReport(document, prescriptions);
                }

                document.Close();
                return ms.ToArray();
            }
        }

        private void GeneratePrescriptionsByDoctorReport(Document document, List<DispensedPrescription> prescriptions)
        {
            var groupFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14);
            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
            var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);

            var groupedPrescriptions = prescriptions
                .GroupBy(dp => 
                {
                    // Check if this is from an order (no prescription line) or from a prescription
                    if (dp.PrescriptionLineId == 0)
                    {
                        // For orders, use the doctor name from the virtual prescription
                        return dp.PrescriptionLine?.Prescription?.Doctor?.FullName ?? "No Doctor Assigned";
                    }
                    return dp.PrescriptionLine?.Prescription?.Doctor?.FullName ?? "No Doctor Assigned";
                })
                .OrderBy(g => g.Key);

            foreach (var group in groupedPrescriptions)
            {
                // Doctor header
                var doctorHeader = new Paragraph($"DOCTOR: {group.Key}", groupFont);
                document.Add(doctorHeader);
                document.Add(new Paragraph(" ")); // Spacing

                // Create table
                PdfPTable table = new PdfPTable(4);
                table.WidthPercentage = 100;

                // Table headers
                var header1 = new PdfPCell(new Phrase("Date", headerFont));
                header1.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header1);
                
                var header2 = new PdfPCell(new Phrase("Medication", headerFont));
                header2.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header2);
                
                var header3 = new PdfPCell(new Phrase("Qty", headerFont));
                header3.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header3);
                
                var header4 = new PdfPCell(new Phrase("Repeats", headerFont));
                header4.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header4);

                int groupQuantity = 0;
                int groupRepeats = 0;

                foreach (var prescription in group.OrderBy(dp => dp.DispensedDate))
                {
                    table.AddCell(new PdfPCell(new Phrase(prescription.DispensedDate.ToString("dd/MM/yyyy"), normalFont)));
                    table.AddCell(new PdfPCell(new Phrase(prescription.PrescriptionLine?.Medication?.Name ?? "Unknown Medication", normalFont)));
                    
                    // For prescriptions, show the actual dispensed quantity
                    // For orders, show the dispensed quantity (which is the same as ordered quantity when filled)
                    var quantityToShow = prescription.QuantityDispensed;
                    table.AddCell(new PdfPCell(new Phrase(quantityToShow.ToString(), normalFont)));
                    
                    // Get remaining repeats from prescription line (only for actual prescriptions, not orders)
                    var remainingRepeats = prescription.PrescriptionLineId == 0 ? 0 : (prescription.PrescriptionLine?.RepeatsRemaining ?? 0);
                    table.AddCell(new PdfPCell(new Phrase(remainingRepeats.ToString(), normalFont)));

                    groupQuantity += quantityToShow;
                    groupRepeats += remainingRepeats;
                }

                document.Add(table);
                document.Add(new Paragraph(" ")); // Spacing

                // Subtotal
                var subtotal = new Paragraph($"Sub-total: Qty: {groupQuantity}", normalFont);
                subtotal.Alignment = Element.ALIGN_LEFT;
                document.Add(subtotal);
                document.Add(new Paragraph(" ")); // Spacing
                document.Add(new Paragraph(" ")); // Spacing
            }

            // Grand total aligned left, appearing below the last group's subtotal area
            var grandTotalQuantity = prescriptions.Sum(dp => dp.QuantityDispensed);
            var grandTotalText = new Paragraph($"GRAND TOTAL: Qty: {grandTotalQuantity}", groupFont);
            grandTotalText.Alignment = Element.ALIGN_LEFT;
            document.Add(grandTotalText);
        }

        private void GeneratePrescriptionsByMedicationReport(Document document, List<DispensedPrescription> prescriptions)
        {
            var groupFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14);
            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
            var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);

            var groupedPrescriptions = prescriptions
                .GroupBy(dp => 
                {
                    // Check if this is from an order (no prescription line) or from a prescription
                    if (dp.PrescriptionLineId == 0)
                    {
                        return dp.PrescriptionLine?.Medication?.Name ?? "Unknown Medication"; // This will be the medication from the order
                    }
                    return dp.PrescriptionLine?.Medication?.Name ?? "Unknown Medication";
                })
                .OrderBy(g => g.Key);

            foreach (var group in groupedPrescriptions)
            {
                // Medication header
                var medicationHeader = new Paragraph($"MEDICATION: {group.Key}", groupFont);
                document.Add(medicationHeader);
                document.Add(new Paragraph(" ")); // Spacing

                // Create table
                PdfPTable table = new PdfPTable(4);
                table.WidthPercentage = 100;

                // Table headers
                var header1 = new PdfPCell(new Phrase("Date", headerFont));
                header1.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header1);
                
                var header2 = new PdfPCell(new Phrase("Doctor", headerFont));
                header2.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header2);
                
                var header3 = new PdfPCell(new Phrase("Qty", headerFont));
                header3.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header3);
                
                var header4 = new PdfPCell(new Phrase("Amount", headerFont));
                header4.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header4);

                decimal groupTotal = 0;
                int groupQuantity = 0;

                foreach (var prescription in group.OrderBy(dp => dp.DispensedDate))
                {
                    table.AddCell(new PdfPCell(new Phrase(prescription.DispensedDate.ToString("dd/MM/yyyy"), normalFont)));
                    
                    // Show doctor name or "Order-Based" for orders
                    var doctorName = prescription.PrescriptionLineId == 0 ? "Order-Based" : (prescription.PrescriptionLine?.Prescription?.Doctor?.FullName ?? "No Doctor Assigned");
                    table.AddCell(new PdfPCell(new Phrase(doctorName, normalFont)));
                    
                    table.AddCell(new PdfPCell(new Phrase(prescription.QuantityDispensed.ToString(), normalFont)));
                    table.AddCell(new PdfPCell(new Phrase($"R{prescription.AmountDue:F2}", normalFont)));

                    groupTotal += prescription.AmountDue;
                    groupQuantity += prescription.QuantityDispensed;
                }

                document.Add(table);
                document.Add(new Paragraph(" ")); // Spacing

                // Subtotal
                var subtotal = new Paragraph($"Sub-total: Qty: {groupQuantity}", normalFont);
                subtotal.Alignment = Element.ALIGN_RIGHT;
                document.Add(subtotal);
                document.Add(new Paragraph(" ")); // Spacing
                document.Add(new Paragraph(" ")); // Spacing
            }

            // Grand total aligned right for medication grouping to mirror subtotal
            var grandTotalQuantity = prescriptions.Sum(dp => dp.QuantityDispensed);
            var grandTotalText = new Paragraph($"GRAND TOTAL: Qty: {grandTotalQuantity}", groupFont);
            grandTotalText.Alignment = Element.ALIGN_RIGHT;
            document.Add(grandTotalText);
        }


        private byte[] GenerateOrdersReport(List<Order> orders, DateTime startDate, DateTime endDate, string groupBy, Customer customer)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                Document document = new Document(PageSize.A4, 25, 25, 30, 30);
                PdfWriter writer = PdfWriter.GetInstance(document, ms);

                // Add header and footer
                writer.PageEvent = new OrderReportPageEvent(customer.FullName);

                document.Open();

                // Title
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18);
                var title = new Paragraph("CUSTOMER ORDERS REPORT", titleFont);
                title.Alignment = Element.ALIGN_CENTER;
                document.Add(title);

                document.Add(new Paragraph(" ")); // Spacing

                // Date range
                var dateFont = FontFactory.GetFont(FontFactory.HELVETICA, 12);
                var dateRange = new Paragraph($"Date Range: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}", dateFont);
                dateRange.Alignment = Element.ALIGN_CENTER;
                document.Add(dateRange);

                document.Add(new Paragraph(" ")); // Spacing

                // Customer info
                var customerInfo = new Paragraph($"Customer: {customer.FullName}", dateFont);
                document.Add(customerInfo);
                document.Add(new Paragraph(" ")); // Spacing

                if (groupBy.ToLower() == "medication")
                {
                    GenerateOrdersByMedicationReport(document, orders);
                }
                else if (groupBy.ToLower() == "doctor")
                {
                    GenerateOrdersByDoctorReport(document, orders);
                }
                else
                {
                    GenerateOrdersByDateReport(document, orders);
                }

                document.Close();
                return ms.ToArray();
            }
        }

        private void GenerateOrdersByMedicationReport(Document document, List<Order> orders)
        {
            var groupFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14);
            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
            var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);

            var groupedOrders = orders
                .SelectMany(o => o.OrderItems.Select(oi => new { Order = o, Item = oi }))
                .GroupBy(x => x.Item.Medication.Name)
                .OrderBy(g => g.Key);

            foreach (var group in groupedOrders)
            {
                // Medication header
                var medicationHeader = new Paragraph($"MEDICATION: {group.Key}", groupFont);
                document.Add(medicationHeader);
                document.Add(new Paragraph(" ")); // Spacing

                // Create table
                PdfPTable table = new PdfPTable(4);
                table.WidthPercentage = 100;

                // Table headers
                var header1 = new PdfPCell(new Phrase("Date", headerFont));
                header1.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header1);
                
                var header2 = new PdfPCell(new Phrase("Order #", headerFont));
                header2.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header2);
                
                var header3 = new PdfPCell(new Phrase("Qty", headerFont));
                header3.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header3);
                
                var header4 = new PdfPCell(new Phrase("Amount", headerFont));
                header4.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header4);

                decimal groupTotal = 0;
                int groupQuantity = 0;

                foreach (var orderItem in group.OrderBy(x => x.Order.OrderDate))
                {
                    table.AddCell(new PdfPCell(new Phrase(orderItem.Order.OrderDate.ToString("dd/MM/yyyy"), normalFont)));
                    table.AddCell(new PdfPCell(new Phrase(orderItem.Order.OrderId.ToString(), normalFont)));
                    table.AddCell(new PdfPCell(new Phrase(orderItem.Item.QuantityOrdered.ToString(), normalFont)));
                    table.AddCell(new PdfPCell(new Phrase($"R{orderItem.Item.UnitPrice * orderItem.Item.QuantityOrdered:F2}", normalFont)));

                    groupTotal += (decimal)(orderItem.Item.UnitPrice * orderItem.Item.QuantityOrdered);
                    groupQuantity += orderItem.Item.QuantityOrdered;
                }

                document.Add(table);
                document.Add(new Paragraph(" ")); // Spacing

                // Subtotal
                var subtotal = new Paragraph($"Sub-total: Amount: R{groupTotal:F2}", normalFont);
                subtotal.Alignment = Element.ALIGN_RIGHT;
                document.Add(subtotal);
                document.Add(new Paragraph(" ")); // Spacing
                document.Add(new Paragraph(" ")); // Spacing
            }

            // Grand total
            var grandTotalAmount = orders.Sum(o => o.OrderItems.Sum(oi => oi.UnitPrice * oi.QuantityOrdered));
            var grandTotalText = new Paragraph($"GRAND TOTAL: Amount: R{grandTotalAmount:F2}", groupFont);
            grandTotalText.Alignment = Element.ALIGN_CENTER;
            document.Add(grandTotalText);
        }

        private void GenerateOrdersByDoctorReport(Document document, List<Order> orders)
        {
            var groupFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14);
            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
            var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);

            // Build a lookup from MedicationId to Doctor FullName based on customer's processed prescriptions
            var medicationIds = orders.SelectMany(o => o.OrderItems).Select(oi => oi.MedicationId).Distinct().ToList();

            var medicationToDoctor = _context.PrescriptionLines
                .Include(pl => pl.Prescription)
                    .ThenInclude(p => p.Doctor)
                .Where(pl => medicationIds.Contains(pl.MedicationId))
                .Where(pl => pl.Prescription != null && pl.Prescription.Doctor != null)
                .AsEnumerable()
                .GroupBy(pl => pl.MedicationId)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderByDescending(pl => pl.Prescription!.PrescriptionDate)
                        .Select(pl => pl.Prescription!.Doctor!.FullName)
                        .FirstOrDefault() ?? "No Doctor Assigned"
                );

            // Flatten to order items and group by inferred doctor from processed prescription
            var groupedOrderItems = orders
                .SelectMany(o => o.OrderItems.Select(oi => new { Order = o, Item = oi }))
                .GroupBy(x => medicationToDoctor.TryGetValue(x.Item.MedicationId, out var doc) ? doc : "No Doctor Assigned")
                .OrderBy(g => g.Key);

            foreach (var group in groupedOrderItems)
            {
                // Group header (Doctor)
                var doctorHeader = new Paragraph($"DOCTOR: {group.Key}", groupFont);
                document.Add(doctorHeader);
                document.Add(new Paragraph(" ")); // Spacing

                // Create table
                PdfPTable table = new PdfPTable(4);
                table.WidthPercentage = 100;

                // Table headers
                var header1 = new PdfPCell(new Phrase("Date", headerFont));
                header1.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header1);
                
                var header2 = new PdfPCell(new Phrase("Order #", headerFont));
                header2.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header2);
                
                var header3 = new PdfPCell(new Phrase("Items", headerFont));
                header3.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header3);
                
                var header4 = new PdfPCell(new Phrase("Total Amount", headerFont));
                header4.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header4);

                decimal groupTotal = 0;
                int groupItems = 0;

                foreach (var x in group.OrderBy(y => y.Order.OrderDate))
                {
                    table.AddCell(new PdfPCell(new Phrase(new Chunk(x.Order.OrderDate.ToString("dd/MM/yyyy"), normalFont))));
                    table.AddCell(new PdfPCell(new Phrase(new Chunk(x.Order.OrderId.ToString(), normalFont))));
                    table.AddCell(new PdfPCell(new Phrase(new Chunk(x.Item.QuantityOrdered.ToString(), normalFont))));
                    table.AddCell(new PdfPCell(new Phrase(new Chunk($"R{(x.Item.UnitPrice * x.Item.QuantityOrdered):F2}", normalFont))));

                    groupTotal += (decimal)(x.Item.UnitPrice * x.Item.QuantityOrdered);
                    groupItems += x.Item.QuantityOrdered;
                }

                document.Add(table);
                document.Add(new Paragraph(" ")); // Spacing

                // Subtotal
                var subtotal = new Paragraph($"Sub-total: Items: {groupItems}, Amount: R{groupTotal:F2}", normalFont);
                subtotal.Alignment = Element.ALIGN_LEFT;
                document.Add(subtotal);
                document.Add(new Paragraph(" ")); // Spacing
                document.Add(new Paragraph(" ")); // Spacing
            }

            // Grand total
            var grandTotalAmount = orders.Sum(o => o.OrderItems.Sum(oi => (decimal)(oi.UnitPrice * oi.QuantityOrdered)));
            var grandTotalItems = orders.Sum(o => o.OrderItems.Sum(oi => oi.QuantityOrdered));
            var grandTotalText = new Paragraph($"GRAND TOTAL: Items: {grandTotalItems}, Amount: R{grandTotalAmount:F2}", groupFont);
            grandTotalText.Alignment = Element.ALIGN_LEFT;
            document.Add(grandTotalText);
        }

        private void GenerateOrdersByDateReport(Document document, List<Order> orders)
        {
            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
            var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);

            // Create table
            PdfPTable table = new PdfPTable(4);
            table.WidthPercentage = 100;

                            // Table headers
                var header1 = new PdfPCell(new Phrase("Date", headerFont));
                header1.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header1);
                
                var header2 = new PdfPCell(new Phrase("Order #", headerFont));
                header2.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header2);
                
                var header3 = new PdfPCell(new Phrase("Items", headerFont));
                header3.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header3);
                
                var header4 = new PdfPCell(new Phrase("Total Amount", headerFont));
                header4.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header4);

            foreach (var order in orders.OrderBy(o => o.OrderDate))
            {
                table.AddCell(new PdfPCell(new Phrase(order.OrderDate.ToString("dd/MM/yyyy"), normalFont)));
                table.AddCell(new PdfPCell(new Phrase(order.OrderId.ToString(), normalFont)));
                table.AddCell(new PdfPCell(new Phrase(order.OrderItems.Sum(oi => oi.QuantityOrdered).ToString(), normalFont)));
                table.AddCell(new PdfPCell(new Phrase($"R{order.TotalAmount:F2}", normalFont)));
            }

            document.Add(table);
            document.Add(new Paragraph(" ")); // Spacing

            // Grand total
            var grandTotalAmount = orders.Sum(o => o.OrderItems.Sum(oi => oi.UnitPrice * oi.QuantityOrdered));
            var grandTotalText = new Paragraph($"GRAND TOTAL: Amount: R{grandTotalAmount:F2}", headerFont);
            grandTotalText.Alignment = Element.ALIGN_CENTER;
            document.Add(grandTotalText);
        }


        private byte[] GenerateCombinedReport(List<DispensedPrescription> prescriptions, List<Order> orders, DateTime startDate, DateTime endDate, string groupBy, Customer customer)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                Document document = new Document(PageSize.A4, 25, 25, 30, 30);
                PdfWriter writer = PdfWriter.GetInstance(document, ms);

                // Add header and footer
                writer.PageEvent = new CombinedReportPageEvent(customer.FullName);

                document.Open();

                // Title
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18);
                var title = new Paragraph("COMBINED CUSTOMER ACTIVITY REPORT", titleFont);
                title.Alignment = Element.ALIGN_CENTER;
                document.Add(title);

                document.Add(new Paragraph(" ")); // Spacing

                // Date range
                var dateFont = FontFactory.GetFont(FontFactory.HELVETICA, 12);
                var dateRange = new Paragraph($"Date Range: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}", dateFont);
                dateRange.Alignment = Element.ALIGN_CENTER;
                document.Add(dateRange);

                document.Add(new Paragraph(" ")); // Spacing

                // Customer info
                var customerInfo = new Paragraph($"Customer: {customer.FullName}", dateFont);
                document.Add(customerInfo);
                document.Add(new Paragraph(" ")); // Spacing

                // Summary
                var summaryFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12);
                var summary = new Paragraph("SUMMARY", summaryFont);
                document.Add(summary);
                document.Add(new Paragraph(" ")); // Spacing

                var summaryText = new Paragraph($"Total Prescriptions: {prescriptions.Count} | Total Orders: {orders.Count}", dateFont);
                document.Add(summaryText);
                document.Add(new Paragraph(" ")); // Spacing

                // Prescriptions section
                if (prescriptions.Any())
                {
                    var prescriptionHeader = new Paragraph("PRESCRIPTIONS", summaryFont);
                    document.Add(prescriptionHeader);
                    document.Add(new Paragraph(" ")); // Spacing

                    if (groupBy.ToLower() == "doctor")
                    {
                        GeneratePrescriptionsByDoctorReport(document, prescriptions);
                    }
                    else if (groupBy.ToLower() == "medication")
                    {
                        GeneratePrescriptionsByMedicationReport(document, prescriptions);
                    }
                }

                // Orders section
                if (orders.Any())
                {
                    var orderHeader = new Paragraph("ORDERS", summaryFont);
                    document.Add(orderHeader);
                    document.Add(new Paragraph(" ")); // Spacing

                    if (groupBy.ToLower() == "medication")
                    {
                        GenerateOrdersByMedicationReport(document, orders);
                    }
                    else if (groupBy.ToLower() == "doctor")
                    {
                        GenerateOrdersByDoctorReport(document, orders);
                    }
                    else
                    {
                        GenerateOrdersByDateReport(document, orders);
                    }
                }

                document.Close();
                return ms.ToArray();
            }
        }
    }

    // Page event classes for headers and footers
    public class PrescriptionReportPageEvent : PdfPageEventHelper
    {
        private string customerName;

        public PrescriptionReportPageEvent(string customerName)
        {
            this.customerName = customerName;
        }

        public override void OnEndPage(PdfWriter writer, Document document)
        {
            // Add page number at the bottom center, well separated from content
            PdfContentByte cb = writer.DirectContent;
            var baseFont = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, false);
            cb.BeginText();
            cb.SetFontAndSize(baseFont, 9);
            cb.SetColorFill(BaseColor.DARK_GRAY);
            float centerX = (document.PageSize.GetLeft(36) + document.PageSize.GetRight(36)) / 2f;
            float bottomY = document.PageSize.GetBottom(20);
            cb.ShowTextAligned(PdfContentByte.ALIGN_CENTER, $"Page {writer.PageNumber}", centerX, bottomY, 0);
            cb.EndText();
        }
    }

    public class OrderReportPageEvent : PdfPageEventHelper
    {
        private string customerName;

        public OrderReportPageEvent(string customerName)
        {
            this.customerName = customerName;
        }

        public override void OnEndPage(PdfWriter writer, Document document)
        {
            // Add page number at the bottom center, consistent with pharmacist reports
            PdfContentByte cb = writer.DirectContent;
            var baseFont = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, false);
            cb.BeginText();
            cb.SetFontAndSize(baseFont, 9);
            cb.SetColorFill(BaseColor.DARK_GRAY);
            float centerX = (document.PageSize.GetLeft(36) + document.PageSize.GetRight(36)) / 2f;
            float bottomY = document.PageSize.GetBottom(20);
            cb.ShowTextAligned(PdfContentByte.ALIGN_CENTER, $"Page {writer.PageNumber}", centerX, bottomY, 0);
            cb.EndText();
        }
    }

    public class CombinedReportPageEvent : PdfPageEventHelper
    {
        private string customerName;

        public CombinedReportPageEvent(string customerName)
        {
            this.customerName = customerName;
        }

        public override void OnEndPage(PdfWriter writer, Document document)
        {
            // Add page number at the bottom center
            PdfContentByte cb = writer.DirectContent;
            var baseFont = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, false);
            cb.BeginText();
            cb.SetFontAndSize(baseFont, 9);
            cb.SetColorFill(BaseColor.DARK_GRAY);
            float centerX = (document.PageSize.GetLeft(36) + document.PageSize.GetRight(36)) / 2f;
            float bottomY = document.PageSize.GetBottom(20);
            cb.ShowTextAligned(PdfContentByte.ALIGN_CENTER, $"Page {writer.PageNumber}", centerX, bottomY, 0);
            cb.EndText();
        }
    }
}
