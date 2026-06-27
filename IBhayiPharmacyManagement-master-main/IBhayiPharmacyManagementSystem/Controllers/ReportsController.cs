using Microsoft.AspNetCore.Mvc;
using IBhayiPharmacyManagementSystem.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using IBhayiPharmacyManagementSystem.Models;
using IBhayiPharmacyManagementSystem.Utilities.Reports;
using Microsoft.AspNetCore.Authorization;

namespace IBhayiPharmacyManagementSystem.Controllers
{
    [Authorize(Roles = "PharmacyManager")]
    public class ReportsController : Controller
    {
        private readonly AppDbContext _context;

        public ReportsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> PharmacyManagerReport(string groupBy = "None")
        {
            // Prepare the view with group by options
            ViewBag.GroupByOptions = new List<string> { "None", "DosageForm", "Schedule", "Supplier" };
            ViewBag.SelectedGroupBy = groupBy;

            // If it's an initial load or no groupBy specified, just return the view
            // The actual PDF generation will happen on a POST or a specific PDF action
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GeneratePharmacyManagerReport(string groupBy)
        {
            if (string.IsNullOrEmpty(groupBy)) groupBy = "None";

            var medications = await _context.Medications
                                            .Include(m => m.DosageForm)
                                            .Include(m => m.Supplier)
                                            .ToListAsync();

            var reportGenerator = new PharmacyReportGenerator();
            byte[] pdfBytes = reportGenerator.GenerateReport(medications, groupBy);

            // Return the PDF file
            return File(pdfBytes, "application/pdf", "PharmacyManagerReport.pdf");
        }
    }
}
