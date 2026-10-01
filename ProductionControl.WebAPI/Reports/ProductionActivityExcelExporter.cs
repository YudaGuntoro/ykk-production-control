using ClosedXML.Excel;

namespace ProductionControl.WebAPI.Reports;

public sealed record ProductionActivityReportRow(
    DateTime ActivityAt,
    string WorkOrder,
    string LotNo,
    string ProjectNo,
    decimal? Weight,
    string LineCode,
    string ShiftName,
    string OperatorName,
    string EmployeeNo,
    string Activity,
    string Remarks,
    int ActualQty,
    int RejectQty,
    string Status);

public sealed record ProductionActivityReportContext(
    DateTime? StartDate,
    DateTime? EndDate,
    DateTime GeneratedAt);

public sealed class ProductionActivityExcelExporter
{
    private const string SheetName = "Activity Report";
    private const string SummarySheetName = "Summary";
    private const int HeaderRow = 8;
    private const int ColumnCount = 16;
    private const string PrimaryBlue = "#0B5CAD";
    private const string DarkBlue = "#073B73";
    private const string LightBlue = "#EAF4FF";
    private const string LighterBlue = "#F7FBFF";
    private const string BorderBlue = "#B8D7F3";
    private const string TextDark = "#0F172A";
    private const string MutedText = "#475569";
    private static readonly string[] Headers =
    [
        "No",
        "Date",
        "Time",
        "Work Order",
        "Lot No",
        "Project No",
        "Weight",
        "Line",
        "Shift",
        "Operator",
        "Employee No",
        "Activity",
        "Detail",
        "Actual Qty",
        "Reject Qty",
        "Status"
    ];

    public ProductionActivityExcelExporter(string contentRootPath)
    {
    }

