using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Reports;

namespace AqarInsights.Api.Tests.RentalControls.Reports;

// Client review of the Jeddah Q3 report.
public class JeddahReportReviewTests
{
    private static CityReportFacts Facts(string cityId)
    {
        var agg = RealReportData.Aggregation;
        return CityReportFactsBuilder.Build(agg.GetOverview().Cities.Single(c => c.Id == cityId), agg.GetCity(cityId)!, agg.GetIndicators().CurrentPeriodIndicators);
    }

    // The overview workbook rounds the yield to a whole percent; the city workbook's series carries the decimal
    // (Jeddah 5.33%). The rounding also hid breaches of the 6% reference (Buraidah 5.79%, Bahah 5.54% — Bahah's
    // series ends at Q4 2025, the point the overview's 6% rounds).
    [Theory]
    [InlineData("jeddah", "5.3%", "دون الحد")]
    [InlineData("buraidah", "5.8%", "دون الحد")]   // overview: 6% (ضمن الحد)
    [InlineData("bahah", "5.5%", "دون الحد")]      // overview: 6% (ضمن الحد); series ends at Q4 2025 (5.54%)
    [InlineData("hail", "6.4%", "ضمن الحد")]
    [InlineData("abha", "4.5%", "دون الحد")]       // no yield series: the overview's own text ("العائد 4.5%")
    public void Yield_is_shown_with_the_source_decimal_and_judged_on_it(string cityId, string value, string status)
        => Assert.Equal((value, status), (Facts(cityId).InvestmentReturn.Value, Facts(cityId).InvestmentReturn.Status));

    [Fact]
    public void Dashboard_overview_and_indicator_panel_show_the_same_yield()
    {
        var agg = RealReportData.Aggregation;
        Assert.Equal("5.3%", agg.GetOverview().Cities.Single(c => c.Id == "jeddah").InvestmentReturn);
        var panel = agg.GetIndicators().CurrentPeriodIndicators.Single(p => p.Id == "investment-return").Cities.Single(c => c.CityId == "jeddah");
        Assert.Equal(("5.3%", 5.3, "دون الحد"), (panel.DisplayValue, panel.Value, panel.Status));
        Assert.Equal(5.3, agg.GetMarketAnalysis().Cities.Single(c => c.Id == "jeddah").InvestmentReturn);
    }

    // The city's own series wins over the overview's text: in the Q2 release Buraidah's overview line repeats Hail's
    // "العائد 4.7%", while Buraidah's latest point (Q1) is 5.23% — the figure its overview 5% rounds. Dammam's text
    // ("5%") has no decimal; its Q1 point 5.43% gives it.
    [Fact]
    public void The_city_series_decimal_wins_over_the_overview_text()
    {
        var q2 = RealReportData.Periods.ResolveAsync("2026-Q2").GetAwaiter().GetResult()!;
        var buraidah = q2.Aggregation.GetOverview().Cities.Single(c => c.Id == "buraidah");
        Assert.Equal("5.2%", buraidah.InvestmentReturn);
        Assert.Equal("4.7%", q2.Aggregation.GetOverview().Cities.Single(c => c.Id == "hail").InvestmentReturn);
        Assert.Equal("5.4%", q2.Aggregation.GetOverview().Cities.Single(c => c.Id == "dammam").InvestmentReturn);
    }

    // Average rental deal value ranks Riyadh first (32,944 vs 26,564); Jeddah leads rent-to-income (18%).
    [Fact]
    public void Narrative_claims_the_national_lead_jeddah_actually_has()
    {
        var content = RealReportData.Current.Content.Get("jeddah")!;
        var prose = string.Join("\n", content.ExecutiveBullets.Concat(content.IndicatorsBullets).Concat(content.MarketBullets)
            .Concat(content.ConclusionBullets).Concat(content.WatchList));

        Assert.DoesNotContain("للإيجارات الأعلى وطنيًا", prose);
        Assert.Contains("أعلى نسبة إيجار إلى الدخل وطنيًا (18%)", prose);
        var top = RealReportData.Aggregation.GetOverview().Cities.MaxBy(c => double.Parse(c.RentToIncome.TrimEnd('%')))!;
        Assert.Equal("jeddah", top.Id);
    }
}
