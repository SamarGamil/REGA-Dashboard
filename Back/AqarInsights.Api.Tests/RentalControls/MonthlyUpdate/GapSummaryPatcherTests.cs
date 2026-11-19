using ClosedXML.Excel;
using AqarInsights.Api.Models;
using AqarInsights.Api.Services.RentalControls.MonthlyUpdate;
using AqarInsights.Api.Services.RentalControls.Reading;

namespace AqarInsights.Api.Tests.RentalControls.MonthlyUpdate;

public class GapSummaryPatcherTests
{
    [Fact]
    public void Apply_AddsSurplusActuals_AndSupplyFromDemand()
    {
        using var gap = RiyadhGap();
        using var summary = Summary(("شهر أكتوبر 2026", 183, 127.6), ("شهر سبتمبر 2026", 179.7, 129.3));
        var report = new MonthlyUpdateReport();

        GapSummaryPatcher.Apply(gap, GapSummaryPatcher.ReadSummary(summary), report);

        var monthly = SupplyDemandGapReader.Read(gap, new List<ConversionWarning>()).Single().Monthly.ToDictionary(m => m.Period);
        // Data owner (2026-11-18): the actuals are surpluses, stored positive even against a shortage model.
        Assert.Equal((129300L, 2637501L), (monthly["2026-09"].GapAct!.Value, monthly["2026-09"].SupplyAct!.Value));
        Assert.Equal((127600L, 2643209L), (monthly["2026-10"].GapAct!.Value, monthly["2026-10"].SupplyAct!.Value));
        Assert.Equal(-130998L, monthly["2026-08"].GapAct!.Value);
        Assert.Equal(2, report.Warnings.Count(w => w.Contains("surplus while the model expects a shortage")));
    }

    [Fact]
    public void Apply_SkipsAMonth_WhenTheSummaryExpectationIsNotTheWorkbookModel()
    {
        using var gap = RiyadhGap();
        using var summary = Summary(("شهر سبتمبر 2026", 150, 129.3));
        var report = new MonthlyUpdateReport();

        GapSummaryPatcher.Apply(gap, GapSummaryPatcher.ReadSummary(summary), report);

        Assert.True(gap.Worksheet(1).Cell(8, 18).IsEmpty());
        Assert.Contains(report.Warnings, w => w.Contains("riyadh 2026-09"));
    }

    [Fact]
    public void Apply_KeepsAnnualFormulaCells_ReadableAfterSave()
    {
        // Jeddah's annual Gap Exp / Gap Act are formulas; editing other cells invalidates their cached values,
        // which are all the import reads after a save.
        using var gap = RiyadhGap();
        var s = gap.Worksheet(1);
        s.Cell(19, 2).Value = "Demand Exp"; s.Cell(19, 3).Value = "Supply Exp"; s.Cell(19, 4).Value = "Suppy Act";
        s.Cell(19, 5).Value = "Gap Exp."; s.Cell(19, 6).Value = "Gap Act.";
        s.Cell(20, 1).Value = 2025; s.Cell(20, 2).Value = 100000; s.Cell(20, 3).Value = 90000; s.Cell(20, 4).Value = 91000;
        s.Cell(20, 5).FormulaA1 = "C20-B20"; s.Cell(20, 6).FormulaA1 = "D20-B20";
        using var summary = Summary(("شهر سبتمبر 2026", 179.7, 129.3));

        GapSummaryPatcher.Apply(gap, GapSummaryPatcher.ReadSummary(summary), new MonthlyUpdateReport());
        using var ms = new MemoryStream();
        gap.SaveAs(ms, GapSummaryPatcher.SaveOptions);
        ms.Position = 0;
        using var reloaded = new XLWorkbook(ms);

        var annual = SupplyDemandGapReader.Read(reloaded, new List<ConversionWarning>()).Single().Annual.Single();
        Assert.Equal((2025, -10000L, -9000L), (annual.Year, annual.GapExp, annual.GapAct!.Value));
    }

    // Columns J..U are months 1..12 (column = 9 + month), labels in column H, as in the real workbook.
    private static XLWorkbook RiyadhGap()
    {
        var wb = new XLWorkbook();
        var s = wb.AddWorksheet("Riyadh ");
        s.Cell(1, 2).Value = 0.1;
        s.Cell(2, 10).Value = "Monthly Demand 2026";
        for (var m = 1; m <= 12; m++) s.Cell(3, 9 + m).Value = m;
        s.Cell(4, 8).Value = "Monthly Demand"; s.Cell(5, 8).Value = "Monthly Supply"; s.Cell(6, 8).Value = "Gap Exp";
        s.Cell(7, 8).Value = "Monthly Supply ACt"; s.Cell(8, 8).Value = "Gap Act";
        (int Month, double Demand, double Supply, double GapExp)[] months =
            [(8, 2500793.7037037034, 2324314.666666668, -176479.03703703545),
             (9, 2508201.3888888885, 2328453.5000000014, -179747.88888888713),
             (10, 2515609.0740740737, 2332592.333333335, -183016.7407407388)];
        foreach (var (m, demand, supply, gapExp) in months)
        {
            s.Cell(4, 9 + m).Value = demand; s.Cell(5, 9 + m).Value = supply; s.Cell(6, 9 + m).Value = gapExp;
        }
        s.Cell(7, 17).Value = 2369796; s.Cell(8, 17).Value = -130997.70370370336;
        return wb;
    }

    private static XLWorkbook Summary(params (string Title, double Expected, double Actual)[] months)
    {
        var wb = new XLWorkbook();
        var s = wb.AddWorksheet("فجوة العرض والطلب");
        s.Cell(1, 1).Value = "فجوة العرض والطلب للمدن الرئيسية - نسبة شاغر 10%";
        var r = 4;
        foreach (var (title, expected, actual) in months)
        {
            s.Cell(r, 1).Value = title;
            s.Cell(r + 1, 1).Value = "المدينة"; s.Cell(r + 1, 2).Value = "الفجوة المتوقعة"; s.Cell(r + 1, 3).Value = "الفجوة الفعلية";
            s.Cell(r + 2, 1).Value = "الرياض"; s.Cell(r + 2, 2).Value = expected; s.Cell(r + 2, 3).Value = actual;
            r += 9;
        }
        return wb;
    }
}
