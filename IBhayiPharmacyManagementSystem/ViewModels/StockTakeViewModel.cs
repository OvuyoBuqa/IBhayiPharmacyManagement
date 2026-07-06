using IBhayiPharmacyManagementSystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using IBhayiPharmacyManagementSystem.Models;

namespace IBhayiPharmacyManagementSystem.Views.StockManagements
{
    public class StockTakeViewModel
    {
        public List<Medication> Medications { get; set; } = new List<Medication>();
        public string GroupBy { get; set; } = "None";

        public List<SelectListItem> GroupByOptions => new List<SelectListItem>
        {
            new SelectListItem { Value = "None", Text = "No Grouping" },
            new SelectListItem { Value = "DosageForm", Text = "Dosage Form" },
            new SelectListItem { Value = "Schedule", Text = "Schedule" },
            new SelectListItem { Value = "Supplier", Text = "Supplier" }
        };
    }
}
