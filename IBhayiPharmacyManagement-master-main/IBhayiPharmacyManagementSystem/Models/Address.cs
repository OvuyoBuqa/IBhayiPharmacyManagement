using System.ComponentModel.DataAnnotations;

namespace IBhayiPharmacyManagementSystem.Models
{
    public class Address
    {
        [Key]   
        public int AddressId { get; set; }
        public string Street { get; set; }
        public string Suburb { get; set; }
        public string Province { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        public string ZipCode { get; set; }

    }
}
