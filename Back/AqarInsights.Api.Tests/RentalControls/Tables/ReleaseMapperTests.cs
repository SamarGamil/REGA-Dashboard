using AqarInsights.Api.Models.RentalControls.Raw;
using AqarInsights.Api.Models.RentalControls.Tables;
using AqarInsights.Api.Services.RentalControls.Tables;
using AqarInsights.Api.Tests.RentalControls.Reports;
using static AqarInsights.Api.Tests.RentalControls.Tables.TableTestData;

namespace AqarInsights.Api.Tests.RentalControls.Tables;

public class ReleaseMapperTests
{
    public static IEnumerable<object[]> StoredReleases()
        => RealReportData.Repository.GetReleases().Select(r => new object[] { r.Id });

    [Theory, MemberData(nameof(StoredReleases))]
    public async Task Every_stored_release_round_trips_exactly(string id)
    {
        var rows = (await RealReportData.Repository.LoadReleaseAsync(id))!;

        var again = ReleaseMapper.ToRows(rows.Release, ReleaseMapper.FromRows(rows));

        Assert.Equal(Json(rows), Json(again));
    }

    [Fact]
    public void Missing_values_stay_missing()
    {
        var data = Small(roi: null, withMedia: false, supplyAct: null);

        var rows = ReleaseMapper.ToRows(LiveRow, data);
        var back = ReleaseMapper.FromRows(rows);

        Assert.DoesNotContain(rows.Observations, o => o.Measure == "kpi.investmentReturn");
        Assert.DoesNotContain(rows.Observations, o => o.Measure == "gap.supplyAct");
        Assert.Empty(rows.MediaMonitoring);
        Assert.Null(back.Cities[0].InvestmentReturn);
        Assert.Null(back.CityRaw["riyadh"].Media);
        Assert.Null(back.SupplyDemandGap[0].Annual[0].SupplyAct);
        Assert.Equal(Json(data), Json(back));
    }

    [Fact]
    public void Rows_are_table_shaped()
    {
        var data = Small();

        var rows = ReleaseMapper.ToRows(LiveRow, data);

        Assert.All(rows.AllRows(), r => Assert.Equal(ReleaseIds.Live, r.ReleaseId));
        Assert.Equal(Enumerable.Range(0, rows.Observations.Count), rows.Observations.Select(o => o.Seq));
        Assert.Contains(rows.Observations, o => o is { Measure: "kpi.cagr", Grain: Grains.Latest, Period: "", Dim: "" });
        Assert.Contains(rows.Observations, o => o is { Measure: "indicator.roi", Grain: Grains.Quarter, Period: "2026-Q2", Dim: "شقة" });
        Assert.Contains(rows.Observations, o => o is { Measure: "indicator.vacancy", Grain: Grains.Month, Period: "2026-06", Dim: "" });
        Assert.Contains(rows.Observations, o => o is { Measure: "city.rentIndex", Grain: Grains.Month, Period: "2026-06", Dim: "الرقم القياسي", Note: "+1.2%" });
        Assert.Contains(rows.Observations, o => o is { Measure: "gap.demandExp", Grain: Grains.Year, Period: "2026" });
        Assert.Contains(rows.Observations, o => o is { Measure: "gap.vacancyAssumptionPct", Grain: Grains.Latest });
        Assert.Contains(rows.CityNarrative, n => n is { Section: ReleaseMapper.DashboardSection, Text: "نقطة" });
        Assert.Contains(rows.CityNarrative, n => n is { Section: "cityAverages", Seq: 0, Text: "متوسط" });
        Assert.Equal(new CityRow(ReleaseIds.Live, "riyadh", 0, "الرياض", true, true), Assert.Single(rows.Cities));
        Assert.Equal(3, rows.MediaItems.Count);
        Assert.Equal(new GapCityRow(ReleaseIds.Live, "riyadh", 0), Assert.Single(rows.GapCities));
        Assert.Equal(Json(data), Json(ReleaseMapper.FromRows(rows)));
    }

    [Fact]
    public void Unknown_shapes_fail_instead_of_being_dropped()
    {
        IndicatorSeriesPoint Point(string indicator, int period, string? nationality = null)
            => new() { Indicator = indicator, CityId = "riyadh", Year = 2026, Period = period, Nationality = nationality };

        Assert.Throws<InvalidOperationException>(() => ReleaseMapper.ToRows(LiveRow, Small() with { IndicatorSeries = [Point("newIndicator", 1)] }));
        Assert.Throws<InvalidOperationException>(() => ReleaseMapper.ToRows(LiveRow, Small() with { IndicatorSeries = [Point("roi", 1, "مصر")] }));
        Assert.Throws<InvalidOperationException>(() => ReleaseMapper.ToRows(LiveRow, Small() with { IndicatorSeries = [Point("roi", 5)] }));
        Assert.Throws<InvalidOperationException>(() => ReleaseMapper.ToRows(LiveRow, Small() with { Cities = [] }));
    }

    [Fact]
    public void Null_narrative_lists_count_as_empty()
    {
        var data = Small();
        var content = data.ReportContent["riyadh"];
        content.ExecutiveBullets = null!;
        content.ProductTable = null!;
        content.AssessmentAxes = null!;

        var rows = ReleaseMapper.ToRows(LiveRow, data);

        Assert.DoesNotContain(rows.CityNarrative, n => n.Section == "executive");
        Assert.Empty(rows.ReportProducts);
        Assert.Empty(rows.ReportAxes);
    }

    [Fact]
    public void Copy_is_deep()
    {
        var data = Small();

        var copy = ReleaseMapper.Copy(data);
        copy.CityRaw["riyadh"].Series = [];

        Assert.NotEmpty(data.CityRaw["riyadh"].Series);
        Assert.Equal(Json(Small()), Json(data));
    }
}
