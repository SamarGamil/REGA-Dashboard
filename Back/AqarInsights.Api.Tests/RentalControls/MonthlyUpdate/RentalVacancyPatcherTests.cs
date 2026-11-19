using ClosedXML.Excel;
using AqarInsights.Api.Services.RentalControls.MonthlyUpdate;
using AqarInsights.Api.Services.RentalControls.Reading;
using AqarInsights.Api.Services.RentalControls.Snapshots;

namespace AqarInsights.Api.Tests.RentalControls.MonthlyUpdate;

public class RentalVacancyPatcherTests
{
    [Fact]
    public void Append_AddsOnlyNewMonths_AndVacancyReaderReadsThem()
    {
        using var vacancy = VacancyWorkbook(("2026-06", "الرياض", 0.07451));
        using var city = CityVacancy(("2026-06", 7), ("2026-07", 7), ("2026-10", 8));
        var report = new MonthlyUpdateReport();

        RentalVacancyPatcher.Append(vacancy, "riyadh", RentalVacancyPatcher.ReadCityMonthly(city), report);

        var points = VacancyReader.Read(vacancy, new Dictionary<string, string> { ["الرياض"] = "riyadh" })
            .Where(p => p.Indicator == "vacancy").OrderBy(p => p.Year * 100 + p.Period).ToList();
        Assert.Equal(new[] { (6, 7.0), (7, 7.0), (10, 8.0) }, points.Select(p => (p.Period, p.Value)));
        Assert.Empty(report.Warnings);
        Assert.Equal(2, report.Changes.Count);
    }

    [Fact]
    public void Append_KeepsExistingMonth_AndWarns_WhenTheUpdateDiffers()
    {
        using var vacancy = VacancyWorkbook(("2026-06", "بريده", 0.0536));
        using var city = CityVacancy(("2026-06", 7));
        var report = new MonthlyUpdateReport();

        RentalVacancyPatcher.Append(vacancy, "buraidah", RentalVacancyPatcher.ReadCityMonthly(city), report);

        Assert.Equal(2, vacancy.Worksheet(1).LastRowUsed()!.RowNumber());
        Assert.Contains(report.Warnings, w => w.Contains("buraidah 2026-06"));
    }

    private static XLWorkbook VacancyWorkbook(params (string Period, string City, double Rate)[] rows)
    {
        var wb = new XLWorkbook();
        var s = wb.AddWorksheet("الشواغر الايجارية");
        string[] header = ["الفترة", "المدينة \\ المحافظة", "OccupiedUnits", "EligibleUnits", "معدل الشواغر", "معدل المأهول"];
        for (var c = 0; c < header.Length; c++) s.Cell(1, c + 1).Value = header[c];
        for (var i = 0; i < rows.Length; i++)
        {
            s.Cell(i + 2, 1).Value = rows[i].Period;
            s.Cell(i + 2, 2).Value = rows[i].City;
            s.Cell(i + 2, 5).Value = rows[i].Rate;
            s.Cell(i + 2, 6).Value = 1 - rows[i].Rate;
        }
        return wb;
    }

    private static XLWorkbook CityVacancy(params (string Month, int Rate)[] rows)
    {
        var wb = new XLWorkbook();
        wb.AddWorksheet("لوحة المؤشرات");
        var s = wb.AddWorksheet("الشواغر الإيجارية");
        s.Cell(2, 2).Value = "الشواغر الإيجارية - الرياض";
        s.Cell(4, 2).Value = "الشهر"; s.Cell(4, 3).Value = "المعدل"; s.Cell(4, 4).Value = "حد المراقبة (أقل من)";
        for (var i = 0; i < rows.Length; i++)
        {
            s.Cell(5 + i, 2).Value = rows[i].Month;
            s.Cell(5 + i, 3).Value = rows[i].Rate;
            s.Cell(5 + i, 4).Value = 5;
        }
        s.Cell(7 + rows.Length, 2).Value = "معدل الشواغر الإيجارية الربعي (%)";
        s.Cell(8 + rows.Length, 2).Value = "الفترة";
        s.Cell(9 + rows.Length, 2).Value = "2026-Q3"; s.Cell(9 + rows.Length, 3).Value = 7;
        return wb;
    }
}
