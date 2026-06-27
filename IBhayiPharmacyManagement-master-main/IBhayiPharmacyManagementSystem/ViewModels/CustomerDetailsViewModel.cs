using IBhayiPharmacyManagementSystem.Models;

namespace IBhayiPharmacyManagementSystem.ViewModels
{

    public class CustomerDetailsViewModel
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string IDNumber { get; set; }
        public string CellPhoneNumber { get; set; }
        public string FullAddress { get; set; }
        public string ProfileImagePath { get; set; }
        public DateTime DateCreated { get; set; }
        public bool IsWalkInCustomer { get; set; }

        // You can add a method to generate the full address if needed
        public string GenerateFullAddress()
        {
            return $"{Street}, {Suburb}, {City}, {Province}, {ZipCode}, {Country}";
        }

        // These properties are only needed if you use the GenerateFullAddress method
        public string Street { get; set; }
        public string Suburb { get; set; }
        public string City { get; set; }
        public string Province { get; set; }
        public string ZipCode { get; set; }
        public string Country { get; set; }

        public List<CustomerAllergy> Allergies { get; set; } = new List<CustomerAllergy>();

        public List<ActiveIngredients> ActiveIngredients { get; set; } = new List<ActiveIngredients>();
        public List<Prescription> Prescriptions { get; set; } = new List<Prescription>();
        public List<Medication> Medications { get; set; } = new List<Medication>();

        public string GetSeverityBadgeClass(string severity)
        {
            return severity switch
            {
                "Mild" => "bg-info",
                "Medium" => "bg-warning",
                "Severe" => "bg-danger",
                "Life-threatening" => "bg-dark",
                _ => "bg-secondary"
            };
        }
    }
}
