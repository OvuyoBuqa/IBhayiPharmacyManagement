using Microsoft.AspNetCore.Identity;

namespace IBhayiPharmacyManagementSystem.Models
{
    public class Users : IdentityUser
    {

        public string FullName { get; set; }
        public bool ForcePasswordChange { get; set; } = false;
    }
}
