using IBhayiPharmacyManagementSystem.Models;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class PharmacyReportGenerator
{
    public byte[] GenerateReport(List<Medication> medications, string groupBy)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            // PDF setup
            Document document = new Document(PageSize.LETTER, 36, 36, 90, 54); // Margins
            PdfWriter writer = PdfWriter.GetInstance(document, ms);

            // Add header/footer handler
            var pageEventHandler = new PharmacyReportPageEventHandler();
            writer.PageEvent = pageEventHandler;

            document.Open();

            // Title page
            AddTitlePage(document);
            document.NewPage();

            // Report content
            AddReportContent(document, medications, groupBy);

            document.Close();
            return ms.ToArray();
        }
    }

    private void AddTitlePage(Document document)
    {
        var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 24);
        var subtitleFont = FontFactory.GetFont(FontFactory.HELVETICA, 16);
        var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 12);

        // Title
        document.Add(new Paragraph("Pharmacy Manager Report", titleFont)
        {
            Alignment = Element.ALIGN_CENTER,
            SpacingAfter = 20
        });

        // Subtitle
        document.Add(new Paragraph("Stock Take Report", subtitleFont)
        {
            Alignment = Element.ALIGN_CENTER,
            SpacingAfter = 40
        });

        // Metadata
        document.Add(new Paragraph($"Date Generated: {DateTime.Now:yyyy-MM-dd HH:mm}", normalFont));
        document.Add(new Paragraph("Prepared For: Pharmacy Inventory Audit", normalFont));
        document.Add(new Paragraph("This report contains current stock levels for inventory verification", normalFont)
        {
            SpacingAfter = 60
        });
    }

    private void AddReportContent(Document document, List<Medication> medications, string groupBy)
    {
        var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
        var groupFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12);
        var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
        var boldFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);

        // Group header
        string groupTitle = groupBy.ToLower() switch
        {
            "dosageform" => "STOCK BY DOSAGE FORM",
            "schedule" => "STOCK BY SCHEDULE",
            "supplier" => "STOCK BY SUPPLIER",
            _ => "STOCK BY MEDICATION"
        };

        document.Add(new Paragraph(groupTitle, headerFont)
        {
            Alignment = Element.ALIGN_CENTER,
            SpacingAfter = 20
        });

        // Group medications
        IEnumerable<IGrouping<string, Medication>> groupedData;
        switch (groupBy.ToLower())
        {
            case "dosageform":
                groupedData = medications
                    .GroupBy(m => m.DosageForm != null ? m.DosageForm.Type : "Unknown")
                    .OrderBy(g => g.Key);
                break;
            case "schedule":
                groupedData = medications
                    .GroupBy(m => m.Schedule.ToString())
                    .OrderBy(g => g.Key);
                break;
            case "supplier":
                groupedData = medications
                    .GroupBy(m => m.Supplier != null ? m.Supplier.Name : "Unknown")
                    .OrderBy(g => g.Key);
                break;
            default:
                // Default grouping or throw error if an invalid group by is passed
                groupedData = medications
                    .GroupBy(m => "All Medications")
                    .OrderBy(g => g.Key);
                break;
        }

        int grandTotal = 0;

        foreach (var group in groupedData)
        {
            // Group header - label should reflect the selected grouping
            string headerLabel = groupBy.ToLower() switch
            {
                "dosageform" => "DOSAGE FORM",
                "schedule" => "SCHEDULE",
                "supplier" => "SUPPLIER",
                _ => "GROUP"
            };

            var groupHeader = new Paragraph($"{headerLabel}: {group.Key.ToUpper()}", groupFont);
            groupHeader.Alignment = Element.ALIGN_LEFT;
            document.Add(groupHeader);
            document.Add(new Paragraph(" ")); // Spacing

            // Create table for this group
            PdfPTable table = new PdfPTable(4) { WidthPercentage = 100 };
            table.SetWidths(new float[] { 4, 2, 2, 2 });

            // Table headers
            AddTableHeader(table, "Medication", "Re-Order Level", "Quantity on Hand", "Verified Count");

            int groupTotal = 0;

            foreach (var med in group.OrderBy(m => m.Name))
            {
                table.AddCell(new Phrase(med.Name, normalFont));
                table.AddCell(new Phrase(med.MinStockLevel.ToString(), normalFont));
                table.AddCell(new Phrase(med.QuantityInStock.ToString(), normalFont));
                table.AddCell(new Phrase("", normalFont)); // Empty column for verification
                groupTotal += med.QuantityInStock;
            }

            document.Add(table);
            document.Add(new Paragraph(" ")); // Spacing

            // Sub-total
            var subtotal = new Paragraph($"Sub-total: {groupTotal}", boldFont);
            subtotal.Alignment = Element.ALIGN_LEFT;
            document.Add(subtotal);
            document.Add(new Paragraph(" ")); // Spacing
            document.Add(new Paragraph(" ")); // Spacing

            grandTotal += groupTotal;
        }

        // Grand total
        var grandTotalText = new Paragraph($"GRAND TOTAL: {grandTotal}", headerFont);
        grandTotalText.Alignment = Element.ALIGN_CENTER;
        document.Add(grandTotalText);
    }

    private void AddTableHeader(PdfPTable table, params string[] headers)
    {
        foreach (string header in headers)
        {
            PdfPCell cell = new PdfPCell(new Phrase(header))
            {
                BackgroundColor = new BaseColor(221, 221, 221), // Light gray
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

        // Label cell spanning three columns
        PdfPCell labelCell = new PdfPCell(new Phrase(label, font))
        {
            Colspan = 3, // Span 3 columns (Medication, Re-order Level, Quantity on Hand)
            Border = PdfPCell.TOP_BORDER,
            BorderWidthTop = isGrandTotal ? 2f : 1f,
            Padding = 5,
            HorizontalAlignment = Element.ALIGN_LEFT
        };
        table.AddCell(labelCell);

        // Value cell (for the total quantity)
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

// Page event handler for headers and footers
public class PharmacyReportPageEventHandler : PdfPageEventHelper
{
    private PdfTemplate totalPages;
    private BaseFont baseFont;
    private DateTime printDate;

    public override void OnOpenDocument(PdfWriter writer, Document document)
    {
        printDate = DateTime.Now;
        baseFont = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
        totalPages = writer.DirectContent.CreateTemplate(30, 16);
    }

    public override void OnStartPage(PdfWriter writer, Document document)
    {
        if (writer.PageNumber > 1) // Skip header on title page
        {
            PdfContentByte cb = writer.DirectContent;
            cb.BeginText();
            cb.SetFontAndSize(baseFont, 12);
            cb.SetColorFill(BaseColor.BLACK);
            cb.ShowTextAligned(PdfContentByte.ALIGN_CENTER,
                "Pharmacy Manager Report - Stock Take",
                document.PageSize.Width / 2,
                document.PageSize.Height - 40,
                0);
            cb.EndText();
        }
    }

    public override void OnEndPage(PdfWriter writer, Document document)
    {
        base.OnEndPage(writer, document);

        int pageN = writer.PageNumber;
        String text = "Page " + pageN + " of ";
        float len = baseFont.GetWidthPoint(text, 10);

        Rectangle pageSize = document.PageSize;
        PdfContentByte cb = writer.DirectContent;

        // Footer text
        cb.BeginText();
        cb.SetFontAndSize(baseFont, 10);
        cb.SetColorFill(BaseColor.DARK_GRAY);

        // Page number
        cb.ShowTextAligned(PdfContentByte.ALIGN_LEFT,
            $"Generated: {printDate:yyyy-MM-dd HH:mm}",
            pageSize.GetLeft(36),
            pageSize.GetBottom(20),
            0);

        // Page count
        cb.ShowTextAligned(PdfContentByte.ALIGN_RIGHT,
            text,
            pageSize.GetRight(36) - len,
            pageSize.GetBottom(20),
            0);
        cb.EndText();

        // Add total page count
        cb.AddTemplate(totalPages, pageSize.GetRight(36) - len + 1, pageSize.GetBottom(20));
    }

    public override void OnCloseDocument(PdfWriter writer, Document document)
    {
        base.OnCloseDocument(writer, document);
        totalPages.BeginText();
        totalPages.SetFontAndSize(baseFont, 10);
        totalPages.SetColorFill(BaseColor.DARK_GRAY);
        totalPages.ShowText((writer.PageNumber).ToString());
        totalPages.EndText();
    }
}