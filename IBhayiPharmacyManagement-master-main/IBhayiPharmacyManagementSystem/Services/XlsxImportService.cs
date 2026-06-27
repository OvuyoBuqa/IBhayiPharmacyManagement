using ClosedXML.Excel;
using IBhayiPharmacyManagementSystem.ViewModels;
using System.Text.RegularExpressions;

namespace IBhayiPharmacyManagementSystem.Services
{
    public class XlsxImportService
    {
        private readonly ILogger<XlsxImportService> _logger;

        public XlsxImportService(ILogger<XlsxImportService> logger)
        {
            _logger = logger;
        }

        public Task<XlsxParsedData> ParseXlsxAsync(IFormFile xlsxFile)
        {
            try
            {
                using var stream = xlsxFile.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                
                var result = new XlsxParsedData();
                
                // Try to detect worksheets - if multiple sheets exist, parse each
                if (workbook.Worksheets.Count > 1)
                {
                    foreach (var worksheet in workbook.Worksheets)
                    {
                        ParseWorksheet(worksheet, result);
                    }
                }
                else
                {
                    // Single worksheet - try to detect sections
                    var worksheet = workbook.Worksheet(1);
                    ParseWorksheetWithSections(worksheet, result);
                }
                
                _logger.LogInformation("Parsed XLSX: {CustomerCount} customers, {StockOrderCount} stock orders, {PrescriptionCount} prescriptions, {OrderCount} orders",
                    result.Customers.Count, result.StockOrders.Count, result.Prescriptions.Count, result.Orders.Count);
                
                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing XLSX file");
                throw new InvalidOperationException("Failed to parse XLSX file", ex);
            }
        }

        private void ParseWorksheet(IXLWorksheet worksheet, XlsxParsedData result)
        {
            _logger.LogInformation("Parsing worksheet: {Name}", worksheet.Name);
            
            // Try to determine the type based on headers
            var firstRow = worksheet.FirstRow();
            var headers = firstRow.CellsUsed().Select(c => c.GetValue<string>().Trim().ToLowerInvariant()).ToList();
            
            if (headers.Contains("full name") || headers.Contains("id number"))
            {
                ParseCustomers(worksheet, result.Customers);
            }
            else if (headers.Contains("supplier") && headers.Contains("medication") && headers.Contains("quantity") && headers.Contains("order status"))
            {
                ParseStockOrders(worksheet, result.StockOrders);
            }
            else if (headers.Contains("customer") && headers.Contains("doctor") && headers.Contains("medication") && headers.Contains("repeats"))
            {
                ParsePrescriptions(worksheet, result.Prescriptions);
            }
            else if (headers.Contains("customer") && headers.Contains("medication(s)") && headers.Contains("order status"))
            {
                ParseOrders(worksheet, result.Orders);
            }
        }

        private void ParseWorksheetWithSections(IXLWorksheet worksheet, XlsxParsedData result)
        {
            var rows = worksheet.RowsUsed().ToList();
            
            // Scan for different section headers throughout the worksheet
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var cellValues = row.Cells().Select(c => c.GetValue<string>().Trim().ToLowerInvariant()).ToList();
                
                // Check for Customers section
                if (cellValues.Contains("full name") && cellValues.Contains("id number"))
                {
                    _logger.LogInformation("Found Customers section at row {Row}", i + 1);
                    ParseCustomersFromRow(worksheet, result.Customers, i);
                }
                // Check for Stock Orders section
                else if (cellValues.Contains("supplier") && cellValues.Contains("medication") && cellValues.Contains("quantity"))
                {
                    _logger.LogInformation("Found Stock Orders section at row {Row}", i + 1);
                    ParseStockOrdersFromRow(worksheet, result.StockOrders, i);
                }
                // Check for Prescriptions section
                else if (cellValues.Contains("customer") && cellValues.Contains("doctor") && 
                         cellValues.Contains("medication") && cellValues.Contains("quantity") && 
                         cellValues.Contains("instructions"))
                {
                    _logger.LogInformation("Found Prescriptions section at row {Row}", i + 1);
                    ParsePrescriptionsFromRow(worksheet, result.Prescriptions, i);
                }
                // Check for Customer Orders section
                else if (cellValues.Contains("customer") && (cellValues.Contains("medication(s)") || cellValues.Contains("medications")) && 
                         cellValues.Contains("order status"))
                {
                    _logger.LogInformation("Found Customer Orders section at row {Row}", i + 1);
                    ParseOrdersFromRow(worksheet, result.Orders, i);
                }
            }
        }

