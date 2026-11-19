using AqarInsights.Api.Models.ForeignOwnership;
using AqarInsights.Api.Services.ForeignOwnership;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging.Abstractions;
using System.Globalization;
using System.Text.Json;

namespace AqarInsights.Api.Tests;

// Guards deterministic, culture-invariant parsing of text-typed date cells in the
// foreign-ownership importer (mixed day-first / month-first / ISO / dotted formats).
public class ExcelConverterServiceDateTests
{
    private static string WriteWorkbook(string dir, params string[] textDates)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("الشراء");
        // Header at row 2.
        ws.Cell(2, 1).Value = "تاريخ التنفيذ";
        ws.Cell(2, 2).Value = "مدينة العقار";
        ws.Cell(2, 3).Value = "قيمة الصفقة";
        for (var i = 0; i < textDates.Length; i++)
        {
            var r = 3 + i;
            ws.Cell(r, 1).Value = textDates[i]; // assigned as text — stays XLDataType.Text
            ws.Cell(r, 2).Value = "الرياض";
            ws.Cell(r, 3).Value = 100000;
        }
        var path = Path.Combine(dir, "test.xlsx");
        wb.SaveAs(path);
        return path;
    }

    private static async Task<List<TransactionRecord>> Convert(string[] dates)
    {
        var dir = Path.Combine(Path.GetTempPath(), "conv-date-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WriteWorkbook(dir, dates);
            var svc = new ExcelConverterService(dir, dir, Path.Combine(dir, "missing-filter.json"),
                NullLogger<ExcelConverterService>.Instance);
            var report = await svc.ConvertLatestAsync();
            Assert.True(report.Success, string.Join(", ", report.Errors));
            var json = await File.ReadAllTextAsync(Path.Combine(dir, "transactions.json"));
            return JsonSerializer.Deserialize<List<TransactionRecord>>(json,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData("20/5/2026", "2026-05-20")]   // day-first, month unambiguous
    [InlineData("18/5/2026", "2026-05-18")]   // day-first
    [InlineData("6/14/2026", "2026-06-14")]   // month-first fallback (14 invalid as month)
    [InlineData("5/18/2026", "2026-05-18")]   // month-first fallback
    [InlineData("2026-06-8", "2026-06-08")]   // ISO, single-digit day
    [InlineData("17-5-2026", "2026-05-17")]   // day-first, dashes
    [InlineData("10.6.2026", "2026-06-10")]   // day-first, dots
    [InlineData("14\\6\\2026", "2026-06-14")]  // day-first, backslashes
    [InlineData("3/4/2026", "2026-04-03")]    // both <=12 -> day-first wins
    public async Task Parses_text_dates_deterministically(string input, string expectedIso)
    {
        var records = await Convert(new[] { input });
        Assert.Single(records);
        Assert.Equal(expectedIso, records[0].Date!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Unparseable_text_date_yields_null()
    {
        var records = await Convert(new[] { "not-a-date" });
        Assert.Single(records);
        Assert.Null(records[0].Date);
    }

    // Writes real DateTime-typed cells; the file name carries the report date (leading yyyyMMdd).
    private static async Task<List<TransactionRecord>> ConvertDated(string fileName, params DateTime[] dateCells)
    {
        var dir = Path.Combine(Path.GetTempPath(), "conv-date-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            using (var wb = new XLWorkbook())
            {
                var ws = wb.AddWorksheet("الشراء");
                ws.Cell(2, 1).Value = "تاريخ التنفيذ";
                ws.Cell(2, 2).Value = "مدينة العقار";
                ws.Cell(2, 3).Value = "قيمة الصفقة";
                for (var i = 0; i < dateCells.Length; i++)
                {
                    var r = 3 + i;
                    ws.Cell(r, 1).Value = dateCells[i]; // DateTime-typed cell
                    ws.Cell(r, 2).Value = "الرياض";
                    ws.Cell(r, 3).Value = 100000;
                }
                wb.SaveAs(Path.Combine(dir, fileName));
            }
            var svc = new ExcelConverterService(dir, dir, Path.Combine(dir, "missing-filter.json"),
                NullLogger<ExcelConverterService>.Instance);
            var report = await svc.ConvertLatestAsync();
            Assert.True(report.Success, string.Join(", ", report.Errors));
            var json = await File.ReadAllTextAsync(Path.Combine(dir, "transactions.json"));
            return JsonSerializer.Deserialize<List<TransactionRecord>>(json,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string Iso(TransactionRecord r) =>
        r.Date!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    [Fact]
    public async Task Future_dated_datetime_is_rejected()
    {
        // Report 2026-06-15; source cells dated after it are data errors -> date rejected (null).
        var records = await ConvertDated("20260615_report.xlsx",
            new DateTime(2026, 10, 5), new DateTime(2026, 11, 6));
        Assert.Null(records[0].Date);
        Assert.Null(records[1].Date);
    }

    [Fact]
    public async Task Dates_within_report_window_are_unchanged()
    {
        var records = await ConvertDated("20260615_report.xlsx",
            new DateTime(2026, 6, 10), new DateTime(2026, 4, 3));
        Assert.Equal("2026-06-10", Iso(records[0]));
        Assert.Equal("2026-04-03", Iso(records[1]));
    }

    [Fact]
    public async Task Future_date_beyond_report_is_rejected()
    {
        var records = await ConvertDated("20260615_report.xlsx", new DateTime(2026, 7, 20));
        Assert.Null(records[0].Date);
    }

    [Fact]
    public async Task No_report_date_in_filename_disables_correction()
    {
        var records = await ConvertDated("report.xlsx", new DateTime(2026, 10, 5));
        Assert.Equal("2026-10-05", Iso(records[0]));
    }
}
