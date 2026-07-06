using System.ComponentModel.DataAnnotations;

namespace IBhayiPharmacyManagementSystem.Enums
{
    public enum DosageFrequency
    {
        [Display(Name = "Once daily")]
        OnceDaily = 1,
        [Display(Name = "Twice daily")]
        TwiceDaily = 2,
        [Display(Name = "Three times daily")]
        ThreeTimesDaily = 3,
        [Display(Name = "Four times daily")]
        FourTimesDaily = 4,
        [Display(Name = "As needed")]
        AsNeeded = 0
    }
}
