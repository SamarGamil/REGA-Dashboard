using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Snapshots;

namespace AqarInsights.Api.Tests.RentalControls.Reports;

public class CityReportMarketBuilderTests
{
    private static RentalControlsPeriodData Period(string? id) => RealReportData.Periods.ResolveAsync(id).GetAwaiter().GetResult()!;

    private static string Norm(string s)
    {
        var text = s.TrimEnd(CityReportMarketBuilder.SmallBaseMarker[0]);
        return text == "+0%" ? "0%" : text;
    }

    // Client review (Buraidah): the report showed Q2 averages (14,103 / 338,843) while the Q3 source has 14,420 / 477,833.
    [Fact]
    public void Buraidah_current_report_uses_q3_averages_and_deal_changes()
    {
        var city = Period(null).Aggregation.GetCity("buraidah")!;

        var line = CityReportMarketBuilder.AveragesLine(city, "stale");
        var rows = CityReportMarketBuilder.ProductRows(city, Array.Empty<CityReportProductRow>());

        Assert.Equal("متوسط قيمة الصفقة الإيجارية: 14,420 ريال/وحدة (+1%) — متوسط قيمة الصفقة البيعية: 477,833 ريال (-1%) على مستوى المدينة.", line);
        Assert.Equal(new[] { ("شقة", "+37%", "+0%"), ("دور", "+43%", "+7%"), ("فيلا", "+76%", "+6%") },
            rows.Select(r => (r.Product, r.RentalDeals, r.FisherIndex)));
    }

    // The generated table reproduces the human-reviewed Q2 narrative from the Q2 data, for every city.
    // Values the narrative dashed (zero year-ago base) stay dashed and are skipped here; small-base markers are
    // stripped before comparing.
    [Fact]
    public void Generated_market_figures_match_the_reviewed_q2_narratives()
    {
        var q2 = Period("2026-Q2");
        var mismatches = new List<string>();
        foreach (var c in q2.Aggregation.GetOverview().Cities)
        {
            var city = q2.Aggregation.GetCity(c.Id)!;
            var content = q2.Content.Get(c.Id)!;
            if (CityReportMarketBuilder.AveragesLine(city, "") != content.CityAveragesLine)
                mismatches.Add($"{c.Id} averages: {CityReportMarketBuilder.AveragesLine(city, "")}");
            foreach (var row in CityReportMarketBuilder.ProductRows(city, content.ProductTable))
            {
                var text = content.ProductTable.Single(r => r.Product == row.Product);
                foreach (var (gen, txt, kind) in new[]
                         { (row.RentalDeals, text.RentalDeals, "rent"), (row.SalesDeals, text.SalesDeals, "sales"), (row.FisherIndex, text.FisherIndex, "fisher") })
                    if (gen != "—" && Norm(gen) != Norm(txt))
                        mismatches.Add($"{c.Id} {row.Product} {kind}: {gen} vs {txt}");
            }
        }
        Assert.Empty(mismatches);
    }

    // QA review of the Q3 reports: a small year-ago base must still show its percentage (e.g. Jeddah's 5 → 42 floor
    // sales = +740%), marked for the small-base footnote; a zero base has no percentage and stays "—".
    [Fact]
    public void Small_year_ago_bases_show_marked_changes_and_zero_bases_dash()
    {
        var current = Period(null);
        var hail = current.Aggregation.GetCity("hail")!;
        var jeddah = current.Aggregation.GetCity("jeddah")!;

        Assert.Equal("+3050%", CityReportMarketBuilder.DealsChange(hail, "sales", "فيلا")); // 6 → 189 deals
        Assert.Equal("+49%", CityReportMarketBuilder.DealsChange(hail, "sales", "شقة"));    // 102 → 152 deals
        Assert.Equal("+740%", CityReportMarketBuilder.DealsChange(jeddah, "sales", "دور")); // 5 → 42 deals

        var rows = CityReportMarketBuilder.ProductRows(jeddah, Array.Empty<CityReportProductRow>());
        Assert.Equal("+740%" + CityReportMarketBuilder.SmallBaseMarker, rows.Single(r => r.Product == "دور").SalesDeals);
        Assert.True(CityReportMarketBuilder.HasSmallBase(rows));
    }

    [Fact]
    public void City_report_shows_generated_figures_not_the_narrative_copy()
    {
        var current = Period(null);
        var city = current.Aggregation.GetCity("buraidah")!;
        var stale = current.Content.Get("buraidah")!;
        var model = CityReportModelBuilder.Build(city, CityReportFactsBuilder.Build(
            current.Aggregation.GetOverview().Cities.Single(c => c.Id == "buraidah"), city, current.Aggregation.GetIndicators().CurrentPeriodIndicators),
            stale, new DateOnly(2026, 10, 20));

        Assert.Contains("14,420", model.CityAveragesLine);
        Assert.DoesNotContain("14,103", model.CityAveragesLine);
        Assert.Equal("+37%", model.ProductRows.Single(r => r.Product == "شقة").RentalDeals);
    }

    [Fact]
    public void Falls_back_to_the_narrative_when_the_data_has_no_market_sources()
    {
        var empty = new AqarInsights.Api.Models.RentalControls.CityAnalysisData();
        var fallback = new List<CityReportProductRow> { new() { Product = "شقة", RentalDeals = "+1%", SalesDeals = "+2%", FisherIndex = "+3%" } };

        Assert.Equal("narrative", CityReportMarketBuilder.AveragesLine(empty, "narrative"));
        Assert.Same(fallback[0], Assert.Single(CityReportMarketBuilder.ProductRows(empty, fallback)));
    }
}