    public byte[] Export(IReadOnlyList<ProductionActivityReportRow> rows, ProductionActivityReportContext context)
    {
        using var workbook = CreateTemplateWorkbook();
        var worksheet = workbook.Worksheet(SheetName);

        WriteMetadata(worksheet, rows, context);
        WriteRows(worksheet, rows);
        WriteSummarySheet(workbook, rows, context);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static XLWorkbook CreateTemplateWorkbook()
    {
        var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(SheetName);
        worksheet.Style.Font.FontName = "Aptos";
        worksheet.Style.Font.FontSize = 10;
        worksheet.Style.Font.FontColor = XLColor.FromHtml(TextDark);
        worksheet.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        worksheet.ShowGridLines = false;
        worksheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        worksheet.PageSetup.PagesWide = 1;
        worksheet.PageSetup.Margins.Top = 0.35;
        worksheet.PageSetup.Margins.Bottom = 0.35;
        worksheet.PageSetup.Margins.Left = 0.25;
        worksheet.PageSetup.Margins.Right = 0.25;

        worksheet.Range(1, 1, 1, ColumnCount).Merge();
        worksheet.Cell(1, 1).Value = "PT YKK AP INDONESIA";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 18;
        worksheet.Cell(1, 1).Style.Font.FontColor = XLColor.White;
        worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Row(1).Height = 30;
        worksheet.Range(1, 1, 1, ColumnCount).Style.Fill.BackgroundColor = XLColor.FromHtml(DarkBlue);

        worksheet.Range(2, 1, 2, ColumnCount).Merge();
        worksheet.Cell(2, 1).Value = "Production Activity Report";
        worksheet.Cell(2, 1).Style.Font.Bold = true;
        worksheet.Cell(2, 1).Style.Font.FontSize = 14;
        worksheet.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml(PrimaryBlue);
        worksheet.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Row(2).Height = 24;
        worksheet.Range(2, 1, 2, ColumnCount).Style.Fill.BackgroundColor = XLColor.FromHtml(LightBlue);

        worksheet.Range(4, 1, 5, 3).Merge();
        worksheet.Range(4, 4, 5, 6).Merge();
        worksheet.Range(4, 7, 5, 8).Merge();
        worksheet.Range(4, 9, 5, 10).Merge();
        worksheet.Range(4, 11, 5, 13).Merge();
        worksheet.Range(4, 14, 5, 16).Merge();
        StyleInfoCard(worksheet.Range(4, 1, 5, 3));
        StyleInfoCard(worksheet.Range(4, 4, 5, 6));
        StyleInfoCard(worksheet.Range(4, 7, 5, 8));
        StyleInfoCard(worksheet.Range(4, 9, 5, 10));
        StyleInfoCard(worksheet.Range(4, 11, 5, 13));
        StyleInfoCard(worksheet.Range(4, 14, 5, 16));

        var headerRange = worksheet.Range(HeaderRow, 1, HeaderRow, ColumnCount);
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml(PrimaryBlue);
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.None;
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.None;
        worksheet.Row(HeaderRow).Height = 24;

        for (var index = 0; index < Headers.Length; index++)
        {
            worksheet.Cell(HeaderRow, index + 1).Value = Headers[index];
        }

        worksheet.Columns(1, ColumnCount).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        worksheet.Column(1).Width = 5;
        worksheet.Column(2).Width = 13;
        worksheet.Column(3).Width = 9;
        worksheet.Column(4).Width = 21;
        worksheet.Column(5).Width = 21;
        worksheet.Column(6).Width = 15;
        worksheet.Column(7).Width = 16;
        worksheet.Column(8).Width = 15;
        worksheet.Column(9).Width = 11;
        worksheet.Column(10).Width = 18;
        worksheet.Column(11).Width = 17;
        worksheet.Column(12).Width = 17;
        worksheet.Column(13).Width = 21;
        worksheet.Column(14).Width = 13;
        worksheet.Column(15).Width = 17;
        worksheet.Column(16).Width = 12;
        worksheet.SheetView.FreezeRows(HeaderRow);

        workbook.Worksheets.Add(SummarySheetName);
        return workbook;
    }

    private static void StyleInfoCard(IXLRange range)
    {
        range.Style.Fill.BackgroundColor = XLColor.FromHtml(LighterBlue);
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = XLColor.FromHtml(BorderBlue);
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static void WriteMetadata(
        IXLWorksheet worksheet,
        IReadOnlyCollection<ProductionActivityReportRow> rows,
        ProductionActivityReportContext context)
    {
        WriteMetric(worksheet.Cell(4, 1), "Period", FormatPeriod(context));
        WriteMetric(worksheet.Cell(4, 4), "Generated", context.GeneratedAt.ToString("dd MMM yyyy HH:mm"));
        WriteMetric(worksheet.Cell(4, 7), "Total Activity", rows.Count.ToString("#,##0"));
        WriteMetric(worksheet.Cell(4, 9), "Total Work Order", rows.Select(x => x.WorkOrder).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Count().ToString("#,##0"));
        WriteMetric(worksheet.Cell(4, 11), "Total Actual", rows.Sum(x => x.ActualQty).ToString("#,##0"));
        WriteMetric(worksheet.Cell(4, 14), "Total Reject", rows.Sum(x => x.RejectQty).ToString("#,##0"));
        worksheet.Row(4).Height = 20;
        worksheet.Row(5).Height = 20;
    }

    private static void WriteMetric(IXLCell cell, string label, string value)
    {
        cell.Value = $"{label}\n{value}";
        cell.Style.Alignment.WrapText = true;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        cell.Style.Font.FontColor = XLColor.FromHtml(MutedText);
        cell.Style.Font.Bold = true;
    }

    private static string FormatPeriod(ProductionActivityReportContext context)
    {
        if (context.StartDate.HasValue && context.EndDate.HasValue)
        {
            return $"{context.StartDate.Value:dd MMM yyyy} - {context.EndDate.Value:dd MMM yyyy}";
        }

        if (context.StartDate.HasValue)
        {
            return $"From {context.StartDate.Value:dd MMM yyyy}";
        }

        if (context.EndDate.HasValue)
        {
            return $"Until {context.EndDate.Value:dd MMM yyyy}";
        }

        return "Last 7 days through today";
    }

    private static void WriteRows(IXLWorksheet worksheet, IReadOnlyList<ProductionActivityReportRow> rows)
    {
        for (var index = 0; index < Headers.Length; index++)
        {
            worksheet.Cell(HeaderRow, index + 1).Value = Headers[index];
        }

        var headerRange = worksheet.Range(HeaderRow, 1, HeaderRow, ColumnCount);
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml(PrimaryBlue);
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.None;
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.None;

        var lastUsedRow = worksheet.LastRowUsed()?.RowNumber() ?? HeaderRow;
        if (lastUsedRow > HeaderRow)
        {
            worksheet.Rows(HeaderRow + 1, lastUsedRow).Delete();
        }

        if (rows.Count == 0)
        {
            worksheet.Cell(HeaderRow + 1, 1).Value = "No activity data found.";
            worksheet.Range(HeaderRow + 1, 1, HeaderRow + 1, ColumnCount).Merge();
            worksheet.Range(HeaderRow + 1, 1, HeaderRow + 1, ColumnCount).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Range(HeaderRow + 1, 1, HeaderRow + 1, ColumnCount).Style.Font.Italic = true;
            return;
        }

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var excelRow = HeaderRow + 1 + index;

            worksheet.Cell(excelRow, 1).Value = index + 1;
            worksheet.Cell(excelRow, 2).Value = row.ActivityAt.Date;
            worksheet.Cell(excelRow, 3).Value = row.ActivityAt;
            worksheet.Cell(excelRow, 4).Value = row.WorkOrder;
            worksheet.Cell(excelRow, 5).Value = row.LotNo;
            worksheet.Cell(excelRow, 6).Value = row.ProjectNo;
            worksheet.Cell(excelRow, 7).Value = row.Weight;
            worksheet.Cell(excelRow, 8).Value = row.LineCode;
            worksheet.Cell(excelRow, 9).Value = row.ShiftName;
            worksheet.Cell(excelRow, 10).Value = row.OperatorName;
            worksheet.Cell(excelRow, 11).Value = row.EmployeeNo;
            worksheet.Cell(excelRow, 12).Value = row.Activity;
            worksheet.Cell(excelRow, 13).Value = row.Remarks;
            worksheet.Cell(excelRow, 14).Value = row.ActualQty;
            worksheet.Cell(excelRow, 15).Value = row.RejectQty;
            worksheet.Cell(excelRow, 16).Value = row.Status;
        }

        var dataRange = worksheet.Range(HeaderRow + 1, 1, HeaderRow + rows.Count, ColumnCount);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.OutsideBorderColor = XLColor.FromHtml(BorderBlue);
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        dataRange.Style.Border.InsideBorderColor = XLColor.FromHtml("#DCEBFA");
        dataRange.Style.Alignment.WrapText = false;
        dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        for (var rowNumber = HeaderRow + 1; rowNumber <= HeaderRow + rows.Count; rowNumber++)
        {
            worksheet.Row(rowNumber).Height = 21;
            if ((rowNumber - HeaderRow) % 2 == 0)
            {
                worksheet.Range(rowNumber, 1, rowNumber, ColumnCount).Style.Fill.BackgroundColor = XLColor.FromHtml(LighterBlue);
            }
        }

        worksheet.Column(2).Style.DateFormat.Format = "dd mmm yyyy";
        worksheet.Column(3).Style.DateFormat.Format = "hh:mm";
        worksheet.Column(7).Style.NumberFormat.Format = "#,##0.000";
        worksheet.Columns(14, 15).Style.NumberFormat.Format = "#,##0";
        worksheet.Columns(1, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Columns(7, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        worksheet.Columns(14, 15).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        worksheet.Range(HeaderRow, 1, HeaderRow + rows.Count, ColumnCount).SetAutoFilter();
    }

    private static void WriteSummarySheet(
        XLWorkbook workbook,
        IReadOnlyList<ProductionActivityReportRow> rows,
        ProductionActivityReportContext context)
    {
        var worksheet = workbook.Worksheet(SummarySheetName);
        worksheet.Clear();
        worksheet.Style.Font.FontName = "Aptos";
        worksheet.Style.Font.FontSize = 10;
        worksheet.ShowGridLines = false;

        worksheet.Range(1, 1, 1, 5).Merge();
        worksheet.Cell(1, 1).Value = "Activity Summary";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 16;
        worksheet.Cell(1, 1).Style.Font.FontColor = XLColor.White;
        worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Range(1, 1, 1, 5).Style.Fill.BackgroundColor = XLColor.FromHtml(DarkBlue);

        worksheet.Cell(3, 1).Value = "Period";
        worksheet.Cell(3, 2).Value = FormatPeriod(context);
        worksheet.Cell(4, 1).Value = "Generated";
        worksheet.Cell(4, 2).Value = context.GeneratedAt;
        worksheet.Cell(4, 2).Style.DateFormat.Format = "dd mmm yyyy hh:mm";

        worksheet.Cell(6, 1).Value = "Activity";
        worksheet.Cell(6, 2).Value = "Count";
        worksheet.Cell(6, 3).Value = "Actual Qty";
        worksheet.Cell(6, 4).Value = "Reject Qty";

        var summaryRows = rows
            .GroupBy(x => x.Activity)
            .OrderByDescending(x => x.Count())
            .ThenBy(x => x.Key)
            .ToList();

        for (var index = 0; index < summaryRows.Count; index++)
        {
            var group = summaryRows[index];
            var rowNumber = 7 + index;
            worksheet.Cell(rowNumber, 1).Value = group.Key;
            worksheet.Cell(rowNumber, 2).Value = group.Count();
            worksheet.Cell(rowNumber, 3).Value = group.Sum(x => x.ActualQty);
            worksheet.Cell(rowNumber, 4).Value = group.Sum(x => x.RejectQty);
        }

        var lastSummaryRow = Math.Max(7, 6 + summaryRows.Count);
        worksheet.Range(6, 1, lastSummaryRow, 4).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        worksheet.Range(6, 1, lastSummaryRow, 4).Style.Border.OutsideBorderColor = XLColor.FromHtml(BorderBlue);
        worksheet.Range(6, 1, lastSummaryRow, 4).Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        worksheet.Range(6, 1, lastSummaryRow, 4).Style.Border.InsideBorderColor = XLColor.FromHtml("#DCEBFA");
        worksheet.Range(6, 1, 6, 4).Style.Fill.BackgroundColor = XLColor.FromHtml(PrimaryBlue);
        worksheet.Range(6, 1, 6, 4).Style.Font.FontColor = XLColor.White;
        worksheet.Range(6, 1, 6, 4).Style.Font.Bold = true;
        worksheet.Columns(1, 5).AdjustToContents();
    }
}
