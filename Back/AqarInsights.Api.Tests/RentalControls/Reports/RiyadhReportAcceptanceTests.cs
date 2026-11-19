using DocumentFormat.OpenXml.Packaging;
using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Reports;

namespace AqarInsights.Api.Tests.RentalControls.Reports;

// Client review of the Riyadh Q3-2026 report (19-Oct-2026 screenshots).
public class RiyadhReportAcceptanceTests
{
    [Fact]
    public void Word_report_meets_client_review_checklist()
    {
        var file = new CityReportService(TimeProvider.System).GenerateCity(RealReportData.Current, "riyadh", ReportFormat.Word);
        Assert.NotNull(file);

        using var ms = new MemoryStream(file.Content);
        using var doc = WordprocessingDocument.Open(ms, false);
        var main = doc.MainDocumentPart!;
        var text = main.Document.Body!.InnerText + string.Concat(main.HeaderParts.Select(h => h.Header!.InnerText));

        string[] mustContain =
        {
            "هو الأعلى وطنيًا، والضوابط الإيجارية مفعلة مسبقًا",          // controls were already active
            "العبء السكني لذوي الدخل المحدود (35%) يتجاوز الحد المرجعي للمراقبة (30%)", // no contradictory "(مستقر)"
            "الحد المرجعي للمراقبة",                                      // table header
            "الإيجار الفعلي للمسكن CPI \u00a0سبتمبر\u00a0",      // the period word replaces the frequency tag
            "التضخم العام (CPI) \u00a0سبتمبر\u00a0",
            "الشواغر الإيجارية عند مستوى متوسط (7%)",                     // full term
            "العائد الإيجاري (5.1%) دون الحد المرجعي للمراقبة (6%)",                // one decimal, as the source (Jeddah review)
        };
        string[] mustNotContain = { "تم تفعيل الضوابط", "(مستقر).", "ضغط رأسمالي" };

        Assert.Empty(mustContain.Where(s => !text.Contains(s, StringComparison.Ordinal)).ToList());
        Assert.Empty(mustNotContain.Where(s => text.Contains(s, StringComparison.Ordinal)).ToList());
    }

    [Fact]
    public void Burden_status_agrees_with_its_reference()
    {
        var agg = RealReportData.Aggregation;
        var overviewCity = agg.GetOverview().Cities.Single(c => c.Id == "riyadh");
        var f = CityReportFactsBuilder.Build(overviewCity, agg.GetCity("riyadh")!, agg.GetIndicators().CurrentPeriodIndicators);

        Assert.Equal(("35%", "أكبر من 30%", "متجاوز"), (f.HousingBurden.Value, f.HousingBurden.Reference, f.HousingBurden.Status));
        Assert.Equal(("5.1%", "أقل من 6%", "دون الحد"), (f.InvestmentReturn.Value, f.InvestmentReturn.Reference, f.InvestmentReturn.Status));
    }
}
