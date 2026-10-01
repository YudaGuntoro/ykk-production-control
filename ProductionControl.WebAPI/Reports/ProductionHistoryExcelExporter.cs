using ClosedXML.Excel;
using ProductionControl.Domain.Production;

namespace ProductionControl.WebAPI.Reports;

public sealed record ProductionHistoryExportContext(
    DateTime GeneratedAt,
    string Title);

public sealed class ProductionHistoryExcelExporter
{
    private const string SheetName = "Production History";
    private const string DetailSheetName = "Production Detail";
    private const int HeaderRow = 5;
    private const int ColumnCount = 14;
    private const int DetailColumnCount = 8;
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
        "Project No",
        "Project Name",
        "Order No",
        "Lot No",
        "Weight",
        "Line",
        "Status",
        "Scanned Date",
        "Start WO",
        "Finish WO",
        "Operators",
        "Start Operators",
        "Finish Operators"
    ];

    public byte[] Export(IReadOnlyList<CuttingListResponse> rows, ProductionHistoryExportContext context)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(SheetName);

        worksheet.Style.Font.FontName = "Aptos";
        worksheet.Style.Font.FontSize = 10;
        worksheet.Style.Font.FontColor = XLColor.FromHtml(TextDark);
        worksheet.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        worksheet.ShowGridLines = false;
        worksheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        worksheet.PageSetup.PagesWide = 1;

        worksheet.Range(1, 1, 1, ColumnCount).Merge();
        worksheet.Cell(1, 1).Value = "PT YKK AP INDONESIA";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 18;
        worksheet.Cell(1, 1).Style.Font.FontColor = XLColor.White;
        worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Range(1, 1, 1, ColumnCount).Style.Fill.BackgroundColor = XLColor.FromHtml(DarkBlue);

        worksheet.Range(2, 1, 2, ColumnCount).Merge();
        worksheet.Cell(2, 1).Value = context.Title;
        worksheet.Cell(2, 1).Style.Font.Bold = true;
        worksheet.Cell(2, 1).Style.Font.FontSize = 14;
        worksheet.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml(PrimaryBlue);
        worksheet.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Range(2, 1, 2, ColumnCount).Style.Fill.BackgroundColor = XLColor.FromHtml(LightBlue);

        worksheet.Range(3, 1, 3, 4).Merge();
        worksheet.Cell(3, 1).Value = $"Generated: {context.GeneratedAt:dd MMM yyyy HH:mm}";
        worksheet.Cell(3, 1).Style.Font.Bold = true;
        worksheet.Cell(3, 1).Style.Font.FontColor = XLColor.FromHtml(MutedText);
        worksheet.Range(3, 6, 3, 9).Merge();
        worksheet.Cell(3, 6).Value = $"Total Rows: {rows.Count:#,##0}";
        worksheet.Cell(3, 6).Style.Font.Bold = true;
        worksheet.Cell(3, 6).Style.Font.FontColor = XLColor.FromHtml(MutedText);

        for (var index = 0; index < Headers.Length; index++)
        {
            worksheet.Cell(HeaderRow, index + 1).Value = Headers[index];
        }

        var headerRange = worksheet.Range(HeaderRow, 1, HeaderRow, ColumnCount);
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml(PrimaryBlue);
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        if (rows.Count == 0)
        {
            worksheet.Cell(HeaderRow + 1, 1).Value = "No production history data found.";
            worksheet.Range(HeaderRow + 1, 1, HeaderRow + 1, ColumnCount).Merge();
            worksheet.Range(HeaderRow + 1, 1, HeaderRow + 1, ColumnCount).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Range(HeaderRow + 1, 1, HeaderRow + 1, ColumnCount).Style.Font.Italic = true;
        }
        else
        {
            WriteRows(worksheet, rows);
        }

        worksheet.Columns(1, ColumnCount).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        worksheet.Columns(1, ColumnCount).AdjustToContents();
        worksheet.Column(6).Style.NumberFormat.Format = "#,##0.000";
        worksheet.Columns(9, 11).Style.DateFormat.Format = "dd mmm yyyy hh:mm";
        worksheet.Column(6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        worksheet.Columns(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.SheetView.FreezeRows(HeaderRow);
        worksheet.Range(HeaderRow, 1, HeaderRow + Math.Max(rows.Count, 1), ColumnCount).SetAutoFilter();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] ExportDetail(CuttingListResponse row, ProductionHistoryExportContext context)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(DetailSheetName);

        worksheet.Style.Font.FontName = "Aptos";
        worksheet.Style.Font.FontSize = 10;
        worksheet.Style.Font.FontColor = XLColor.FromHtml(TextDark);
        worksheet.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        worksheet.ShowGridLines = false;
        worksheet.PageSetup.PageOrientation = XLPageOrientation.Portrait;
        worksheet.PageSetup.PagesWide = 1;

        worksheet.Range(1, 1, 1, DetailColumnCount).Merge();
        worksheet.Cell(1, 1).Value = "PT YKK AP INDONESIA";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 18;
        worksheet.Cell(1, 1).Style.Font.FontColor = XLColor.White;
        worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Range(1, 1, 1, DetailColumnCount).Style.Fill.BackgroundColor = XLColor.FromHtml(DarkBlue);

        worksheet.Range(2, 1, 2, DetailColumnCount).Merge();
        worksheet.Cell(2, 1).Value = context.Title;
        worksheet.Cell(2, 1).Style.Font.Bold = true;
        worksheet.Cell(2, 1).Style.Font.FontSize = 14;
        worksheet.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml(PrimaryBlue);
        worksheet.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Range(2, 1, 2, DetailColumnCount).Style.Fill.BackgroundColor = XLColor.FromHtml(LightBlue);

        worksheet.Range(3, 1, 3, 4).Merge();
        worksheet.Cell(3, 1).Value = $"Generated: {context.GeneratedAt:dd MMM yyyy HH:mm}";
        worksheet.Cell(3, 1).Style.Font.Bold = true;
        worksheet.Cell(3, 1).Style.Font.FontColor = XLColor.FromHtml(MutedText);
        worksheet.Range(3, 5, 3, 8).Merge();
        worksheet.Cell(3, 5).Value = $"Lot No: {row.LotNo ?? "-"}";
        worksheet.Cell(3, 5).Style.Font.Bold = true;
        worksheet.Cell(3, 5).Style.Font.FontColor = XLColor.FromHtml(MutedText);

        WriteDetailSectionTitle(worksheet, 5, "Production Information");
        WriteDetailField(worksheet, 6, 1, "Status", row.Status.ToString().Replace("_", " "));
        WriteDetailField(worksheet, 6, 5, "Project No", row.ProjectNo ?? "-");
        WriteDetailField(worksheet, 7, 1, "Project Name", row.ProjectName ?? "-");
        WriteDetailField(worksheet, 7, 5, "Order No", row.OrderNumber ?? "-");
        WriteDetailField(worksheet, 8, 1, "Lot No", row.LotNo ?? "-");
        WriteDetailField(worksheet, 8, 5, "Weight", row.Weight?.ToString("#,##0.000") ?? "-");
        WriteDetailField(worksheet, 9, 1, "Line", row.LineCode);

        WriteDetailSectionTitle(worksheet, 12, "Timeline");
        WriteTimelineHeader(worksheet, 13);
        WriteTimelineRow(worksheet, 14, "Scanned Date", row.CreatedAt);
        WriteTimelineRow(worksheet, 15, "Start WO", row.StartedAt);
        WriteTimelineRow(worksheet, 16, "Finish WO", row.CompletedAt);

        WriteOperatorSection(worksheet, 18, "Active Operators", row.Operators);
        var nextStartRow = 21 + Math.Max(row.Operators.Count, 1);
        WriteOperatorSection(worksheet, nextStartRow, "Start Operators", row.StartOperators);
        nextStartRow += 3 + Math.Max(row.StartOperators.Count, 1);
        WriteOperatorSection(worksheet, nextStartRow, "Finish Operators", row.FinishOperators);

        var usedRange = worksheet.RangeUsed();
        if (usedRange is not null)
        {
            usedRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            usedRange.Style.Border.OutsideBorderColor = XLColor.FromHtml(BorderBlue);
        }

        worksheet.Columns(1, DetailColumnCount).AdjustToContents();
        worksheet.Columns(1, DetailColumnCount).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        worksheet.Columns(3, 4).Width = 18;
        worksheet.Columns(7, 8).Width = 18;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteRows(IXLWorksheet worksheet, IReadOnlyList<CuttingListResponse> rows)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var excelRow = HeaderRow + 1 + index;

            worksheet.Cell(excelRow, 1).Value = index + 1;
            worksheet.Cell(excelRow, 2).Value = row.ProjectNo ?? "-";
            worksheet.Cell(excelRow, 3).Value = row.ProjectName ?? "-";
            worksheet.Cell(excelRow, 4).Value = row.OrderNumber ?? "-";
            worksheet.Cell(excelRow, 5).Value = row.LotNo ?? "-";
            worksheet.Cell(excelRow, 6).Value = row.Weight;
            worksheet.Cell(excelRow, 7).Value = row.LineCode;
            worksheet.Cell(excelRow, 8).Value = row.Status.ToString().Replace("_", " ");
            worksheet.Cell(excelRow, 9).Value = row.CreatedAt;
            worksheet.Cell(excelRow, 10).Value = row.StartedAt;
            worksheet.Cell(excelRow, 11).Value = row.CompletedAt;
            worksheet.Cell(excelRow, 12).Value = FormatOperators(row.Operators);
            worksheet.Cell(excelRow, 13).Value = FormatOperators(row.StartOperators);
            worksheet.Cell(excelRow, 14).Value = FormatOperators(row.FinishOperators);

            if (index % 2 == 1)
            {
                worksheet.Range(excelRow, 1, excelRow, ColumnCount).Style.Fill.BackgroundColor = XLColor.FromHtml(LighterBlue);
            }
        }

        var dataRange = worksheet.Range(HeaderRow + 1, 1, HeaderRow + rows.Count, ColumnCount);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.OutsideBorderColor = XLColor.FromHtml(BorderBlue);
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        dataRange.Style.Border.InsideBorderColor = XLColor.FromHtml("#DCEBFA");
        dataRange.Style.Alignment.WrapText = true;
    }

    private static string FormatOperators(IReadOnlyList<ProductionOperatorResponse> operators)
    {
        if (operators.Count == 0)
        {
            return "-";
        }

        return string.Join(Environment.NewLine, operators.Select(operatorItem =>
            $"{operatorItem.EmployeeNo} - {operatorItem.FullName}"));
    }

    private static void WriteDetailSectionTitle(IXLWorksheet worksheet, int row, string title)
    {
        worksheet.Range(row, 1, row, DetailColumnCount).Merge();
        worksheet.Cell(row, 1).Value = title;
        worksheet.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromHtml(PrimaryBlue);
        worksheet.Cell(row, 1).Style.Font.FontColor = XLColor.White;
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        worksheet.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
    }

    private static void WriteDetailField(IXLWorksheet worksheet, int row, int column, string label, string value)
    {
        worksheet.Range(row, column, row, column + 1).Merge();
        worksheet.Cell(row, column).Value = label;
        worksheet.Cell(row, column).Style.Fill.BackgroundColor = XLColor.FromHtml(LighterBlue);
        worksheet.Cell(row, column).Style.Font.Bold = true;
        worksheet.Cell(row, column).Style.Font.FontColor = XLColor.FromHtml(MutedText);

        worksheet.Range(row, column + 2, row, column + 3).Merge();
        worksheet.Cell(row, column + 2).Value = value;
        worksheet.Cell(row, column + 2).Style.Font.Bold = true;

        var range = worksheet.Range(row, column, row, column + 3);
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = XLColor.FromHtml(BorderBlue);
        range.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        range.Style.Border.InsideBorderColor = XLColor.FromHtml("#DCEBFA");
    }

    private static void WriteTimelineHeader(IXLWorksheet worksheet, int row)
    {
        worksheet.Range(row, 1, row, 4).Merge();
        worksheet.Cell(row, 1).Value = "Activity";
        worksheet.Range(row, 5, row, 8).Merge();
        worksheet.Cell(row, 5).Value = "Date Time";
        worksheet.Range(row, 1, row, DetailColumnCount).Style.Fill.BackgroundColor = XLColor.FromHtml(LightBlue);
        worksheet.Range(row, 1, row, DetailColumnCount).Style.Font.Bold = true;
        worksheet.Range(row, 1, row, DetailColumnCount).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static void WriteTimelineRow(IXLWorksheet worksheet, int row, string label, DateTime? value)
    {
        worksheet.Range(row, 1, row, 4).Merge();
        worksheet.Cell(row, 1).Value = label;
        worksheet.Range(row, 5, row, 8).Merge();
        worksheet.Cell(row, 5).Value = value.HasValue ? value.Value.ToString("dd MMM yyyy HH:mm") : "-";

        var range = worksheet.Range(row, 1, row, DetailColumnCount);
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = XLColor.FromHtml(BorderBlue);
        range.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        range.Style.Border.InsideBorderColor = XLColor.FromHtml("#DCEBFA");
    }

    private static void WriteOperatorSection(IXLWorksheet worksheet, int startRow, string title, IReadOnlyList<ProductionOperatorResponse> operators)
    {
        WriteDetailSectionTitle(worksheet, startRow, title);
        worksheet.Cell(startRow + 1, 1).Value = "No";
        worksheet.Cell(startRow + 1, 2).Value = "Employee No";
        worksheet.Range(startRow + 1, 3, startRow + 1, 8).Merge();
        worksheet.Cell(startRow + 1, 3).Value = "Full Name";
        worksheet.Range(startRow + 1, 1, startRow + 1, DetailColumnCount).Style.Fill.BackgroundColor = XLColor.FromHtml(LightBlue);
        worksheet.Range(startRow + 1, 1, startRow + 1, DetailColumnCount).Style.Font.Bold = true;
        worksheet.Range(startRow + 1, 1, startRow + 1, DetailColumnCount).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        if (operators.Count == 0)
        {
            worksheet.Range(startRow + 2, 1, startRow + 2, DetailColumnCount).Merge();
            worksheet.Cell(startRow + 2, 1).Value = "No operator data.";
            worksheet.Cell(startRow + 2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Cell(startRow + 2, 1).Style.Font.Italic = true;
        }
        else
        {
            for (var index = 0; index < operators.Count; index++)
            {
                var operatorItem = operators[index];
                var row = startRow + 2 + index;
                worksheet.Cell(row, 1).Value = index + 1;
                worksheet.Cell(row, 2).Value = operatorItem.EmployeeNo;
                worksheet.Range(row, 3, row, 8).Merge();
                worksheet.Cell(row, 3).Value = operatorItem.FullName;
            }
        }

        var endRow = startRow + 2 + Math.Max(operators.Count, 1) - 1;
        var range = worksheet.Range(startRow + 1, 1, endRow, DetailColumnCount);
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = XLColor.FromHtml(BorderBlue);
        range.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        range.Style.Border.InsideBorderColor = XLColor.FromHtml("#DCEBFA");
        range.Style.Alignment.WrapText = true;
    }
}
