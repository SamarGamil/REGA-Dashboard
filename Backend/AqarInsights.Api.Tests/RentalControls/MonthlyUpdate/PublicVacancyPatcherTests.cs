using ClosedXML.Excel;
using AqarInsights.Api.Services.RentalControls.MonthlyUpdate;
using AqarInsights.Api.Services.RentalControls.Reading;

namespace AqarInsights.Api.Tests.RentalControls.MonthlyUpdate;

public class PublicVacancyPatcherTests
{
    [Fact]
    public void Apply_AppendsNewMonths_SkipsCitiesWithoutRate_KeepsHistory()
    {
        using var vacancy = Target((2023, 9, "الرياض", 1.8), (2026, 8, "الرياض", 1.4));
        using var update = Update((2026, 8, "Riyadh", 0.014), (2026, 10, "Riyadh", 0.015), (2026, 10, "Hail", null));
        var report = new MonthlyUpdateReport();

        PublicVacancyPatcher.Apply(vacancy, update, report);

        var points = VacancyReader.Read(vacancy, new Dictionary<string, string> { ["الرياض"] = "riyadh", ["حائل"] = "hail" })
            .Where(p => p.Indicator == "publicVacancy").OrderBy(p => p.Year * 100 + p.Period).ToList();
        Assert.Equal(new[] { (2023, 9, 1.8), (2026, 8, 1.4), (2026, 10, 1.5) }, points.Select(p => (p.Year, p.Period, p.Value)));
        Assert.Single(report.Changes);
        Assert.Empty(report.Warnings);
    }

    [Fact]
    public void Apply_OverwritesAChangedMonth_AndReportsIt()
    {
        using var vacancy = Target((2026, 8, "ابها", 11.2));
        using var update = Update((2026, 8, "Abha", 0.113));
        var report = new MonthlyUpdateReport();

        PublicVacancyPatcher.Apply(vacancy, update, report);

        Assert.Equal(11.3, vacancy.Worksheet(1).Cell(2, 4).GetValue<double>());
        Assert.Contains(report.Changes, c => c.Contains("abha 2026-08") && c.Contains("11.2") && c.Contains("11.3"));
    }

    private static XLWorkbook Target(params (int Year, int Month, string City, double Pct)[] rows)
    {
        var wb = new XLWorkbook();
        var s = wb.AddWorksheet("الشواغر العامة- لم يعتمد");
        s.Cell(1, 1).Value = "السنة"; s.Cell(1, 2).Value = "الشهر"; s.Cell(1, 3).Value = "المدينة"; s.Cell(1, 4).Value = "معدل الشواغر العامة (سكني) %";
        for (var i = 0; i < rows.Length; i++)
        {
            s.Cell(i + 2, 1).Value = rows[i].Year; s.Cell(i + 2, 2).Value = rows[i].Month;
            s.Cell(i + 2, 3).Value = rows[i].City; s.Cell(i + 2, 4).Value = rows[i].Pct;
        }
        return wb;
    }

    private static XLWorkbook Update(params (int Year, int Month, string City, double? Rate)[] rows)
    {
        var wb = new XLWorkbook();
        var s = wb.AddWorksheet("Sheet1");
        string[] header = ["year", "month", "City", "Housing Supply", "Housing Completions", "Vacant Units", "Vacancy Rate"];
        for (var c = 0; c < header.Length; c++) s.Cell(1, c + 1).Value = header[c];
        for (var i = 0; i < rows.Length; i++)
        {
            s.Cell(i + 2, 1).Value = rows[i].Year; s.Cell(i + 2, 2).Value = rows[i].Month; s.Cell(i + 2, 3).Value = rows[i].City;
            if (rows[i].Rate is double r) s.Cell(i + 2, 7).Value = r;
        }
        return wb;
    }
}
