using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sparkle.Api.Services;

public interface IPdfReportService
{
    byte[] GenerateSalesReportPdf(DateTime fromDate, DateTime toDate, IEnumerable<Sparkle.Api.Areas.Admin.Controllers.ReportsController.SalesReportItem> data);
}

public class PdfReportService : IPdfReportService
{
    public PdfReportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateSalesReportPdf(DateTime fromDate, DateTime toDate, IEnumerable<Sparkle.Api.Areas.Admin.Controllers.ReportsController.SalesReportItem> data)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily(Fonts.Arial));

                page.Header().Element(c => ComposeHeader(c, fromDate, toDate));
                page.Content().Element(c => ComposeContent(c, data));
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private void ComposeHeader(IContainer container, DateTime fromDate, DateTime toDate)
    {
        container.Column(column =>
        {
            column.Spacing(10);

            column.Item().Row(row =>
            {
                row.ConstantItem(100).Height(60).Border(1).BorderColor(Colors.Grey.Lighten2)
                    .Padding(5).AlignCenter().AlignMiddle().Text("SPARKLE")
                    .FontSize(20).Bold().FontColor(Colors.Blue.Medium);

                row.RelativeItem().PaddingLeft(20).Column(col =>
                {
                    col.Item().Text("SPARKLE E-COMMERCE").FontSize(24).Bold().FontColor(Colors.Blue.Darken2);
                    col.Item().Text("Sales & Revenue Report").FontSize(14).SemiBold().FontColor(Colors.Grey.Darken3);
                });

                row.ConstantItem(150).AlignRight().Column(col =>
                {
                    col.Item().Background(Colors.Blue.Medium).Padding(8).AlignCenter()
                        .Text("REPORT").FontSize(16).Bold().FontColor(Colors.White);
                    col.Item().PaddingTop(5).AlignRight().Text(text =>
                    {
                        text.Span("Generated: ").SemiBold().FontSize(9);
                        text.Span($"{DateTime.UtcNow:dd MMM yyyy}").FontSize(9);
                    });
                });
            });

            column.Item().PaddingTop(10).Background(Colors.Grey.Lighten3).Padding(10)
                .Row(row =>
                {
                    row.RelativeItem().Text(text =>
                    {
                        text.Span("Report Period: ").SemiBold().FontSize(10);
                        text.Span($"{fromDate:dd MMM yyyy} - {toDate:dd MMM yyyy}").FontSize(10);
                    });
                });

            column.Item().PaddingTop(5).BorderBottom(2).BorderColor(Colors.Blue.Medium);
        });
    }

    private void ComposeContent(IContainer container, IEnumerable<Sparkle.Api.Areas.Admin.Controllers.ReportsController.SalesReportItem> data)
    {
        var dataList = data.ToList();
        var totalRevenue = dataList.Sum(x => x.TotalRevenue);
        var totalOrders = dataList.Sum(x => x.TotalOrders);
        var overallAvg = totalOrders > 0 ? totalRevenue / totalOrders : 0;

        container.PaddingVertical(20).Column(column =>
        {
            column.Spacing(15);

            // Summary Cards
            column.Item().Row(row =>
            {
                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
                {
                    col.Item().Text("Total Revenue").FontSize(10).FontColor(Colors.Grey.Darken1);
                    col.Item().Text($"BDT {totalRevenue:N2}").FontSize(16).Bold().FontColor(Colors.Green.Darken1);
                });

                row.ConstantItem(10);

                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
                {
                    col.Item().Text("Total Orders").FontSize(10).FontColor(Colors.Grey.Darken1);
                    col.Item().Text($"{totalOrders}").FontSize(16).Bold().FontColor(Colors.Blue.Darken1);
                });

                row.ConstantItem(10);

                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
                {
                    col.Item().Text("Average Order Value").FontSize(10).FontColor(Colors.Grey.Darken1);
                    col.Item().Text($"BDT {overallAvg:N2}").FontSize(16).Bold().FontColor(Colors.Grey.Darken3);
                });
            });

            // Data Table
            column.Item().PaddingTop(10).Element(c => ComposeTable(c, dataList));
        });
    }

    private void ComposeTable(IContainer container, List<Sparkle.Api.Areas.Admin.Controllers.ReportsController.SalesReportItem> data)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2); // Date
                columns.RelativeColumn(2); // Orders
                columns.RelativeColumn(3); // Revenue
                columns.RelativeColumn(3); // Avg Value
            });

            table.Header(header =>
            {
                header.Cell().Element(CellStyle).Text("Date");
                header.Cell().Element(CellStyle).AlignRight().Text("Orders");
                header.Cell().Element(CellStyle).AlignRight().Text("Revenue (BDT)");
                header.Cell().Element(CellStyle).AlignRight().Text("Avg Order (BDT)");

                static IContainer CellStyle(IContainer container)
                {
                    return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
                }
            });

            foreach (var item in data)
            {
                table.Cell().Element(CellStyle).Text(item.Date.ToString("MMM dd, yyyy"));
                table.Cell().Element(CellStyle).AlignRight().Text(item.TotalOrders.ToString());
                table.Cell().Element(CellStyle).AlignRight().Text($"{item.TotalRevenue:N2}").FontColor(Colors.Green.Darken1);
                table.Cell().Element(CellStyle).AlignRight().Text($"{item.AverageOrderValue:N2}");

                static IContainer CellStyle(IContainer container)
                {
                    return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);
                }
            }
        });
    }
}
