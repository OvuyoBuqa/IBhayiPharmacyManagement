using System;
using System.Collections.Generic;

namespace IBhayiPharmacyManagementSystem.ViewModels
{
    public class PharmacistReportViewModel
    {
        public int PharmacistId { get; set; }
        public string PharmacistFullName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string GroupBy { get; set; }
        public List<PrescriptionDetailViewModel> PrescriptionsDispensed { get; set; } = new List<PrescriptionDetailViewModel>();
    }
}
