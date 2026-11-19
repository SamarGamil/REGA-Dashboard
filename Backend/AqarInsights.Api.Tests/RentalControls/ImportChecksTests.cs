using AqarInsights.Api.Models.RentalControls.Raw;
using AqarInsights.Api.Services.RentalControls;
using AqarInsights.Api.Services.RentalControls.Snapshots;
using AqarInsights.Api.Services.RentalControls.Tables;

namespace AqarInsights.Api.Tests.RentalControls;

public class ImportChecksTests
{
    private static MappedRelease Data(params (string Indicator, int Year, int Month)[] points) => MappedRelease.Empty with
    {
        IndicatorSeries = points.Select(p => new IndicatorSeriesPoint { Indicator = p.Indicator, CityId = "riyadh", Year = p.Year, Period = p.Month, Value = 7 }).ToList(),
        CityRaw = new Dictionary<string, CityRawData>
        {
            ["riyadh"] = new() { CityId = "riyadh", Media = [new RawMediaMonitoring { Period = "2026-07", Available = true }] },
        },
    };

    [Fact]
    public void An_older_quarter_is_a_rollback()
    {
        Assert.NotNull(ImportChecks.Rollback(new YearQuarter(2026, 3), new YearQuarter(2026, 2)));
        Assert.Null(ImportChecks.Rollback(new YearQuarter(2026, 3), new YearQuarter(2026, 3)));
        Assert.Null(ImportChecks.Rollback(null, new YearQuarter(2026, 3)));
    }

    [Fact]
    public void Dropping_an_open_month_is_lost_history_but_trimming_a_closed_quarter_is_not()
    {
        var outgoing = Data(("vacancy", 2026, 2), ("vacancy", 2026, 6), ("publicVacancy", 2026, 8));
        var incoming = Data(("vacancy", 2026, 6)) with { CityRaw = new Dictionary<string, CityRawData> { ["riyadh"] = new() { CityId = "riyadh" } } };

        var lost = ImportChecks.LostHistory(outgoing, incoming, safeThrough: new YearMonth(2026, 3));

        Assert.Equal(new[] { "media riyadh 2026-07", "publicVacancy riyadh 2026-08" }, lost);
    }
}
