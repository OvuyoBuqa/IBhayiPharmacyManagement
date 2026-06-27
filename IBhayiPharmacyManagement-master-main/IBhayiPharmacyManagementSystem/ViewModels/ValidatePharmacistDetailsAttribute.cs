using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace IBhayiPharmacyManagementSystem.ViewModels
{
    public class ValidatePharmacistDetailsAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var viewModel = validationContext.ObjectInstance as RegisterStaffViewModel;
            if (viewModel == null) return ValidationResult.Success;

            if (viewModel.Role == "Pharmacist")
            {
                if (string.IsNullOrWhiteSpace(viewModel.IDNumber))
                {
                    return new ValidationResult("ID Number is required for Pharmacists.", new[] { nameof(viewModel.IDNumber) });
                }
                if (string.IsNullOrWhiteSpace(viewModel.CellPhoneNumber))
                {
                    return new ValidationResult("Cellphone Number is required for Pharmacists.", new[] { nameof(viewModel.CellPhoneNumber) });
                }
                if (string.IsNullOrWhiteSpace(viewModel.RegistrationNumber))
                {
                    return new ValidationResult("Registration Number is required for Pharmacists.", new[] { nameof(viewModel.RegistrationNumber) });
                }
            }
            return ValidationResult.Success;
        }
    }
}
