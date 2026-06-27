using System.ComponentModel.DataAnnotations;

namespace IBhayiPharmacyManagementSystem.ViewModels
{
    public class DataImportDashboardViewModel
    {
        [Required(ErrorMessage = "Please select a PDF file")]
        public IFormFile? PdfFile { get; set; }
        
        public IFormFile? XlsxFile { get; set; }
        
        public DatabaseStatsViewModel DatabaseStats { get; set; } = new();
    }

    public class DatabaseStatsViewModel
    {
        public int ActiveIngredientsCount { get; set; }
        public int DosageFormsCount { get; set; }
        public int SuppliersCount { get; set; }
        public int MedicationsCount { get; set; }
        public int DoctorsCount { get; set; }
        public int PharmacyManagersCount { get; set; }
        public int PharmacistsCount { get; set; }
        public int CustomersCount { get; set; }
        public int OrdersCount { get; set; }
        public int PrescriptionsCount { get; set; }
        public int DispensedPrescriptionsCount { get; set; }
    }

    public class DataImportPreviewViewModel
    {
        public PdfParsedData ParsedData { get; set; } = new();
        public ImportOptionsViewModel ImportOptions { get; set; } = new();
    }

    public class ImportOptionsViewModel
    {
        public bool ImportActiveIngredients { get; set; } = true;
        public bool ImportDosageForms { get; set; } = true;
        public bool ImportSuppliers { get; set; } = true;
        public bool ImportMedications { get; set; } = true;
        public bool ImportDoctors { get; set; } = true;
        public bool ImportPharmacyManagers { get; set; } = true;
        public bool ImportPharmacists { get; set; } = true;
    }

    public class ImportResultsViewModel
    {
        public ImportResult ActiveIngredientsResult { get; set; } = new();
        public ImportResult DosageFormsResult { get; set; } = new();
        public ImportResult SuppliersResult { get; set; } = new();
        public ImportResult MedicationsResult { get; set; } = new();
        public ImportResult DoctorsResult { get; set; } = new();
        public ImportResult PharmacyManagersResult { get; set; } = new();
        public ImportResult PharmacistsResult { get; set; } = new();
        public ImportResult CustomersResult { get; set; } = new();
        public ImportResult StockOrdersResult { get; set; } = new();
        public ImportResult PrescriptionsResult { get; set; } = new();
        public ImportResult OrdersResult { get; set; } = new();
        
        public bool HasErrors => ActiveIngredientsResult.ErrorCount > 0 ||
                                DosageFormsResult.ErrorCount > 0 ||
                                SuppliersResult.ErrorCount > 0 ||
                                MedicationsResult.ErrorCount > 0 ||
                                DoctorsResult.ErrorCount > 0 ||
                                PharmacyManagersResult.ErrorCount > 0 ||
                                PharmacistsResult.ErrorCount > 0 ||
                                CustomersResult.ErrorCount > 0 ||
                                StockOrdersResult.ErrorCount > 0 ||
                                PrescriptionsResult.ErrorCount > 0 ||
                                OrdersResult.ErrorCount > 0;
    }

    public class ImportResult
    {
        public int SuccessCount { get; set; }
        public int SkippedCount { get; set; }
        public int ErrorCount { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class PdfParsedData
    {
        public List<string> ActiveIngredients { get; set; } = new();
        public List<string> DosageForms { get; set; } = new();
        public List<SupplierData> Suppliers { get; set; } = new();
        public List<MedicationData> Medications { get; set; } = new();
        public List<DoctorData> Doctors { get; set; } = new();
        public List<PharmacyManagerData> PharmacyManagers { get; set; } = new();
        public List<PharmacistData> Pharmacists { get; set; } = new();
    }

    public class SupplierData
    {
        public string Name { get; set; } = string.Empty;
        public string ContactFirstName { get; set; } = string.Empty;
        public string ContactSurname { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        
        // Computed property that combines first name and surname for database storage
        public string ContactPerson => $"{ContactFirstName} {ContactSurname}".Trim();
    }

    public class MedicationData
    {
        public string Name { get; set; } = string.Empty;
        public int Schedule { get; set; }
        public string DosageForm { get; set; } = string.Empty;
        public string Supplier { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int ReorderLevel { get; set; }
        public int StockOnHand { get; set; }
        public List<ActiveIngredientData> ActiveIngredients { get; set; } = new();
    }

    public class ActiveIngredientData
    {
        public string Name { get; set; } = string.Empty;
        public string Strength { get; set; } = string.Empty;
    }

    public class DoctorData
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PracticeNumber { get; set; } = string.Empty;
    }

    public class PharmacyManagerData
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class PharmacistData
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string RegistrationNumber { get; set; } = string.Empty;
    }

    public class CustomerData
    {
        public string FullName { get; set; } = string.Empty;
        public string IDNumber { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Allergies { get; set; } = string.Empty;
        
        // Parsed properties
        public string FirstName { get; set; } = string.Empty;
        public string Surname { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string Suburb { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Province { get; set; } = string.Empty;
        public List<string> AllergyList { get; set; } = new List<string>();
    }

    public class StockOrderData
    {
        public DateTime Date { get; set; }
        public string Supplier { get; set; } = string.Empty;
        public string Medication { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string OrderStatus { get; set; } = string.Empty;
    }

    public class PrescriptionData
    {
        public DateTime Date { get; set; }
        public string Customer { get; set; } = string.Empty;
        public string Doctor { get; set; } = string.Empty;
        public string Medication { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Instructions { get; set; } = string.Empty;
        public int Repeats { get; set; }
        public string Frequency { get; set; } = string.Empty;
    }

    public class OrderData
    {
        public DateTime? OrderDate { get; set; }
        public string Customer { get; set; } = string.Empty;
        public string Medications { get; set; } = string.Empty; // Comma-separated list
        public string OrderStatus { get; set; } = string.Empty;
        public List<string> MedicationList { get; set; } = new List<string>();
    }

    public class XlsxParsedData
    {
        public List<CustomerData> Customers { get; set; } = new List<CustomerData>();
        public List<StockOrderData> StockOrders { get; set; } = new List<StockOrderData>();
        public List<PrescriptionData> Prescriptions { get; set; } = new List<PrescriptionData>();
        public List<OrderData> Orders { get; set; } = new List<OrderData>();
    }
}
