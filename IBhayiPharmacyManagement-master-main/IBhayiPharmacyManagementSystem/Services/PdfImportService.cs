using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using IBhayiPharmacyManagementSystem.ViewModels;

namespace IBhayiPharmacyManagementSystem.Services
{
    public class PdfImportService
    {
        private readonly ILogger<PdfImportService> _logger;

        public PdfImportService(ILogger<PdfImportService> logger)
        {
            _logger = logger;
        }

        public Task<PdfParsedData> ParsePdfAsync(IFormFile pdfFile)
        {
            try
            {
                using var stream = pdfFile.OpenReadStream();
                using var document = PdfDocument.Open(stream);
                
                var fullText = new StringBuilder();
                
                // Extract text from all pages
                foreach (var page in document.GetPages())
                {
                    fullText.AppendLine(page.Text);
                }

                var text = fullText.ToString();
                _logger.LogInformation("Extracted PDF text length: {Length}", text.Length);

                return Task.FromResult(ParseTextToStructuredData(text));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing PDF file");
                throw new InvalidOperationException("Failed to parse PDF file", ex);
            }
        }

        private PdfParsedData ParseTextToStructuredData(string text)
        {
            var parsedData = new PdfParsedData();
            _logger.LogInformation("Starting to parse PDF text, length: {Length}", text.Length);
            
            // Log first 500 characters to see the structure
            _logger.LogInformation("First 500 chars: {Text}", text.Length > 500 ? text.Substring(0, 500) : text);
            
            // Use ExtractSection to get isolated text blocks
            var activeIngredientsSection = ExtractSection(text, "ACTIVE INGREDIENTS", "DOSAGE FORMS");
            var dosageFormsSection = ExtractSection(text, "DOSAGE FORMS", "SUPPLIERS");
            var suppliersSection = ExtractSection(text, "SUPPLIERS", "MEDICATION");
            var medicationsSection = ExtractSection(text, "MEDICATION", "DOCTORS");
            var doctorsSection = ExtractSection(text, "DOCTORS", "PHARMACY");
            var managersSection = ExtractSection(text, "PHARMACY MANAGER", "PHARMACISTS");
            var pharmacistsSection = ExtractSection(text, "PHARMACISTS", "PASSWORDS");
            
            // Parse each section with multiline regex
            ParseActiveIngredientsSection(activeIngredientsSection, parsedData);
            ParseDosageFormsSection(dosageFormsSection, parsedData);
            ParseSuppliersSection(suppliersSection, parsedData);
            ParseMedicationsSection(medicationsSection, parsedData);
            ParseDoctorsSection(doctorsSection, parsedData);
            ParseManagersSection(managersSection, parsedData);
            ParsePharmacistsSection(pharmacistsSection, parsedData);
            
            _logger.LogInformation("Final counts - Active Ingredients: {AI}, Dosage Forms: {DF}, Suppliers: {S}, Medications: {M}, Doctors: {D}, Managers: {PM}, Pharmacists: {P}",
                parsedData.ActiveIngredients.Count, parsedData.DosageForms.Count, parsedData.Suppliers.Count, 
                parsedData.Medications.Count, parsedData.Doctors.Count, parsedData.PharmacyManagers.Count, parsedData.Pharmacists.Count);

            return parsedData;
        }
        
        private void ParseActiveIngredientsSection(string sectionText, PdfParsedData parsedData)
        {
            if (string.IsNullOrEmpty(sectionText))
            {
                _logger.LogWarning("Active Ingredients section is empty");
                return;
            }
            
            // Match all patterns like "1 Word" where Word starts with capital letter and is alphabetic
            var matches = Regex.Matches(sectionText, @"\d+\s+([A-Z][a-zA-Z]+)", RegexOptions.Multiline);
            _logger.LogInformation("Found {Count} active ingredient matches in section", matches.Count);
            
            foreach (Match match in matches)
            {
                var ingredient = match.Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(ingredient) && ingredient.Length > 1)
                {
                    parsedData.ActiveIngredients.Add(ingredient);
                    _logger.LogInformation("Added active ingredient: {Ingredient}", ingredient);
                }
            }
        }
        
