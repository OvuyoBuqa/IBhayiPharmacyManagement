using IBhayiPharmacyManagementSystem.Models;
using IBhayiPharmacyManagementSystem.ViewModels;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IBhayiPharmacyManagementSystem.Utilities.Reports
{
    public class CustomerReportGenerator
    {
        public byte[] GenerateReport(CustomerReportViewModel model)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                Document document = new Document(PageSize.LETTER, 36, 36, 90, 54);
                PdfWriter writer = PdfWriter.GetInstance(document, ms);

                var pageEventHandler = new PharmacyReportPageEventHandler(); // Reusing existing handler
                writer.PageEvent = pageEventHandler;

                document.Open();

                AddTitlePage(document, model);
                document.NewPage();

                AddReportContent(document, model);

                document.Close();
                return ms.ToArray();
            }
        }

        private void AddTitlePage(Document document, CustomerReportViewModel model)
        {
            var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 24);
            var subtitleFont = FontFactory.GetFont(FontFactory.HELVETICA, 16);
            var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 12);

            document.Add(new Paragraph("Customer Prescriptions & Orders Report", titleFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 20
            });

            document.Add(new Paragraph($"For Customer: {model.CustomerName}", subtitleFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 10
            });

            document.Add(new Paragraph($"Date Range: {model.StartDate:yyyy-MM-dd} - {model.EndDate:yyyy-MM-dd}", subtitleFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 40
            });

            document.Add(new Paragraph($"Date Generated: {DateTime.Now:yyyy-MM-dd HH:mm}", normalFont));
            document.Add(new Paragraph("This report details dispensed prescriptions and placed orders within the specified date range.", normalFont)
            {
                SpacingAfter = 60
            });
        }

        private void AddReportContent(Document document, CustomerReportViewModel model)
        {
            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
            var groupFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12);
            var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
            var boldFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);

            // Prescriptions Section
            document.Add(new Paragraph("DISPENSED PRESCRIPTIONS", headerFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingBefore = 20, // Add spacing before section
                SpacingAfter = 20
            });

            PdfPTable prescriptionTable = new PdfPTable(4) { WidthPercentage = 100 };
            prescriptionTable.SetWidths(new float[] { 2, 4, 1, 1 });
            AddTableHeader(prescriptionTable, "Date", "Medication", "Qty", "Repeats");

            IEnumerable<IGrouping<string, PrescriptionDetailViewModel>> groupedPrescriptions;
            switch (model.GroupBy?.ToLower())
            {
                case "doctor":
                    groupedPrescriptions = model.Prescriptions
                        .GroupBy(p => p.DoctorName ?? "Unknown Doctor")
                        .OrderBy(g => g.Key);
                    break;
                case "medication":
                    groupedPrescriptions = model.Prescriptions
                        .GroupBy(p => p.MedicationName ?? "Unknown Medication")
                        .OrderBy(g => g.Key);
                    break;
                default:
                    groupedPrescriptions = model.Prescriptions
                        .GroupBy(p => "All Prescriptions")
                        .OrderBy(g => g.Key);
                    break;
            }

            int grandTotalPrescriptions = 0;
            foreach (var group in groupedPrescriptions)
            {
                PdfPCell groupCell = new PdfPCell(new Phrase($"{(model.GroupBy == "doctor" ? "DOCTOR" : model.GroupBy == "medication" ? "MEDICATION" : "CATEGORY").ToUpper()}: {group.Key}", groupFont))
                {
                    Colspan = 4,
                    BackgroundColor = new BaseColor(221, 235, 247),
                    HorizontalAlignment = Element.ALIGN_LEFT,
                    Padding = 8
                };
                prescriptionTable.AddCell(groupCell);

                int groupQtyTotal = 0;
                foreach (var prescription in group.OrderBy(p => p.Date))
                {
                    prescriptionTable.AddCell(new Phrase(prescription.Date.ToShortDateString(), normalFont));
                    prescriptionTable.AddCell(new Phrase(prescription.MedicationName, normalFont));
                    prescriptionTable.AddCell(new Phrase(prescription.Quantity.ToString(), normalFont));
                    prescriptionTable.AddCell(new Phrase(prescription.Repeats.ToString(), normalFont));
                    groupQtyTotal += prescription.Quantity;
                }
                AddTotalRow(prescriptionTable, "Sub-total:", groupQtyTotal.ToString(), 4);
                grandTotalPrescriptions += groupQtyTotal;
            }
            document.Add(prescriptionTable);
            AddTotalRow(prescriptionTable, "GRAND TOTAL:", grandTotalPrescriptions.ToString(), 4, true);
            document.Add(prescriptionTable);

            // Orders Section (if applicable)
            if (model.Orders != null && model.Orders.Any())
            {
                document.NewPage(); // New page for orders section
                document.Add(new Paragraph("CUSTOMER ORDERS", headerFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingBefore = 20,
                    SpacingAfter = 20
                });

                PdfPTable orderTable = new PdfPTable(5) { WidthPercentage = 100 };
                orderTable.SetWidths(new float[] { 1.5f, 2, 2, 1.5f, 1 });
                AddTableHeader(orderTable, "Date", "Order #", "Supplier", "Total Amount", "Status");

                IEnumerable<IGrouping<string, OrderDetailViewModel>> groupedOrders;
                // For orders, you might group by supplier or just list them
                groupedOrders = model.Orders
                    .GroupBy(o => o.SupplierName ?? "Unknown Supplier")
                    .OrderBy(g => g.Key);

                decimal grandTotalOrders = 0;
                foreach (var group in groupedOrders)
                {
                    PdfPCell groupCell = new PdfPCell(new Phrase($"SUPPLIER: {group.Key}", groupFont))
                    {
                        Colspan = 5,
                        BackgroundColor = new BaseColor(221, 235, 247),
                        HorizontalAlignment = Element.ALIGN_LEFT,
                        Padding = 8
                    };
                    orderTable.AddCell(groupCell);

                    decimal groupAmountTotal = 0;
                    foreach (var order in group.OrderBy(o => o.Date))
                    {
                        orderTable.AddCell(new Phrase(order.Date.ToShortDateString(), normalFont));
                        orderTable.AddCell(new Phrase(order.OrderNumber, normalFont));
                        orderTable.AddCell(new Phrase(order.SupplierName, normalFont));
                        orderTable.AddCell(new Phrase(order.TotalAmount.ToString("C"), normalFont));
                        orderTable.AddCell(new Phrase(order.Status, normalFont));
                        groupAmountTotal += order.TotalAmount;
                    }
                    AddTotalRow(orderTable, "Sub-total:", groupAmountTotal.ToString("C"), 5);
                    grandTotalOrders += groupAmountTotal;
                }
                document.Add(orderTable);
                AddTotalRow(orderTable, "GRAND TOTAL:", grandTotalOrders.ToString("C"), 5, true);
                document.Add(orderTable);
            }
        }

        private void AddTableHeader(PdfPTable table, params string[] headers)
        {
            foreach (string header in headers)
            {
                PdfPCell cell = new PdfPCell(new Phrase(header))
                {
                    BackgroundColor = new BaseColor(221, 221, 221),
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    Padding = 5
                };
                table.AddCell(cell);
            }
        }

        private void AddTotalRow(PdfPTable table, string label, string value, int colspan, bool isGrandTotal = false)
        {
            var font = isGrandTotal
                ? FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12)
                : FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);

            PdfPCell labelCell = new PdfPCell(new Phrase(label, font))
            {
                Colspan = colspan - 1,
                Border = PdfPCell.TOP_BORDER,
                BorderWidthTop = isGrandTotal ? 2f : 1f,
                Padding = 5,
                HorizontalAlignment = Element.ALIGN_RIGHT
            };
            table.AddCell(labelCell);

            PdfPCell valueCell = new PdfPCell(new Phrase(value, font))
            {
                Border = PdfPCell.TOP_BORDER,
                BorderWidthTop = isGrandTotal ? 2f : 1f,
                Padding = 5,
                HorizontalAlignment = Element.ALIGN_RIGHT
            };
            table.AddCell(valueCell);
        }
    }
}
