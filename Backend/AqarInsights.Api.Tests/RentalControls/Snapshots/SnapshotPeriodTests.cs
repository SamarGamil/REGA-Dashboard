using AqarInsights.Api.Models.RentalControls.Raw;
using AqarInsights.Api.Services.RentalControls.Snapshots;

namespace AqarInsights.Api.Tests.RentalControls.Snapshots;

public class SnapshotPeriodTests
{
    private static CityRawData City(IEnumerable<(string Series, string Period)> points, params string[] mediaMonths) => new()
    {
        CityId = "riyadh",
        Series = points.Select(p => new CitySeriesPoint { Series = p.Series, Period = p.Period, Key = "v", Value = 1 }).ToList(),
        Media = mediaMonths.Length == 0 ? null : mediaMonths.Select(m => new RawMediaMonitoring { Period = m, Available = true }).ToList(),
    };

    [Theory]
    [InlineData("2026-09", true, 2026, 9)]
    [InlineData("2026-13", false, 0, 0)]
    [InlineData("2026-Q3", false, 0, 0)]
    [InlineData("", false, 0, 0)]
    [InlineData("٢٠٢٦-٠٩", false, 0, 0)]
    [InlineData("２０２６-０９", false, 0, 0)]
    public void YearMonth_parses_only_valid_months(string text, bool ok, int year, int month)
    {
        Assert.Equal(ok, YearMonth.TryParse(text, out var ym));
        if (ok) Assert.Equal(new YearMonth(year, month), ym);
    }

    // Non-ASCII digits are not period ids (they used to pass the regex and throw in int.Parse → HTTP 500).
    [Theory]
    [InlineData("2026-Q3", true)]
    [InlineData("٢٠٢٦-Q3", false)]
    [InlineData("２０２６-Q3", false)]
    public void YearQuarter_parses_only_ascii_ids(string text, bool ok)
        => Assert.Equal(ok, YearQuarter.TryParse(text, out _));

    [Fact]
    public void Period_ids_and_labels()
    {
        Assert.Equal("2026-06", new YearMonth(2026, 6).Id);
        Assert.Equal("يونيو 2026", new YearMonth(2026, 6).Label);
        Assert.Equal(new YearQuarter(2026, 2), new YearMonth(2026, 6).Quarter);
        Assert.True(YearQuarter.TryParse("2026-Q3", out var q));
        Assert.Equal("الربع الثالث 2026", q.Label);
        Assert.Equal(new YearMonth(2026, 7), q.FirstMonth);
        Assert.Equal(new YearMonth(2026, 9), q.LastMonth);
    }

    [Fact]
    public void DetectMonth_prefers_latest_cpi_month_over_media()
    {
        var city = City(new[] { (SeriesTypes.RentIndex, "2026-06"), (SeriesTypes.Rentals, "2026-Q2") }, "2026-06", "2026-08");
        Assert.Equal(new YearMonth(2026, 6), SnapshotPeriod.DetectMonth(new[] { city }));
    }

    [Fact]
    public void DetectMonth_falls_back_to_media_then_quarter_end()
    {
        var withMedia = City(new[] { (SeriesTypes.Rentals, "2026-Q2") }, "2026-07");
        var quarterOnly = City(new[] { (SeriesTypes.Rentals, "2026-Q2") });

        Assert.Equal(new YearMonth(2026, 7), SnapshotPeriod.DetectMonth(new[] { withMedia }));
        Assert.Equal(new YearMonth(2026, 6), SnapshotPeriod.DetectMonth(new[] { quarterOnly }));
        Assert.Null(SnapshotPeriod.DetectMonth(new[] { City(Array.Empty<(string, string)>()) }));
    }

    [Fact]
    public void Trim_drops_months_after_as_of_and_quarters_not_yet_started()
    {
        var city = City(new[]
        {
            (SeriesTypes.RentIndex, "2026-06"), (SeriesTypes.RentIndex, "2026-07"),
            (SeriesTypes.Rentals, "2026-Q2"), (SeriesTypes.Rentals, "2026-Q3"),
        }, "2026-06", "2026-08");

        SnapshotPeriod.Trim(city, new YearMonth(2026, 6));

        Assert.Equal(new[] { "2026-06" }, city.RentIndexSeries.Select(p => p.Period));
        Assert.Equal(new[] { "2026-Q2" }, city.RentalsSeries.Select(p => p.Period));
        Assert.Equal(new[] { "2026-06" }, city.Media!.Select(m => m.Period));
    }