        private void ParseCustomers(IXLWorksheet worksheet, List<CustomerData> customers)
        {
            var rows = worksheet.RowsUsed().ToList();
            var headerRowIndex = -1;
            
            // Find header row
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var cellValues = row.Cells().Select(c => c.GetValue<string>().Trim().ToLowerInvariant()).ToList();
                
                if (cellValues.Contains("full name") && cellValues.Contains("id number"))
                {
                    headerRowIndex = i;
                    break;
                }
            }
            
            if (headerRowIndex == -1) return;
            
            var headerRow = rows[headerRowIndex];
                var columnMap = new Dictionary<string, int>();
                foreach (var cell in headerRow.Cells())
                {
                var headerValue = cell.GetValue<string>().Trim().ToLowerInvariant();
                columnMap[headerValue] = cell.Address.ColumnNumber;
                }
                
            _logger.LogInformation("Found customer columns: {Columns}", string.Join(", ", columnMap.Keys));
                
                // Process data rows
            for (int i = headerRowIndex + 1; i < rows.Count; i++)
                {
                    try
                    {
                    var row = rows[i];
                        var customer = new CustomerData
                        {
                        FullName = GetCellValue(row, "full name", columnMap).Trim(),
                        IDNumber = GetCellValue(row, "id number", columnMap).Trim(),
                        PhoneNumber = GetCellValue(row, "phone number", columnMap).Trim(),
                        Email = GetCellValue(row, "email", columnMap).Trim(),
                        Address = GetCellValue(row, "address (if relevant)", columnMap).Trim(),
                        Allergies = GetCellValue(row, "allergies", columnMap).Trim()
                    };
                    
                    if (string.IsNullOrWhiteSpace(customer.FullName) || string.IsNullOrWhiteSpace(customer.IDNumber))
                        continue;
                    
                        ParseFullName(customer);
                        ParseAddress(customer);
                        ParseAllergies(customer);
                        
                    customers.Add(customer);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing customer row {RowNumber}", i + 1);
                }
            }
        }

        private void ParseStockOrders(IXLWorksheet worksheet, List<StockOrderData> stockOrders)
        {
            var rows = worksheet.RowsUsed().ToList();
            var headerRowIndex = -1;
            
            // Find header row
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var cellValues = row.Cells().Select(c => c.GetValue<string>().Trim().ToLowerInvariant()).ToList();
                
                if (cellValues.Contains("date") && cellValues.Contains("supplier") && cellValues.Contains("medication"))
                {
                    headerRowIndex = i;
                    break;
                }
            }
            
            if (headerRowIndex == -1) return;
            
            var headerRow = rows[headerRowIndex];
            var columnMap = new Dictionary<string, int>();
            foreach (var cell in headerRow.Cells())
            {
                var headerValue = cell.GetValue<string>().Trim().ToLowerInvariant();
                columnMap[headerValue] = cell.Address.ColumnNumber;
            }
            
            // Track current order details for multi-row orders
            var currentDate = DateTime.Now;
            var currentSupplier = "";
            var currentOrderStatus = "";
            
            for (int i = headerRowIndex + 1; i < rows.Count; i++)
            {
                try
                {
                    var row = rows[i];
                    
                    // Check if this row has a new date (new order starts)
                    var dateValue = GetCellValue(row, "date", columnMap).Trim();
                    if (!string.IsNullOrWhiteSpace(dateValue) && dateValue.ToLower() != "(current)")
                    {
                        if (DateTime.TryParse(dateValue, out var newDate))
                        {
                            currentDate = newDate;
                        }
                    }
                    else if (string.IsNullOrWhiteSpace(dateValue))
                    {
                        // Keep using the current date if cell is empty (part of multi-row order)
                    }
                    
                    // Check if this row has a supplier (new order starts)
                    var supplierValue = GetCellValue(row, "supplier", columnMap).Trim();
                    if (!string.IsNullOrWhiteSpace(supplierValue))
                    {
                        currentSupplier = supplierValue;
                    }
                    
                    // Check if this row has an order status (new order starts)
                    var statusValue = GetCellValue(row, "order status", columnMap).Trim();
                    if (!string.IsNullOrWhiteSpace(statusValue))
                    {
                        currentOrderStatus = statusValue;
                    }
                    
                    // Get medication from this row
                    var medication = GetCellValue(row, "medication", columnMap).Trim();
                    var quantity = int.TryParse(GetCellValue(row, "quantity", columnMap), out var qty) ? qty : 0;
                    
                    // Only add if we have a supplier and medication (skip rows that are just continuation of previous order)
                    if (!string.IsNullOrWhiteSpace(currentSupplier) && !string.IsNullOrWhiteSpace(medication))
                    {
                        var stockOrder = new StockOrderData
                        {
                            Date = currentDate,
                            Supplier = currentSupplier,
                            Medication = medication,
                            Quantity = quantity,
                            OrderStatus = currentOrderStatus
                        };
                        
                        stockOrders.Add(stockOrder);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing stock order row {RowNumber}", i + 1);
                }
            }
        }

        private void ParsePrescriptions(IXLWorksheet worksheet, List<PrescriptionData> prescriptions)
        {
            var rows = worksheet.RowsUsed().ToList();
            var headerRowIndex = -1;
            
            // Find header row
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var cellValues = row.Cells().Select(c => c.GetValue<string>().Trim().ToLowerInvariant()).ToList();
                
                if (cellValues.Contains("date") && cellValues.Contains("customer") && cellValues.Contains("doctor") && cellValues.Contains("medication"))
                {
                    headerRowIndex = i;
                    break;
                }
            }
            
            if (headerRowIndex == -1) return;
            
            var headerRow = rows[headerRowIndex];
            var columnMap = new Dictionary<string, int>();
            foreach (var cell in headerRow.Cells())
            {
                var headerValue = cell.GetValue<string>().Trim().ToLowerInvariant();
                columnMap[headerValue] = cell.Address.ColumnNumber;
            }
            
            // Track current prescription details for multi-medication prescriptions
            var currentDate = DateTime.Now;
            var currentCustomer = "";
            var currentDoctor = "";
            
            for (int i = headerRowIndex + 1; i < rows.Count; i++)
            {
                try
                {
                    var row = rows[i];
                    
                    // Check if this row has a date (new prescription) - update context
                    var dateValue = GetCellValue(row, "date", columnMap).Trim();
                    if (!string.IsNullOrWhiteSpace(dateValue) && DateTime.TryParse(dateValue, out var prescriptionDate))
                    {
                        currentDate = prescriptionDate;
                        currentCustomer = GetCellValue(row, "customer", columnMap).Trim();
                        currentDoctor = GetCellValue(row, "doctor", columnMap).Trim();
                    }
                    
                    // Get customer - use current context if empty
                    var customerValue = GetCellValue(row, "customer", columnMap).Trim();
                    if (!string.IsNullOrWhiteSpace(customerValue))
                        currentCustomer = customerValue;
                    
                    // Get doctor - use current context if empty
                    var doctorValue = GetCellValue(row, "doctor", columnMap).Trim();
                    if (!string.IsNullOrWhiteSpace(doctorValue))
                        currentDoctor = doctorValue;
                    
                    // Only process if we have all required fields
                    if (string.IsNullOrWhiteSpace(currentCustomer))
                        continue;
                    
                    var prescription = new PrescriptionData
                    {
                        Date = currentDate,
                        Customer = currentCustomer,
                        Doctor = currentDoctor,
                        Medication = GetCellValue(row, "medication", columnMap).Trim(),
                        Quantity = int.TryParse(GetCellValue(row, "quantity", columnMap), out var qty) ? qty : 0,
                        Instructions = GetCellValue(row, "instructions", columnMap).Trim(),
                        Repeats = int.TryParse(GetCellValue(row, "repeats", columnMap), out var repeats) ? repeats : 0,
                        Frequency = GetCellValue(row, "frequency", columnMap).Trim()
                    };
                    
                    if (!string.IsNullOrWhiteSpace(prescription.Medication) && prescription.Quantity > 0)
                    {
                        prescriptions.Add(prescription);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing prescription row {RowNumber}", i + 1);
                }
            }
        }

        private void ParseOrders(IXLWorksheet worksheet, List<OrderData> orders)
        {
            var rows = worksheet.RowsUsed().ToList();
            var headerRowIndex = -1;
            
            // Find header row
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var cellValues = row.Cells().Select(c => c.GetValue<string>().Trim().ToLowerInvariant()).ToList();
                
                if (cellValues.Contains("order date") && cellValues.Contains("customer") && cellValues.Contains("medication(s)"))
                {
                    headerRowIndex = i;
                    break;
                }
            }
            
            if (headerRowIndex == -1) return;
            
            var headerRow = rows[headerRowIndex];
            var columnMap = new Dictionary<string, int>();
            foreach (var cell in headerRow.Cells())
            {
                var headerValue = cell.GetValue<string>().Trim().ToLowerInvariant();
                columnMap[headerValue] = cell.Address.ColumnNumber;
            }
            
            for (int i = headerRowIndex + 1; i < rows.Count; i++)
            {
                try
                {
                    var row = rows[i];
                    var orderDateValue = GetCellValue(row, "order date", columnMap).Trim();
                    DateTime? orderDate = string.IsNullOrWhiteSpace(orderDateValue) ? null : DateTime.TryParse(orderDateValue, out var d) ? d : null;
                    
                    var medications = GetCellValue(row, "medication(s)", columnMap).Trim();
                    
                    var order = new OrderData
                    {
                        OrderDate = orderDate,
                        Customer = GetCellValue(row, "customer", columnMap).Trim(),
                        Medications = medications,
                        OrderStatus = GetCellValue(row, "order status", columnMap).Trim()
                    };
                    
                    // Parse medications
                    order.MedicationList = medications
                        .Split(new[] { ',', ';', '&' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(m => m.Trim())
                        .Where(m => !string.IsNullOrWhiteSpace(m))
                        .ToList();
                    
                    if (!string.IsNullOrWhiteSpace(order.Customer) && order.MedicationList.Any())
                    {
                        orders.Add(order);
                    }
            }
            catch (Exception ex)
            {
                    _logger.LogError(ex, "Error parsing order row {RowNumber}", i + 1);
                }
            }
        }
        
        private string GetCellValue(IXLRow row, string columnHeader, Dictionary<string, int> columnMap)
        {
            var columnKey = columnHeader.ToLowerInvariant();
            if (columnMap.TryGetValue(columnKey, out int columnNumber))
            {
                return row.Cell(columnNumber).GetValue<string>();
            }
            return string.Empty;
        }
        
        private void ParseFullName(CustomerData customer)
        {
            if (string.IsNullOrWhiteSpace(customer.FullName))
                return;
                
            var parts = customer.FullName.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length >= 2)
            {
                customer.Surname = parts[parts.Length - 1];
                customer.FirstName = string.Join(" ", parts.Take(parts.Length - 1));
            }
            else if (parts.Length == 1)
            {
                customer.FirstName = parts[0];
                customer.Surname = parts[0];
            }
        }
        
        private void ParseAddress(CustomerData customer)
        {
            if (string.IsNullOrWhiteSpace(customer.Address))
                return;
            
            var parts = customer.Address.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim()).ToArray();
            
            if (parts.Length >= 3)
            {
                customer.Street = string.Join(", ", parts.Take(parts.Length - 2));
                customer.Suburb = parts[parts.Length - 2];
                customer.City = parts[parts.Length - 1];
            }
            else if (parts.Length == 2)
            {
                customer.Street = parts[0];
                customer.City = parts[1];
            }
            else if (parts.Length == 1)
            {
                customer.Street = parts[0];
            }
        }
        
        private void ParseAllergies(CustomerData customer)
        {
            if (string.IsNullOrWhiteSpace(customer.Allergies))
                return;
                
            customer.AllergyList = customer.Allergies
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(a => a.Trim())
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .ToList();
        }
        
        // Helper methods to parse from specific row numbers
        private void ParseCustomersFromRow(IXLWorksheet worksheet, List<CustomerData> customers, int headerRowIndex)
        {
            var rows = worksheet.RowsUsed().ToList();
            if (headerRowIndex >= rows.Count) return;
            
            var headerRow = rows[headerRowIndex];
            var columnMap = new Dictionary<string, int>();
            foreach (var cell in headerRow.Cells())
            {
                var headerValue = cell.GetValue<string>().Trim().ToLowerInvariant();
                columnMap[headerValue] = cell.Address.ColumnNumber;
            }
            
            // Process data rows until we hit another section or empty row
            for (int i = headerRowIndex + 1; i < rows.Count; i++)
            {
                var row = rows[i];
                var firstCell = row.Cell(1).GetValue<string>().Trim();
                
                // Stop if we hit another section header
                if (!string.IsNullOrWhiteSpace(firstCell) && 
                    (firstCell.Equals("date", StringComparison.OrdinalIgnoreCase) ||
                     firstCell.Equals("order date", StringComparison.OrdinalIgnoreCase)))
                    break;
                
                try
                {
                    var customer = new CustomerData
                    {
                        FullName = GetCellValue(row, "full name", columnMap).Trim(),
                        IDNumber = GetCellValue(row, "id number", columnMap).Trim(),
                        PhoneNumber = GetCellValue(row, "phone number", columnMap).Trim(),
                        Email = GetCellValue(row, "email", columnMap).Trim(),
                        Address = GetCellValue(row, "address (if relevant)", columnMap).Trim(),
                        Allergies = GetCellValue(row, "allergies", columnMap).Trim()
                    };
                    
                    if (string.IsNullOrWhiteSpace(customer.FullName) || string.IsNullOrWhiteSpace(customer.IDNumber))
                        continue;
                    
                    ParseFullName(customer);
                    ParseAddress(customer);
                    ParseAllergies(customer);
                    
                    customers.Add(customer);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing customer row {RowNumber}", i + 1);
                }
            }
        }
        
        private void ParseStockOrdersFromRow(IXLWorksheet worksheet, List<StockOrderData> stockOrders, int headerRowIndex)
        {
            var rows = worksheet.RowsUsed().ToList();
            if (headerRowIndex >= rows.Count) return;
            
            var headerRow = rows[headerRowIndex];
            var columnMap = new Dictionary<string, int>();
            foreach (var cell in headerRow.Cells())
            {
                var headerValue = cell.GetValue<string>().Trim().ToLowerInvariant();
                columnMap[headerValue] = cell.Address.ColumnNumber;
            }
            
            // Track current order details for multi-row orders
            var currentDate = DateTime.Now;
            var currentSupplier = "";
            var currentOrderStatus = "";
            
            for (int i = headerRowIndex + 1; i < rows.Count; i++)
            {
                var row = rows[i];
                var firstCell = row.Cell(1).GetValue<string>().Trim();
                
                // Stop if we hit another section header
                if (!string.IsNullOrWhiteSpace(firstCell) &&
                    (firstCell.Equals("full name", StringComparison.OrdinalIgnoreCase) ||
                     (firstCell.StartsWith("date", StringComparison.OrdinalIgnoreCase) && columnMap.ContainsKey("customer"))))
                    break;
                
                try
                {
                    // Check if this row has a new date (new order starts)
                    var dateValue = GetCellValue(row, "date", columnMap).Trim();
                    if (!string.IsNullOrWhiteSpace(dateValue) && dateValue.ToLower() != "(current)")
                    {
                        if (DateTime.TryParse(dateValue, out var newDate))
                        {
                            currentDate = newDate;
                        }
                    }
                    else if (dateValue.ToLower() == "(current)" || string.IsNullOrWhiteSpace(dateValue))
                    {
                        // Keep using the current date if cell is (current) or empty (part of multi-row order)
                    }
                    
                    // Check if this row has a supplier (new order starts)
                    var supplierValue = GetCellValue(row, "supplier", columnMap).Trim();
                    if (!string.IsNullOrWhiteSpace(supplierValue))
                    {
                        currentSupplier = supplierValue;
                    }
                    
                    // Check if this row has an order status (new order starts)
                    var statusValue = GetCellValue(row, "order status", columnMap).Trim();
                    if (!string.IsNullOrWhiteSpace(statusValue))
                    {
                        currentOrderStatus = statusValue;
                    }
                    
                    // Get medication from this row
                    var medication = GetCellValue(row, "medication", columnMap).Trim();
                    var quantity = int.TryParse(GetCellValue(row, "quantity", columnMap), out var qty) ? qty : 0;
                    
                    // Only add if we have a supplier and medication
                    if (!string.IsNullOrWhiteSpace(currentSupplier) && !string.IsNullOrWhiteSpace(medication))
                    {
                        var stockOrder = new StockOrderData
                        {
                            Date = currentDate,
                            Supplier = currentSupplier,
                            Medication = medication,
                            Quantity = quantity,
                            OrderStatus = currentOrderStatus
                        };
                        
                        stockOrders.Add(stockOrder);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing stock order row {RowNumber}", i + 1);
                }
            }
        }
        
        private void ParsePrescriptionsFromRow(IXLWorksheet worksheet, List<PrescriptionData> prescriptions, int headerRowIndex)
        {
            var rows = worksheet.RowsUsed().ToList();
            if (headerRowIndex >= rows.Count) return;
            
            var headerRow = rows[headerRowIndex];
            var columnMap = new Dictionary<string, int>();
            foreach (var cell in headerRow.Cells())
            {
                var headerValue = cell.GetValue<string>().Trim().ToLowerInvariant();
                columnMap[headerValue] = cell.Address.ColumnNumber;
            }
            
            // Track current prescription details for multi-medication prescriptions
            var currentDate = DateTime.Now;
            var currentCustomer = "";
            var currentDoctor = "";
            
            for (int i = headerRowIndex + 1; i < rows.Count; i++)
            {
                var row = rows[i];
                var firstCell = row.Cell(1).GetValue<string>().Trim();
                
                // Stop if we hit another section header
                if (!string.IsNullOrWhiteSpace(firstCell) &&
                    (firstCell.Equals("full name", StringComparison.OrdinalIgnoreCase) ||
                     firstCell.Equals("order date", StringComparison.OrdinalIgnoreCase)))
                    break;
                
                try
                {
                    // Check if this row has a date (new prescription) - update context
                    var dateValue = GetCellValue(row, "date", columnMap).Trim();
                    if (!string.IsNullOrWhiteSpace(dateValue) && DateTime.TryParse(dateValue, out var prescriptionDate))
                    {
                        currentDate = prescriptionDate;
                        currentCustomer = GetCellValue(row, "customer", columnMap).Trim();
                        currentDoctor = GetCellValue(row, "doctor", columnMap).Trim();
                    }
                    
                    // Get customer - use current context if empty
                    var customerValue = GetCellValue(row, "customer", columnMap).Trim();
                    if (!string.IsNullOrWhiteSpace(customerValue))
                        currentCustomer = customerValue;
                    
                    // Get doctor - use current context if empty
                    var doctorValue = GetCellValue(row, "doctor", columnMap).Trim();
                    if (!string.IsNullOrWhiteSpace(doctorValue))
                        currentDoctor = doctorValue;
                    
                    // Only process if we have all required fields
                    if (string.IsNullOrWhiteSpace(currentCustomer))
                        continue;
                    
                    var prescription = new PrescriptionData
                    {
                        Date = currentDate,
                        Customer = currentCustomer,
                        Doctor = currentDoctor,
                        Medication = GetCellValue(row, "medication", columnMap).Trim(),
                        Quantity = int.TryParse(GetCellValue(row, "quantity", columnMap), out var qty) ? qty : 0,
                        Instructions = GetCellValue(row, "instructions", columnMap).Trim(),
                        Repeats = int.TryParse(GetCellValue(row, "repeats", columnMap), out var repeats) ? repeats : 0,
                        Frequency = GetCellValue(row, "frequency", columnMap).Trim()
                    };
                    
                    if (!string.IsNullOrWhiteSpace(prescription.Medication) && prescription.Quantity > 0)
                    {
                        prescriptions.Add(prescription);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing prescription row {RowNumber}", i + 1);
                }
            }
        }
        
        private void ParseOrdersFromRow(IXLWorksheet worksheet, List<OrderData> orders, int headerRowIndex)
        {
            var rows = worksheet.RowsUsed().ToList();
            if (headerRowIndex >= rows.Count) return;
            
            var headerRow = rows[headerRowIndex];
            var columnMap = new Dictionary<string, int>();
            foreach (var cell in headerRow.Cells())
            {
                var headerValue = cell.GetValue<string>().Trim().ToLowerInvariant();
                columnMap[headerValue] = cell.Address.ColumnNumber;
            }
            
            for (int i = headerRowIndex + 1; i < rows.Count; i++)
            {
                var row = rows[i];
                var firstCell = row.Cell(1).GetValue<string>().Trim();
                
                // Stop if we hit empty row or another section
                if (string.IsNullOrWhiteSpace(firstCell) ||
                    firstCell.Equals("full name", StringComparison.OrdinalIgnoreCase))
                    break;
                
                try
                {
                    var orderDateValue = GetCellValue(row, "order date", columnMap).Trim();
                    DateTime? orderDate = string.IsNullOrWhiteSpace(orderDateValue) ? null : DateTime.TryParse(orderDateValue, out var d) ? d : null;
                    
                    var medications = GetCellValue(row, "medication(s)", columnMap).Trim();
                    if (string.IsNullOrWhiteSpace(medications))
                        medications = GetCellValue(row, "medications", columnMap).Trim();
                    
                    var order = new OrderData
                    {
                        OrderDate = orderDate,
                        Customer = GetCellValue(row, "customer", columnMap).Trim(),
                        Medications = medications,
                        OrderStatus = GetCellValue(row, "order status", columnMap).Trim()
                    };
                    
                    order.MedicationList = medications
                        .Split(new[] { ',', ';', '&' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(m => m.Trim())
                        .Where(m => !string.IsNullOrWhiteSpace(m))
                        .ToList();
                    
                    if (!string.IsNullOrWhiteSpace(order.Customer) && order.MedicationList.Any())
                    {
                        orders.Add(order);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing order row {RowNumber}", i + 1);
                }
            }
        }
    }
}
