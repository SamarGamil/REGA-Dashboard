using DocumentFormat.OpenXml.Packaging;
using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Reports;

namespace AqarInsights.Api.Tests.RentalControls.Reports;

// Client review of the Dammam Q2-2026 report (comment numbers in brackets).
public class DammamReportAcceptanceTests
{
    [Fact]
    public void Word_report_meets_client_review_checklist()
    {
        var service = new CityReportService(TimeProvider.System);
        var file = service.GenerateCity(RealReportData.Current, "dammam", ReportFormat.Word);
        Assert.NotNull(file);

        using var ms = new MemoryStream(file.Content);
        using var doc = WordprocessingDocument.Open(ms, false);
        var main = doc.MainDocumentPart!;
        var text = main.Document.Body!.InnerText + string.Concat(main.HeaderParts.Select(h => h.Header!.InnerText));

        string[] mustContain =
        {
            "حالة السوق:  يتطلب دراسة استباقية",                                   // 1
            "الحد المرجعي للمراقبة: أقل من 6%",                                 // 2
            "الحد المرجعي للمراقبة",                                               // ر-3 (table header)
            "الإيجار الفعلي للمسكن CPI \u00a0سبتمبر\u00a0",              // 3, غ-1, ر-1 (the period word replaces the frequency tag)
            "التضخم العام (CPI) \u00a0سبتمبر\u00a0",                       // غ-2, ر-1
            "العبء السكني لذوي الدخل المحدود",                                           // 6
            "2.2% خلال 2022-2026 — نمو مستدام",                                    // 8, 9
            "ثالث أكبر مدينة من حيث الصفقات الإيجارية",                                                // 11
            "الشواغر الإيجارية 7% (ضمن الحد المرجعي للمراقبة 5%) والعائد الإيجاري 5.4% (دون الحد المرجعي للمراقبة 6%)",   // 12, ر-2 (policy statuses, not "محايد"); yield one decimal; Riyadh Aug review: monitoring reference + threshold
            "الإيجار الفعلي للمسكن CPI (التغير السنوي)",                          // 2.4 (executive-summary card)
            "العائد الإيجاري دون الحد المرجعي للمراقبة",                           // غ-5 superseded: the investment status is the platform's
            "يتطلب دراسة استباقية: مراقبة - العائد الإيجاري 5.4% دون الحد المرجعي للمراقبة (6%)",                 // غ-6 (Q3 overview assessment; reference named in full)
            // غ-7 ("مقابل الزيادة الإيجارية (1%)") moved to the monthly narrative: since the October 2026 monthly
            // update, the quarter report masks narrative sentences written for a month after the quarter.
        };
        string[] mustNotContain =
        {
            "الأكثر توازنًا", "تعاقد", "مسار استباقي", "مستقر فوق الحد الأدنى", "الوحيدة",
            "جميع المؤشرات ضمن حدودها", "ملف ضاغط", "لا حاجة لأي إجراء", "رابع أكبر سوق",
            "(محايد)",
        };

        var missing = mustContain.Where(s => !text.Contains(s, StringComparison.Ordinal)).ToList();
        var stillPresent = mustNotContain.Where(s => text.Contains(s, StringComparison.Ordinal)).ToList();
        Assert.Empty(missing);
        Assert.Empty(stillPresent);
    }

    [Fact]
    public void Kpi_and_table_statuses_match_platform()
    {
        var agg = RealReportData.Aggregation;
        var overviewCity = agg.GetOverview().Cities.Single(c => c.Id == "dammam");
        var f = CityReportFactsBuilder.Build(overviewCity, agg.GetCity("dammam")!, agg.GetIndicators().CurrentPeriodIndicators);

        Assert.Equal(("5.4%", "أقل من 6%", "دون الحد"), (f.InvestmentReturn.Value, f.InvestmentReturn.Reference, f.InvestmentReturn.Status));
        Assert.Equal("ارتفاع", f.ActualRentCpi.Status);   // 4
        Assert.Equal(("+2%", "أكتوبر 2026"), (f.GeneralInflation.Value, f.GeneralInflation.Month));
        // Same شهري/ربعي tags as the indicators and city pages; the CAGR has none.
        Assert.Equal(
            ("", "ربعي", "ربعي", "ربعي", "شهري", "ربعي", "شهري", "شهري"),
            (f.Cagr.Frequency, f.RentalIncrease.Frequency, f.RentToIncome.Frequency, f.HousingBurden.Frequency,
             f.Vacancy.Frequency, f.InvestmentReturn.Frequency, f.ActualRentCpi.Frequency, f.GeneralInflation.Frequency));
    }
}
