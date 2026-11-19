using AqarInsights.Api.Models;
using AqarInsights.Api.Models.ForeignOwnership;
using AqarInsights.Api.Services.ForeignOwnership;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace AqarInsights.Api.Tests;

// The RER Individual/Company extract: 4 title rows, Arabic header on row 5, owner type + share columns.
public class ExcelConverterServiceRerTemplateTests
{
    private static readonly string[] Headers =
    [
        "رقم العقار", "اسم المالك الجديد", "رقم هوية المالك الجديد", "جنسية المالك الجديد", "نوع العملية",
        "مصدر العملية الفرع", "تاريخ تنفيذ العملية", "نوع العقار", "المساحة", "قيمة الصفقة",
        "مدينة العقار", "الحي", "هل يوجد موافقة من قبل وزارة الداخلية", "رقم الهاتف",
        "Company vs. Individual", "Transaction Share",
    ];

    private static Task<(List<TransactionRecord> Records, ConversionReport Report)> Convert(
        params (string Ren, string Nationality, string Owner, double Share, double Value)[] rows)
        => Convert("شقة سكنية", rows);

    private static async Task<(List<TransactionRecord> Records, ConversionReport Report)> Convert(string propertyType,
        params (string Ren, string Nationality, string Owner, double Share, double Value)[] rows)
    {
        var dir = Path.Combine(Path.GetTempPath(), "conv-rer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            using (var wb = new XLWorkbook())
            {
                var ws = wb.AddWorksheet("الشراء");
                ws.Cell(1, 1).Value = "شراء مقيم اجنبي للعقار بغرض السكن";
                ws.Cell(2, 1).Value = "Purchase of real estate by a foreign resident for residential purposes";
                ws.Cell(3, 1).Value = "RERInd_REN_E & RERComp_REN_E";
                ws.Cell(3, 11).Value = "RERInd_City Name_Q & RERComp_City Name_Q";
                ws.Cell(4, 1).Value = "Property Number";
                ws.Cell(4, 11).Value = "Property City";
                for (var c = 0; c < Headers.Length; c++) ws.Cell(5, c + 1).Value = Headers[c];
                for (var i = 0; i < rows.Length; i++)
                {
                    var r = 6 + i;
                    ws.Cell(r, 1).Value = rows[i].Ren;
                    ws.Cell(r, 4).Value = rows[i].Nationality;
                    ws.Cell(r, 5).Value = "شراء";
                    ws.Cell(r, 7).Value = new DateTime(2026, 9, 1);
                    ws.Cell(r, 8).Value = propertyType;
                    ws.Cell(r, 9).Value = 120;
                    ws.Cell(r, 10).Value = rows[i].Value;
                    ws.Cell(r, 11).Value = "الرياض";
                    ws.Cell(r, 12).Value = "الملقا";
                    ws.Cell(r, 15).Value = rows[i].Owner;
                    ws.Cell(r, 16).Value = rows[i].Share;
                }
                wb.SaveAs(Path.Combine(dir, "20261027_RER Individual Company Extract.xlsx"));
            }
            var svc = new ExcelConverterService(dir, dir, Path.Combine(dir, "missing-filter.json"),
                NullLogger<ExcelConverterService>.Instance);
            var report = await svc.ConvertLatestAsync();
            Assert.True(report.Success, string.Join(", ", report.Errors));
            var json = await File.ReadAllTextAsync(Path.Combine(dir, "transactions.json"));
            var records = JsonSerializer.Deserialize<List<TransactionRecord>>(json,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;
            return (records, report);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Detects_header_on_row_5_and_reads_data_from_row_6()
    {
        var (records, _) = await Convert(("111", "مصر", "RER Individual", 1, 500000));
        var r = Assert.Single(records);
        Assert.Equal("الرياض", r.City);
        Assert.Equal("مصر", r.Nationality);
        Assert.Equal(500000m, r.Value);
        Assert.Equal("2026-09", r.Month);
    }

    [Theory]
    [InlineData("RER Individual", OwnerTypes.Individual)]
    [InlineData("RER Company", OwnerTypes.Company)]
    [InlineData("", "")]
    public async Task Maps_owner_type(string raw, string expected)
    {
        var (records, _) = await Convert(("111", "مصر", raw, 1, 1));
        Assert.Equal(expected, Assert.Single(records).OwnerType);
    }

    [Fact]
    public async Task Reads_transaction_share()
    {
        var (records, _) = await Convert(("111", "مصر", "RER Individual", 0.5, 250000));
        Assert.Equal(0.5m, Assert.Single(records).TransactionShare);
    }

    [Fact]
    public async Task Company_partner_category_maps_to_company_nationality_without_warning()
    {
        var (records, report) = await Convert(("9876543210", "Mixed", "RER Company", 1, 1));
        Assert.Equal("شركة مختلطة", Assert.Single(records).Nationality);
        Assert.DoesNotContain(report.Warnings, x => x.Issue.Contains("Mixed"));
    }

    [Fact]
    public async Task Unknown_latin_nationality_becomes_unspecified_with_a_warning()
    {
        var (records, report) = await Convert(("9876543210", "Other", "RER Company", 1, 1));
        Assert.Equal("غير محدد", Assert.Single(records).Nationality);
        // No geography file in the test, so an unmapped-city warning is also emitted.
        var w = Assert.Single(report.Warnings, x => x.Issue.Contains("Other"));
        Assert.Equal("الشراء", w.Sheet);
        Assert.Equal(6, w.Row);
        Assert.Contains("9876543210", w.Issue);
    }

    [Theory]
    [InlineData("مبنى سكني", PropertyCategories.ResidentialBuilding)]
    [InlineData("مبنى سكني/تجاري", PropertyCategories.MixedUse)]
    [InlineData("أرض فضاء", PropertyCategories.VacantLand)]
    [InlineData("شقة سكنية", PropertyCategories.ResidentialApartment)]
    [InlineData("مستودع", PropertyCategories.Other)]
    public async Task Maps_property_type(string raw, string expected)
    {
        var (records, _) = await Convert(raw, ("111", "مصر", "RER Individual", 1, 1));
        Assert.Equal(expected, Assert.Single(records).PropertyType);
    }
}
