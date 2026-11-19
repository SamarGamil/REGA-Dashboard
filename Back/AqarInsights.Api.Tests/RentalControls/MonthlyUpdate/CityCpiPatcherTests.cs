using ClosedXML.Excel;
using AqarInsights.Api.Services.RentalControls.MonthlyUpdate;
using AqarInsights.Api.Services.RentalControls.Snapshots;

namespace AqarInsights.Api.Tests.RentalControls.MonthlyUpdate;

public class CityCpiPatcherTests
{
    private static readonly YearMonth October = new(2026, 10);

    [Fact]
    public void Apply_TakesCpiSheetMonthlyRowsAndConclusions_KeepsQuarterlyRows()
    {
        using var target = City("2026-09", "+4.0%", "+2.3%", "-2.7%", "مدينة - \u200E2026-Q3", "+31%",
            "\u200F1. نمو مرتفع للمعدل السنوي المركب حتى الربع الثالث 2026 بتراجع 0.8 نقطة عن \nالربع السابق.",
            " \u200F2. التغير الإيجاري الشهري تراجع -2.7% في سبتمبر 2026 بعد -1.1% في أغسطس.",
            " \u200F3. الإيجار الفعلي (CPI) يرتفع 4.0% سنويا على مستوى المدينة (سبتمبر 2026).");
        using var update = City("2026-10", "+3.5%", "+2.1%", "-3.9%", "مدينة - \u200E2026-10", "+30%",
            "\u200F1. نمو مرتفع للمعدل السنوي المركب حتى الربع الثالث 2026 بتراجع 0.8 نقطة عن \nالربع السابق.",
            " \u200F3. التغير الإيجاري الشهري -3.9% في أكتوبر 2026 بعد -2.7% في سبتمبر.",
            " \u200F7. الإيجار الفعلي (CPI) يرتفع 3.5% سنويا على مستوى المدينة (أكتوبر 2026).");
        var report = new MonthlyUpdateReport();

        Assert.True(CityCpiPatcher.Apply("riyadh", target, update, October, report));

        var s = target.Worksheet(1);
        Assert.Equal("مدينة - \u200E2026-Q3", s.Cell(13, 3).GetString());       // quarterly deals row kept
        Assert.Equal("+31%", s.Cell(13, 4).GetString());
        Assert.Equal("مدينة - \u200E2026-10", s.Cell(14, 3).GetString());
        Assert.Equal("+3.5%", s.Cell(14, 4).GetString());
        Assert.Equal("+2.1%", s.Cell(15, 4).GetString());
        Assert.Equal("-3.9%", s.Cell(16, 4).GetString());
        Assert.Equal(October, CityCpiPatcher.LatestCpiMonth(target));
        Assert.Equal(
            "\u200F1. نمو مرتفع للمعدل السنوي المركب حتى الربع الثالث 2026 بتراجع 0.8 نقطة عن \nالربع السابق.\n" +
            " \u200F2. التغير الإيجاري الشهري -3.9% في أكتوبر 2026 بعد -2.7% في سبتمبر.\n" +
            " \u200F3. الإيجار الفعلي (CPI) يرتفع 3.5% سنويا على مستوى المدينة (أكتوبر 2026).",
            s.Cell(19, 2).GetString());
        Assert.Empty(report.Warnings);
    }

    [Fact]
    public void Apply_SkipsCity_WhenUpdateCpiIsNotTheRequestedMonth()
    {
        using var target = City("2026-09", "+4.0%", "+2.3%", "-2.7%", "مدينة - 2026-Q3", "+31%", "\u200F1. أ");
        using var update = City("2026-09", "+9.9%", "+9.9%", "-9.9%", "مدينة - 2026-Q3", "+1%", "\u200F1. ب");
        var report = new MonthlyUpdateReport();

        Assert.False(CityCpiPatcher.Apply("riyadh", target, update, October, report));

        Assert.Equal("+4.0%", target.Worksheet(1).Cell(14, 4).GetString());
        Assert.Contains(report.Warnings, w => w.Contains("riyadh") && w.Contains("2026-09"));
    }

    private static XLWorkbook City(string month, string cpi, string inflation, string rentChange,
        string dealsLevel, string dealsChange, params string[] conclusions)
    {
        var wb = new XLWorkbook();
        var s = wb.AddWorksheet("لوحة المؤشرات - الرياض");
        s.Cell(11, 2).Value = "لوحة مؤشرات السوق العقاري";
        Row(s, 12, "المؤشر", "المستوى", "القيمة / التغير", "التقييم");
        Row(s, 13, "عدد الصفقات الإيجارية", dealsLevel, dealsChange, "ارتفاع");
        Row(s, 14, "الإيجار الفعلي للمسكن (CPI)", $"مدينة - \u200E{month}", cpi, "ارتفاع");
        Row(s, 15, "التضخم العام (CPI)", $"مدينة - \u200E{month}", inflation, "مستقر");
        Row(s, 16, "التغير الإيجاري (شهري)", $"مدينة - \u200E{month}", rentChange, "تراجع");
        s.Cell(18, 2).Value = "أبرز الاستنتاجات";
        s.Cell(19, 2).Value = string.Join("\n", conclusions);
        var cpiSheet = wb.AddWorksheet("مؤشرات CPI");
        Row(cpiSheet, 4, "الشهر", "الرقم القياسي", "التغير الشهري", "التغير السنوي");
        Row(cpiSheet, 5, "2026-08", "142.6", "+0.2%", "+4.52%");
        Row(cpiSheet, 6, month, "143.1", "+0.1%", cpi);
        wb.AddWorksheet("الصفقات الإيجارية").Cell(4, 2).Value = "الفترة";
        return wb;
    }

    private static void Row(IXLWorksheet s, int r, params string[] values)
    {
        for (var i = 0; i < values.Length; i++) s.Cell(r, 2 + i).Value = values[i];
    }
}