        private void ParseDosageFormsSection(string sectionText, PdfParsedData parsedData)
        {
            if (string.IsNullOrEmpty(sectionText))
            {
                _logger.LogWarning("Dosage Forms section is empty");
                return;
            }
            
            // Match all patterns like "1 Word" where Word starts with capital letter and is alphabetic
            var matches = Regex.Matches(sectionText, @"\d+\s+([A-Z][a-zA-Z]+)", RegexOptions.Multiline);
            _logger.LogInformation("Found {Count} dosage form matches in section", matches.Count);
            
            foreach (Match match in matches)
            {
                var dosageForm = match.Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(dosageForm) && dosageForm.Length > 1)
                {
                    parsedData.DosageForms.Add(dosageForm);
                    _logger.LogInformation("Added dosage form: {DosageForm}", dosageForm);
                }
            }
        }
        
        private void ParseSuppliersSection(string sectionText, PdfParsedData parsedData)
        {
            if (string.IsNullOrEmpty(sectionText))
            {
                _logger.LogWarning("Suppliers section is empty");
                return;
            }
            
            _logger.LogInformation("Suppliers section content: {Content}", sectionText);
            
            // Split by lines and process each line that contains an email
            var lines = sectionText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                
                // Skip headers and page numbers
                if (string.IsNullOrEmpty(trimmed) || 
                    trimmed.Contains("Page ") || 
                    trimmed.Contains("NAME") || 
                    trimmed.Contains("CONTACT") || 
                    trimmed.Contains("E-MAIL") || 
                    trimmed.Contains("ADDRESS") ||
                    trimmed.Contains("SUPPLIERS"))
                    continue;
                
                if (trimmed.Contains("@"))
                {
                    _logger.LogInformation("Processing supplier line: {Line}", trimmed);
                    
                    // Use regex to find email
                    var emailMatch = Regex.Match(trimmed, @"([a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,})");
                    if (!emailMatch.Success)
                        continue;
                    
                    var email = emailMatch.Value;
                    var parts = trimmed.Replace(email, "EMAIL_PLACEHOLDER").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    // Find where the email placeholder is
                    var emailIndex = -1;
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (parts[i] == "EMAIL_PLACEHOLDER")
                        {
                            emailIndex = i;
                            break;
                        }
                    }
                    
                    if (emailIndex > 2 && parts.Length >= 3)
                    {
                        // Extract company name (all parts before the contact name)
                        // Contact name is typically the last 2 parts before email (name and surname)
                        // So everything before that is the company name
                        var companyParts = new List<string>();
                        
                        // Assume last 2 words before email are contact name and surname
                        var contactStartIndex = emailIndex - 2; // Start with 2 words
                        
                        // Collect all words before contact name as company name
                        for (int i = 0; i < contactStartIndex; i++)
                        {
                            if (parts[i] != "EMAIL_PLACEHOLDER" && !string.IsNullOrWhiteSpace(parts[i]))
                                companyParts.Add(parts[i]);
                        }
                        
                        var companyName = companyParts.Count > 0 ? string.Join(" ", companyParts) : parts[0];
                        
                        // Get contact person: extract name and surname separately
                        string contactFirstName = "";
                        string contactSurname = "";
                        
                        if (emailIndex >= 2 && contactStartIndex >= 0 && parts.Length > contactStartIndex + 1)
                        {
                            // Extract name and surname separately
                            contactFirstName = parts[contactStartIndex];
                            contactSurname = parts[contactStartIndex + 1];
                        }
                        else if (contactStartIndex >= 0 && parts.Length > contactStartIndex)
                        {
                            // Only one word available, treat as name
                            contactFirstName = parts[contactStartIndex];
                            contactSurname = "";
                        }
                        
                        if (companyName != "NAME" && companyName != "CONTACT" && companyName != "E-MAIL" && companyName != "ADDRESS")
                        {
                            var supplier = new SupplierData
                            {
                                Name = companyName,
                                ContactFirstName = contactFirstName,
                                ContactSurname = contactSurname,
                                Email = email,
                                Phone = ""
                            };
                            parsedData.Suppliers.Add(supplier);
                            _logger.LogInformation("Added supplier: {Name}, Contact: {FirstName} {Surname}, {Email}", 
                                supplier.Name, supplier.ContactFirstName, supplier.ContactSurname, supplier.Email);
                        }
                    }
                    else if (emailIndex > 0)
                    {
                        // Fallback: if we can't parse properly, just take first word as company
                        if (parts.Length >= 3)
                        {
                            var companyName = parts[0];
                            // Try to get the last 2 words before email as contact name and surname
                            string contactFirstName = "";
                            string contactSurname = "";
                            
                            if (emailIndex > 2)
                            {
                                contactFirstName = parts[emailIndex - 2];
                                contactSurname = parts[emailIndex - 1];
                            }
                            else if (emailIndex > 1)
                            {
                                contactFirstName = parts[emailIndex - 1];
                                contactSurname = "";
                            }
                            else if (parts.Length > 1)
                            {
                                contactFirstName = parts[1];
                                contactSurname = "";
                            }
                            
                            var supplier = new SupplierData
                            {
                                Name = companyName,
                                ContactFirstName = contactFirstName,
                                ContactSurname = contactSurname,
                                Email = email,
                                Phone = ""
                            };
                            parsedData.Suppliers.Add(supplier);
                            _logger.LogInformation("Added supplier: {Name}, Contact: {FirstName} {Surname}, {Email}", 
                                supplier.Name, supplier.ContactFirstName, supplier.ContactSurname, supplier.Email);
                        }
                    }
                }
            }
            
            // Note: Suppliers should now be found by the line-by-line parsing above
            // If no suppliers found, the parsing logic above handles all cases
        }
        
        private void ParseMedicationsSection(string sectionText, PdfParsedData parsedData)
        {
            if (string.IsNullOrEmpty(sectionText))
            {
                _logger.LogWarning("Medications section is empty");
                return;
            }
            
            // Split into individual medication blocks
            var medicationBlocks = Regex.Split(sectionText, @"\d+\)\s+Medication Name:");
            _logger.LogInformation("Found {Count} medication blocks", medicationBlocks.Length - 1);
            
            foreach (var block in medicationBlocks.Skip(1)) // Skip first empty match
            {
                _logger.LogInformation("Processing medication block (first 200 chars): {Block}", 
                    block.Length > 200 ? block.Substring(0, 200) : block);
                
                var medication = new MedicationData
                {
                    ActiveIngredients = new List<ActiveIngredientData>()
                };
                
                // Extract name - match everything after the number and before the newline
                var nameMatch = Regex.Match(block, @"^(\s*Medication Name:\s*)?([^\r\n]+?)(?:\s+Schedule:|\r|\n|$)", RegexOptions.IgnoreCase);
                if (nameMatch.Success && nameMatch.Groups.Count >= 3)
                {
                    medication.Name = nameMatch.Groups[2].Value.Trim();
                }
                
                // If still empty, try to get the actual medication name
                if (string.IsNullOrEmpty(medication.Name))
                {
                    var nameLine = block.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .FirstOrDefault(l => !l.Contains("Schedule") && !l.Contains("Dosage Form") && !l.Contains("Supplier") && !l.Contains("Active Ingredient"));
                    if (!string.IsNullOrEmpty(nameLine))
                        medication.Name = nameLine.Trim();
                }
                
                // Extract schedule
                var scheduleMatch = Regex.Match(block, @"Schedule:\s*(\d+)");
                if (scheduleMatch.Success)
                    medication.Schedule = int.Parse(scheduleMatch.Groups[1].Value);
                
                // Extract dosage form
                var dosageMatch = Regex.Match(block, @"Dosage Form:\s*([^\s]+)");
                if (dosageMatch.Success)
                    medication.DosageForm = dosageMatch.Groups[1].Value.Trim();
                
                // Extract supplier - take first word after "Supplier:"
                var supplierMatch = Regex.Match(block, @"Supplier:\s*([^\s]+)");
                if (supplierMatch.Success)
                    medication.Supplier = supplierMatch.Groups[1].Value.Trim();
                
                // Extract reorder level
                var reorderMatch = Regex.Match(block, @"Re-order level:\s*(\d+)");
                if (reorderMatch.Success)
                    medication.ReorderLevel = int.Parse(reorderMatch.Groups[1].Value);
                
                // Extract stock on hand
                var stockMatch = Regex.Match(block, @"Stock on hand:\s*(\d+)");
                if (stockMatch.Success)
                    medication.StockOnHand = int.Parse(stockMatch.Groups[1].Value);
                
                // Extract price - improved regex to handle different formats
                // Try multiple patterns to match price
                decimal? parsedPrice = null;
                string? matchedPattern = null;
                
                // Log the block to understand the format
                _logger.LogInformation("Attempting to extract price for medication: {Name}", medication.Name);
                _logger.LogInformation("Medication block preview: {Preview}", block.Substring(0, Math.Min(300, block.Length)));
                
                // Pattern 1: Price: R 123.45 or Price: 123.45 (with or without R)
                var priceMatch1 = Regex.Match(block, @"Price:\s*R?\s*([\d,]+\.?\d*)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
                if (priceMatch1.Success && priceMatch1.Groups.Count >= 2)
                {
                    var priceStr = priceMatch1.Groups[1].Value.Replace(",", "").Trim();
                    _logger.LogInformation("Pattern1 matched: '{PriceStr}'", priceStr);
                    if (decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var price) && price > 0)
                    {
                        parsedPrice = price;
                        matchedPattern = "Pattern1";
                    }
                }
                
                // Pattern 2: Price R123.45 or Price 123.45 (without colon, with or without R)
                if (!parsedPrice.HasValue)
                {
                    var priceMatch2 = Regex.Match(block, @"Price\s+R?\s*([\d,]+\.?\d*)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
                    if (priceMatch2.Success && priceMatch2.Groups.Count >= 2)
                    {
                        var priceStr = priceMatch2.Groups[1].Value.Replace(",", "").Trim();
                        _logger.LogInformation("Pattern2 matched: '{PriceStr}'", priceStr);
                        if (decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var price) && price > 0)
                        {
                            parsedPrice = price;
                            matchedPattern = "Pattern2";
                        }
                    }
                }
                
                // Pattern 3: Look for any number after "Price" text
                if (!parsedPrice.HasValue)
                {
                    var priceMatch3 = Regex.Match(block, @"Price[:\s]*R?\s*[\s]*([\d]{1,3}(?:,\d{3})*(?:\.\d{2})?)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
                    if (priceMatch3.Success && priceMatch3.Groups.Count >= 2)
                    {
                        var priceStr = priceMatch3.Groups[1].Value.Replace(",", "").Trim();
                        _logger.LogInformation("Pattern3 matched: '{PriceStr}'", priceStr);
                        if (decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var price) && price > 0)
                        {
                            parsedPrice = price;
                            matchedPattern = "Pattern3";
                        }
                    }
                }
                
                // Pattern 4: R followed by numbers with optional decimals
                if (!parsedPrice.HasValue)
                {
                    var priceMatch4 = Regex.Match(block, @"R\s*([\d,]+\.?\d*)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
                    if (priceMatch4.Success && priceMatch4.Groups.Count >= 2)
                    {
                        var priceStr = priceMatch4.Groups[1].Value.Replace(",", "").Trim();
                        _logger.LogInformation("Pattern4 matched: '{PriceStr}'", priceStr);
                        if (decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var price) && price > 0)
                        {
                            parsedPrice = price;
                            matchedPattern = "Pattern4";
                        }
                    }
                }
                
                // Pattern 5: Look for decimal numbers in lines containing "Price"
                if (!parsedPrice.HasValue)
                {
                    var lines = block.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (line.Contains("Price", StringComparison.OrdinalIgnoreCase))
                        {
                            // Look for number patterns like 123.45 or 123,45
                            var numberMatches = Regex.Matches(line, @"\d+([,.])\d{2}");
                            if (numberMatches.Count > 0)
                            {
                                var priceStr = numberMatches[numberMatches.Count - 1].Value.Replace(",", ".");
                                _logger.LogInformation("Pattern5 found in line with Price: '{PriceStr}'", priceStr);
                                if (decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var price) && price > 0 && price < 100000)
                                {
                                    parsedPrice = price;
                                    matchedPattern = "Pattern5";
                                    break;
                                }
                            }
                        }
                    }
                }
                
                if (parsedPrice.HasValue)
                {
                    medication.Price = parsedPrice.Value;
                    _logger.LogInformation("Successfully parsed price for {Name}: R{Price} (using {Pattern})", 
                        medication.Name, parsedPrice.Value, matchedPattern);
                }
                else
                {
                    _logger.LogWarning("No price found for medication: {Name}. Full block: {Block}", 
                        medication.Name, block);
                    // Set default price of 0.01 to avoid completely skipping the medication
                    medication.Price = 0.01m;
                }
                
                // Extract active ingredients from the block
                var ingredientPattern = @"([A-Za-z]+)\s+(\d+(?:\.\d+)?(?:mg|g))";
                var ingredientMatches = Regex.Matches(block, ingredientPattern, RegexOptions.IgnoreCase);
                foreach (Match ingMatch in ingredientMatches)
                {
                    medication.ActiveIngredients.Add(new ActiveIngredientData
                    {
                        Name = ingMatch.Groups[1].Value,
                        Strength = ingMatch.Groups[2].Value
                    });
                }
                
                parsedData.Medications.Add(medication);
                _logger.LogInformation("Added medication: {Name}, Schedule: {Schedule}, Form: {Form}, Supplier: {Supplier}", 
                    medication.Name, medication.Schedule, medication.DosageForm, medication.Supplier);
            }
        }
        
        private void ParseDoctorsSection(string sectionText, PdfParsedData parsedData)
        {
            if (string.IsNullOrEmpty(sectionText))
            {
                _logger.LogWarning("Doctors section is empty");
                return;
            }
            
            // Match doctor entries: Name Surname Phone Email Number
            var matches = Regex.Matches(sectionText, @"([A-Z][a-z]+)\s+([A-Z][a-z]+)\s+(\d{3}\s+\d{3}\s+\d{4})\s+([a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,})\s+(\d+)", RegexOptions.Multiline);
            foreach (Match match in matches)
            {
                if (match.Groups.Count == 6)
                {
                    parsedData.Doctors.Add(new DoctorData
                    {
                        FirstName = match.Groups[1].Value,
                        LastName = match.Groups[2].Value,
                        PhoneNumber = match.Groups[3].Value,
                        Email = match.Groups[4].Value,
                        PracticeNumber = match.Groups[5].Value
                    });
                }
            }
        }
        
        private void ParseManagersSection(string sectionText, PdfParsedData parsedData)
        {
            if (string.IsNullOrEmpty(sectionText))
            {
                _logger.LogWarning("Pharmacy Managers section is empty");
                return;
            }
            
            // Match manager entries: Name Surname Phone Email
            var matches = Regex.Matches(sectionText, @"([A-Z][a-z]+)\s+([A-Z][a-z]+)\s+(\d{3}\s+\d{3}\s+\d{4})\s+([a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,})", RegexOptions.Multiline);
            foreach (Match match in matches)
            {
                if (match.Groups.Count == 5)
                {
                    parsedData.PharmacyManagers.Add(new PharmacyManagerData
                    {
                        FirstName = match.Groups[1].Value,
                        LastName = match.Groups[2].Value,
                        PhoneNumber = match.Groups[3].Value,
                        Email = match.Groups[4].Value
                    });
                }
            }
        }
        
        private void ParsePharmacistsSection(string sectionText, PdfParsedData parsedData)
        {
            if (string.IsNullOrEmpty(sectionText))
            {
                _logger.LogWarning("Pharmacists section is empty");
                return;
            }
            
            // Match pharmacist entries: Name Surname Phone Email Number
            var matches = Regex.Matches(sectionText, @"([A-Z][a-z]+)\s+([A-Z][a-z]+)\s+(\d{3}\s+\d{4}\s+\d{3})\s+([a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,})\s+(\d+)", RegexOptions.Multiline);
            foreach (Match match in matches)
            {
                if (match.Groups.Count == 6)
                {
                    parsedData.Pharmacists.Add(new PharmacistData
                    {
                        FirstName = match.Groups[1].Value,
                        LastName = match.Groups[2].Value,
                        PhoneNumber = match.Groups[3].Value,
                        Email = match.Groups[4].Value,
                        RegistrationNumber = match.Groups[5].Value
                    });
                }
            }
        }
        

        private string ExtractSection(string text, string startMarker, string endMarker)
        {
            var pattern = $@"{Regex.Escape(startMarker)}[\s\S]*?{Regex.Escape(endMarker)}";
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var section = match.Value.Replace(startMarker, "").Replace(endMarker, "").Trim();
                _logger.LogInformation("Found section '{Start}': {Preview}", startMarker, section.Substring(0, Math.Min(200, section.Length)));
                return section;
            }
            _logger.LogWarning("Could not find section starting with '{Start}'", startMarker);
            return "";
        }
    }
}