    [Fact]
    public void Trim_keeps_a_quarter_once_it_has_started()
    {
        var city = City(new[] { (SeriesTypes.Rentals, "2026-Q3") });
        SnapshotPeriod.Trim(city, new YearMonth(2026, 8));
        Assert.Single(city.RentalsSeries);
    }

    [Fact]
    public void Trim_indicators_uses_monthly_or_quarterly_period_per_indicator()
    {
        var points = new[]
        {
            new IndicatorSeriesPoint { Indicator = "rentalChange", Year = 2026, Period = 6 },
            new IndicatorSeriesPoint { Indicator = "rentalChange", Year = 2026, Period = 7 },
            new IndicatorSeriesPoint { Indicator = "rentToIncome", Year = 2026, Period = 2 },
            new IndicatorSeriesPoint { Indicator = "rentToIncome", Year = 2026, Period = 3 },
        };

        var kept = SnapshotPeriod.Trim(points, new YearMonth(2026, 6));

        Assert.Equal(new[] { ("rentalChange", 6), ("rentToIncome", 2) }, kept.Select(p => (p.Indicator, p.Period)));
    }

    [Fact]
    public void Trim_gap_clears_monthly_actuals_after_asOf_and_keeps_expected_values()
    {
        var gap = new List<RawSupplyDemandGapCity>
        {
            new()
            {
                CityId = "riyadh",
                Annual = { new RawGapAnnualRow { Year = 2026, DemandExp = 10, SupplyExp = 8, GapExp = -2, SupplyAct = 9, GapAct = -1 } },
                Monthly =
                {
                    new RawGapMonthlyRow { Period = "2026-07", DemandExp = 10, SupplyExp = 8, GapExp = -2, SupplyAct = 9, GapAct = -1 },
                    new RawGapMonthlyRow { Period = "2026-08", DemandExp = 11, SupplyExp = 8, GapExp = -3, SupplyAct = 9, GapAct = -2 },
                },
            },
        };

        var trimmed = SnapshotPeriod.Trim(gap, new YearMonth(2026, 7));

        var city = Assert.Single(trimmed);
        Assert.Equal((long?)9, city.Monthly[0].SupplyAct);
        Assert.Equal((long?)-1, city.Monthly[0].GapAct);
        Assert.Null(city.Monthly[1].SupplyAct);
        Assert.Null(city.Monthly[1].GapAct);
        Assert.Equal(-3L, city.Monthly[1].GapExp);
        Assert.Null(city.Annual[0].SupplyAct); // 2026's full-year actual isn't known in July 2026
        Assert.Equal(-2L, city.Annual[0].GapExp);
        Assert.Equal((long?)9, gap[0].Monthly[1].SupplyAct); // input untouched
        Assert.Equal((long?)9, gap[0].Annual[0].SupplyAct);
    }

    [Fact]
    public void Trim_gap_keeps_a_year_actual_once_the_year_is_over()
    {
        var gap = new List<RawSupplyDemandGapCity>
        {
            new() { CityId = "riyadh", Annual = { new RawGapAnnualRow { Year = 2025, GapExp = -2, SupplyAct = 9, GapAct = -1 } } },
        };

        Assert.Equal((long?)-1, SnapshotPeriod.Trim(gap, new YearMonth(2025, 12)).Single().Annual[0].GapAct);
        Assert.Null(SnapshotPeriod.Trim(gap, new YearMonth(2025, 11)).Single().Annual[0].GapAct);
    }

    [Fact]
    public void ExtendsBeyond_sees_gap_actuals_after_the_cap()
    {
        var gap = new List<RawSupplyDemandGapCity>
        {
            new() { CityId = "riyadh", Monthly = { new RawGapMonthlyRow { Period = "2026-08", GapExp = -3, SupplyAct = 9, GapAct = -2 } } },
        };
        var none = new List<CityRawData>();
        var noIndicators = new List<IndicatorSeriesPoint>();

        Assert.True(SnapshotPeriod.ExtendsBeyond(none, noIndicators, gap, new YearMonth(2026, 7)));
        Assert.False(SnapshotPeriod.ExtendsBeyond(none, noIndicators, gap, new YearMonth(2026, 8)));
    }
}
