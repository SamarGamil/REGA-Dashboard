using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Reports;

namespace AqarInsights.Api.Tests.RentalControls.Reports;

// Shared by the import warnings and the content-consistency tests: prose figures vs platform data.
public class NarrativeAuditorTests
{
    private static CityAnalysisData City(string id) => RealReportData.Aggregation.GetCity(id)!;

    private static CityReportContent Content(params string[] marketBullets) => new() { MarketBullets = marketBullets.ToList() };

    [Fact]
    public void Flags_a_stale_product_percentage()
    {
        // Buraidah apartment rentals are +37% in the current data; +35% was the Q2 figure.
        var findings = NarrativeAuditor.Audit(City("buraidah"), Content("النشاط الإيجاري يرتفع (الشقة +35%)"));

        var finding = Assert.Single(findings);
        Assert.Contains("+37%", finding);
        Assert.Contains("rental deals", finding);
    }

    [Fact]
    public void Accepts_matching_figures_for_each_kind()
    {
        var findings = NarrativeAuditor.Audit(City("buraidah"), Content(
            "النشاط الإيجاري يرتفع (الشقة +37%) وعدد الصفقات الإيجارية +39%",
            "الرقم القياسي للدور (+7%)",
            "في جانب البيع: الدور (+66%)"));

        Assert.Empty(findings);
    }

    [Fact]
    public void Skips_figures_the_platform_reports_as_dash()
    {
        // Sakaka villa rentals come from a tiny year-ago base → platform value is "—"; the figure stays editorial.
        Assert.Empty(NarrativeAuditor.Audit(City("sakaka"), Content("الفيلا (+160%) من قاعدة صغيرة")));
    }

    [Fact]
    public void Checks_every_narrative_section()
    {
        var content = new CityReportContent { WatchList = { "متابعة الشقة (+1%) إيجاريًا" } };

        Assert.Single(NarrativeAuditor.Audit(City("buraidah"), content));
    }
}
