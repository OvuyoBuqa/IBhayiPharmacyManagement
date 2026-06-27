using IBhayiPharmacyManagementSystem.Models;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IBhayiPharmacyManagementSystem.Utilities.Reports
{
    public class PharmacistReportGenerator
    {
        public byte[] GenerateReport(List<DispensedPrescription> prescriptions, string pharmacistName, DateTime startDate, DateTime endDate, string groupBy)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                Document document = new Document(PageSize.A4, 25, 25, 30, 30);
                PdfWriter writer = PdfWriter.GetInstance(document, ms);

                // Add header and footer
                writer.PageEvent = new PharmacistReportPageEvent(pharmacistName);

                document.Open();

                // Title
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18);
                var title = new Paragraph($"PRESCRIPTIONS DISPENSED {pharmacistName.ToUpper()}", titleFont);
                title.Alignment = Element.ALIGN_CENTER;
                document.Add(title);

                document.Add(new Paragraph(" ")); // Spacing

                // Date range
                var dateFont = FontFactory.GetFont(FontFactory.HELVETICA, 12);
                var dateRange = new Paragraph($"Date Range: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}", dateFont);
                dateRange.Alignment = Element.ALIGN_CENTER;
                document.Add(dateRange);

                document.Add(new Paragraph(" ")); // Spacing

                if (groupBy.ToLower() == "patient")
                {
                    GeneratePrescriptionsByPatientReport(document, prescriptions);
                }
                else if (groupBy.ToLower() == "medication")
                {
                    GeneratePrescriptionsByMedicationReport(document, prescriptions);
                }
                else if (groupBy.ToLower() == "schedule")
                {
                    GeneratePrescriptionsByScheduleReport(document, prescriptions);
                }

                document.Close();
                return ms.ToArray();
            }
        }

        private void GeneratePrescriptionsByPatientReport(Document document, List<DispensedPrescription> prescriptions)
        {
            var groupFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14);
            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
            var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);

            var groupedPrescriptions = prescriptions
                .GroupBy(dp => dp.PrescriptionLine?.Prescription?.Customer?.FullName ?? "Unknown Patient")
                .OrderBy(g => g.Key);

            foreach (var group in groupedPrescriptions)
            {
                // Patient header
                var patientHeader = new Paragraph($"PATIENT: {group.Key}", groupFont);
                document.Add(patientHeader);
                document.Add(new Paragraph(" ")); // Spacing

                // Create table
                PdfPTable table = new PdfPTable(4);
                table.WidthPercentage = 100;

                // Table headers
                var header1 = new PdfPCell(new Phrase("Date", headerFont));
                header1.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header1);
                
                var header2 = new PdfPCell(new Phrase("Medication", headerFont));
                header2.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header2);
                
                var header3 = new PdfPCell(new Phrase("Qty", headerFont));
                header3.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header3);
                
                var header4 = new PdfPCell(new Phrase("Instructions", headerFont));
                header4.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header4);

                int groupQuantity = 0;

                foreach (var prescription in group.OrderBy(dp => dp.DispensedDate))
                {
                    table.AddCell(new PdfPCell(new Phrase(prescription.DispensedDate.ToString("dd/MM/yyyy"), normalFont)));
                    table.AddCell(new PdfPCell(new Phrase(prescription.PrescriptionLine?.Medication?.Name ?? "Unknown Medication", normalFont)));
                    table.AddCell(new PdfPCell(new Phrase(prescription.QuantityDispensed.ToString(), normalFont)));
                    // Use PatientInstructions if available, otherwise fall back to PrescriptionLine.Instructions
                    var instructions = !string.IsNullOrEmpty(prescription.PatientInstructions) 
                        ? prescription.PatientInstructions 
                        : prescription.PrescriptionLine?.Instructions ?? "N/A";
                    table.AddCell(new PdfPCell(new Phrase(instructions, normalFont)));

                    groupQuantity += prescription.QuantityDispensed;
                }

                document.Add(table);
                document.Add(new Paragraph(" ")); // Spacing

                // Subtotal
                var subtotal = new Paragraph($"Sub-total: {groupQuantity}", normalFont);
                subtotal.Alignment = Element.ALIGN_LEFT;
                document.Add(subtotal);
                document.Add(new Paragraph(" ")); // Spacing
                document.Add(new Paragraph(" ")); // Spacing
            }

            // Grand total
            var grandTotalQuantity = prescriptions.Sum(dp => dp.QuantityDispensed);
            var grandTotalText = new Paragraph($"GRAND TOTAL: {grandTotalQuantity}", groupFont);
            grandTotalText.Alignment = Element.ALIGN_CENTER;
            document.Add(grandTotalText);
        }

        private void GeneratePrescriptionsByMedicationReport(Document document, List<DispensedPrescription> prescriptions)
        {
            var groupFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14);
            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
            var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);

            var groupedPrescriptions = prescriptions
                .GroupBy(dp => dp.PrescriptionLine.Medication.Name)
                .OrderBy(g => g.Key);

            foreach (var group in groupedPrescriptions)
            {
                // Medication header
                var medicationHeader = new Paragraph($"MEDICATION: {group.Key}", groupFont);
                document.Add(medicationHeader);
                document.Add(new Paragraph(" ")); // Spacing

                // Create table
                PdfPTable table = new PdfPTable(4);
                table.WidthPercentage = 100;

                // Table headers
                var header1 = new PdfPCell(new Phrase("Date", headerFont));
                header1.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header1);
                
                var header2 = new PdfPCell(new Phrase("Patient", headerFont));
                header2.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header2);
                
                var header3 = new PdfPCell(new Phrase("Qty", headerFont));
                header3.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header3);
                
                var header4 = new PdfPCell(new Phrase("Instructions", headerFont));
                header4.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header4);

                int groupQuantity = 0;

                foreach (var prescription in group.OrderBy(dp => dp.DispensedDate))
                {
                    table.AddCell(new PdfPCell(new Phrase(prescription.DispensedDate.ToString("dd/MM/yyyy"), normalFont)));
                    table.AddCell(new PdfPCell(new Phrase(prescription.PrescriptionLine?.Prescription?.Customer?.FullName ?? "Unknown", normalFont)));
                    table.AddCell(new PdfPCell(new Phrase(prescription.QuantityDispensed.ToString(), normalFont)));
                    // Use PatientInstructions if available, otherwise fall back to PrescriptionLine.Instructions
                    var instructions = !string.IsNullOrEmpty(prescription.PatientInstructions) 
                        ? prescription.PatientInstructions 
                        : prescription.PrescriptionLine?.Instructions ?? "N/A";
                    table.AddCell(new PdfPCell(new Phrase(instructions, normalFont)));

                    groupQuantity += prescription.QuantityDispensed;
                }

                document.Add(table);
                document.Add(new Paragraph(" ")); // Spacing

                // Subtotal
                var subtotal = new Paragraph($"Sub-total: {groupQuantity}", normalFont);
                subtotal.Alignment = Element.ALIGN_LEFT;
                document.Add(subtotal);
                document.Add(new Paragraph(" ")); // Spacing
                document.Add(new Paragraph(" ")); // Spacing
            }

            // Grand total
            var grandTotalQuantity = prescriptions.Sum(dp => dp.QuantityDispensed);
            var grandTotalText = new Paragraph($"GRAND TOTAL: {grandTotalQuantity}", groupFont);
            grandTotalText.Alignment = Element.ALIGN_CENTER;
            document.Add(grandTotalText);
        }

        private void GeneratePrescriptionsByScheduleReport(Document document, List<DispensedPrescription> prescriptions)
        {
            var groupFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14);
            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
            var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);

            var groupedPrescriptions = prescriptions
                .GroupBy(dp => dp.PrescriptionLine?.Medication?.Schedule.ToString() ?? "Unknown Schedule")
                .OrderBy(g => g.Key);

            foreach (var group in groupedPrescriptions)
            {
                // Schedule header
                var scheduleHeader = new Paragraph($"SCHEDULE: {group.Key}", groupFont);
                document.Add(scheduleHeader);
                document.Add(new Paragraph(" ")); // Spacing

                // Create table
                PdfPTable table = new PdfPTable(4);
                table.WidthPercentage = 100;

                // Table headers
                var header1 = new PdfPCell(new Phrase("Date", headerFont));
                header1.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header1);
                
                var header2 = new PdfPCell(new Phrase("Medication", headerFont));
                header2.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header2);
                
                var header3 = new PdfPCell(new Phrase("Qty", headerFont));
                header3.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header3);
                
                var header4 = new PdfPCell(new Phrase("Instructions", headerFont));
                header4.BackgroundColor = BaseColor.LIGHT_GRAY;
                table.AddCell(header4);

                int groupQuantity = 0;

                foreach (var prescription in group.OrderBy(dp => dp.DispensedDate))
                {
                    table.AddCell(new PdfPCell(new Phrase(prescription.DispensedDate.ToString("dd/MM/yyyy"), normalFont)));
                    table.AddCell(new PdfPCell(new Phrase(prescription.PrescriptionLine?.Medication?.Name ?? "Unknown Medication", normalFont)));
                    table.AddCell(new PdfPCell(new Phrase(prescription.QuantityDispensed.ToString(), normalFont)));
                    // Use PatientInstructions if available, otherwise fall back to PrescriptionLine.Instructions
                    var instructions = !string.IsNullOrEmpty(prescription.PatientInstructions) 
                        ? prescription.PatientInstructions 
                        : prescription.PrescriptionLine?.Instructions ?? "N/A";
                    table.AddCell(new PdfPCell(new Phrase(instructions, normalFont)));

                    groupQuantity += prescription.QuantityDispensed;
                }

                document.Add(table);
                document.Add(new Paragraph(" ")); // Spacing

                // Subtotal
                var subtotal = new Paragraph($"Sub-total: {groupQuantity}", normalFont);
                subtotal.Alignment = Element.ALIGN_LEFT;
                document.Add(subtotal);
                document.Add(new Paragraph(" ")); // Spacing
                document.Add(new Paragraph(" ")); // Spacing
            }

            // Grand total
            var grandTotalQuantity = prescriptions.Sum(dp => dp.QuantityDispensed);
            var grandTotalText = new Paragraph($"GRAND TOTAL: {grandTotalQuantity}", groupFont);
            grandTotalText.Alignment = Element.ALIGN_CENTER;
            document.Add(grandTotalText);
        }
    }

    // Page event handler for headers and footers
    public class PharmacistReportPageEvent : PdfPageEventHelper
    {
        private string pharmacistName;

        public PharmacistReportPageEvent(string pharmacistName)
        {
            this.pharmacistName = pharmacistName;
        }

        public override void OnEndPage(PdfWriter writer, Document document)
        {
            // Draw page number directly on the content stream to avoid recursion
            var cb = writer.DirectContent;
            var pageNumberText = $"Page {writer.PageNumber}";

            var baseFont = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.WINANSI, BaseFont.NOT_EMBEDDED);
            cb.BeginText();
            cb.SetFontAndSize(baseFont, 8);

            // Centered at the bottom of the page
            float x = (document.Right + document.Left) / 2;
            float y = document.Bottom - 10; // slightly below content area
            cb.ShowTextAligned(Element.ALIGN_CENTER, pageNumberText, x, y, 0);
            cb.EndText();
        }
    }
}

