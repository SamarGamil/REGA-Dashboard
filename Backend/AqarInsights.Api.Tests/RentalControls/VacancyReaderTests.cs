using ClosedXML.Excel;
using AqarInsights.Api.Services.RentalControls.Reading;
using AqarInsights.Api.Models.RentalControls.Raw;

namespace AqarInsights.Api.Tests.RentalControls;

public class VacancyReaderTests
{
    [Fact]
    public void Read_SkipsCitiesNotInMap()
    {
        using var wb = new XLWorkbook();
        var sheet = wb.AddWorksheet("التغير الإيجاري (الزيادة)");

        // Header - real column names
        sheet.Cell(1, 1).Value = "السنة";
        sheet.Cell(1, 2).Value = "الشهر";
        sheet.Cell(1, 3).Value = "المستوى الجغرافي";
        sheet.Cell(1, 4).Value = "المدينة";
        sheet.Cell(1, 5).Value = "التغير الإيجاري (عام سكني) %";

        // Unknown city
        sheet.Cell(2, 1).Value = 2023;
        sheet.Cell(2, 2).Value = 1;
        sheet.Cell(2, 3).Value = "مدينة";
        sheet.Cell(2, 4).Value = "مدينة غير معروفة";
        sheet.Cell(2, 5).Value = 2.5;

        var cityMap = new Dictionary<string, string>
        {
            ["الرياض"] = "riyadh"
        };

        var points = VacancyReader.Read(wb, cityMap);

        Assert.Empty(points);
    }
    
    [Fact]
    public void Read_ParsesRentalChangeSheet_NoFilters()
    {
        using var wb = new XLWorkbook();
        var sheet = wb.AddWorksheet("التغير الإيجاري (الزيادة)");
        
        // Header - real column names
        sheet.Cell(1, 1).Value = "السنة";
        sheet.Cell(1, 2).Value = "الشهر";
        sheet.Cell(1, 3).Value = "المستوى الجغرافي";
        sheet.Cell(1, 4).Value = "المدينة";
        sheet.Cell(1, 5).Value = "التغير الإيجاري (عام سكني) %";
        
        // Data row
        sheet.Cell(2, 1).Value = 2024;
        sheet.Cell(2, 2).Value = 3;  // Month 3
        sheet.Cell(2, 3).Value = "مدينة";
        sheet.Cell(2, 4).Value = "الرياض";
        sheet.Cell(2, 5).Value = 2.5;
        
        var cityMap = new Dictionary<string, string>
        {
            ["الرياض"] = "riyadh"
        };
        
        var points = VacancyReader.Read(wb, cityMap);
        
        Assert.Single(points);
        var p = points[0];
        Assert.Equal("rentalChange", p.Indicator);
        Assert.Equal("riyadh", p.CityId);
        Assert.Equal(2024, p.Year);
        Assert.Equal(3, p.Period);  // Month
        Assert.Equal(2.5, p.Value);  // No scale
    }

    [Fact]
    public void Read_ParsesRentalVacancySheet_CombinedPeriod_WholePercent()
    {
        using var wb = new XLWorkbook();
        var sheet = wb.AddWorksheet("الشواغر الايجارية");

        // Header (period is a combined YYYY-MM string; no separate السنة column)
        sheet.Cell(1, 1).Value = "الفترة";
        sheet.Cell(1, 2).Value = "المدينة \\ المحافظة";
        sheet.Cell(1, 3).Value = "معدل الشواغر";

        sheet.Cell(2, 1).Value = "2025-04";
        sheet.Cell(2, 2).Value = "الرياض";
        sheet.Cell(2, 3).Value = 0.07451;   // 7.451% → 7

        sheet.Cell(3, 1).Value = "2025-12";
        sheet.Cell(3, 2).Value = "الرياض";
        sheet.Cell(3, 3).Value = 0.081;      // 8.1% → 8

        sheet.Cell(4, 1).Value = "2026-06";
        sheet.Cell(4, 2).Value = "الرياض";
        sheet.Cell(4, 3).Value = 0.066;      // 6.6% → 7

        var cityMap = new Dictionary<string, string> { ["الرياض"] = "riyadh" };

        var vac = VacancyReader.Read(wb, cityMap).Where(p => p.Indicator == "vacancy").ToList();

        Assert.Equal(3, vac.Count);
        Assert.All(vac, p => Assert.Equal("riyadh", p.CityId));
        Assert.Equal(7, vac.Single(p => p.Year == 2025 && p.Period == 4).Value);
        Assert.Equal(8, vac.Single(p => p.Year == 2025 && p.Period == 12).Value);
        Assert.Equal(7, vac.Single(p => p.Year == 2026 && p.Period == 6).Value);
    }

    [Fact]
    public void Read_RentalVacancy_RepeatedCityMonthsMustAgree()
    {
        using var wb = new XLWorkbook();
        var sheet = wb.AddWorksheet("الشواغر الايجارية");
        sheet.Cell(1, 1).Value = "الفترة";
        sheet.Cell(1, 2).Value = "المدينة \\ المحافظة";
        sheet.Cell(1, 3).Value = "معدل الشواغر";
        // The sheet repeats some city-months; equal repeats are fine, different ones are an error.
        sheet.Cell(2, 1).Value = "2026-06"; sheet.Cell(2, 2).Value = "الرياض"; sheet.Cell(2, 3).Value = 0.07;
        sheet.Cell(3, 1).Value = "2026-06"; sheet.Cell(3, 2).Value = "الرياض"; sheet.Cell(3, 3).Value = 0.07;
        var cityMap = new Dictionary<string, string> { ["الرياض"] = "riyadh" };
        Assert.Equal(2, VacancyReader.Read(wb, cityMap).Count(p => p.Indicator == "vacancy"));

        sheet.Cell(3, 3).Value = 0.09;

        var ex = Assert.Throws<RentalControlsReadException>(() => VacancyReader.Read(wb, cityMap));
        Assert.Contains("riyadh", ex.Message);
        Assert.Contains("2026-06", ex.Message);
    }

    [Fact]
    public void Read_RentalVacancy_SkipsUnknownCitiesAndOutOfWindowYears()
    {
        using var wb = new XLWorkbook();
        var sheet = wb.AddWorksheet("الشواغر الايجارية");
        sheet.Cell(1, 1).Value = "الفترة";
        sheet.Cell(1, 2).Value = "المدينة \\ المحافظة";
        sheet.Cell(1, 3).Value = "معدل الشواغر";

        // Unknown city
        sheet.Cell(2, 1).Value = "2025-06";
        sheet.Cell(2, 2).Value = "مدينة غير معروفة";
        sheet.Cell(2, 3).Value = 0.05;
        // Out-of-window year
        sheet.Cell(3, 1).Value = "2019-06";
        sheet.Cell(3, 2).Value = "الرياض";
        sheet.Cell(3, 3).Value = 0.05;

        var cityMap = new Dictionary<string, string> { ["الرياض"] = "riyadh" };

        var vac = VacancyReader.Read(wb, cityMap).Where(p => p.Indicator == "vacancy").ToList();

        Assert.Empty(vac);
    }
}
