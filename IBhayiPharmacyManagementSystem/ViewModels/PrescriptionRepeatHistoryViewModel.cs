using System;

namespace IBhayiPharmacyManagementSystem.ViewModels
{
    public class PrescriptionRepeatHistoryViewModel
    {
        public int PrescriptionRepeatId { get; set; }
        public string MedicationName { get; set; } = string.Empty;
        public string DosageForm { get; set; } = string.Empty;
        public string Instructions { get; set; } = string.Empty;
        public string Frequency { get; set; } = string.Empty;
        public string DoctorName { get; set; } = string.Empty;
        public DateTime PrescriptionDate { get; set; }
        public int TotalRepeats { get; set; }
        public int RemainingRepeats { get; set; }
        public int DispensedCount { get; set; }
        public DateTime? LastDispensedDate { get; set; }
        public bool IsActive { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }
}
