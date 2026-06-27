using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace IBhayiPharmacyManagementSystem.ViewModels
{
    public class CustomerReportViewModel
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string GroupBy { get; set; }
        public List<PrescriptionDetailViewModel> Prescriptions { get; set; } = new List<PrescriptionDetailViewModel>();
        public List<OrderDetailViewModel> Orders { get; set; } = new List<OrderDetailViewModel>();
    }

    public class PrescriptionDetailViewModel
    {
        public DateTime Date { get; set; }
        public string MedicationName { get; set; }
        public int Quantity { get; set; }
        public int Repeats { get; set; }
        public string DoctorName { get; set; }
        public string Instructions { get; set; } // Added for Pharmacist Report, but good to have here
    }

    public class OrderDetailViewModel
    {
        public DateTime Date { get; set; }
        public string OrderNumber { get; set; }
        public string SupplierName { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; }
    }
}
