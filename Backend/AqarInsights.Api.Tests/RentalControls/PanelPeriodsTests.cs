using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Raw;
using AqarInsights.Api.Services.RentalControls.Projections;
using AqarInsights.Api.Services.RentalControls.Snapshots;

namespace AqarInsights.Api.Tests.RentalControls;

public class PanelPeriodsTests
{
    private static readonly OverviewData Overview = new()
    {
        Cities = new List<CityData> { new() { Id = "riyadh", Name = "الرياض" }, new() { Id = "jeddah", Name = "جدة" } },
    };

    private static readonly List<IndicatorSeriesPoint> Series = new()
    {
        new() { Indicator = "rentalChange", CityId = "riyadh", Year = 2026, Period = 6, Value = 1 },
        new() { Indicator = "rentalChange", CityId = "jeddah", Year = 2026, Period = 5, Value = 1 },
        new() { Indicator = "vacancy", CityId = "riyadh", Year = 2026, Period = 6, Value = 7 },
        new() { Indicator = "vacancy", CityId = "jeddah", Year = 2026, Period = 5, Value = 8 },
        new() { Indicator = "publicVacancy", CityId = "riyadh", Year = 2026, Period = 8, Value = 1.4 },
    };

    private static readonly Dictionary<string, CityRawData> CityRaw = new()
    {
        ["riyadh"] = new CityRawData
        {
            CityId = "riyadh",
            RoiSeries = { new() { Period = "2026-Q1" }, new() { Period = "2026-Q3" } },
            AffordabilitySeries = { new() { Period = "2026-Q2" } },
            BurdenSeries = { new() { Period = "2026-Q2" } },
            IncreaseSeries = { new() { Period = "2026-Q2" } },
        },
        ["jeddah"] = new CityRawData
        {
            CityId = "jeddah",
            BurdenSeries = { new() { Period = "2025-Q4" } },
            IncreaseSeries = { new() { Period = "2026-Q3" } },
        },
    };

    private static IndicatorPanel Panel(IndicatorsData data, string id) => data.CurrentPeriodIndicators.Single(p => p.Id == id);

    [Fact]
    public void Monthly_panels_show_the_latest_month_of_their_series()
    {
        var data = IndicatorsProjection.Build(Overview, Series, new List<RawSupplyDemandGapCity>(), CityRaw);

        Assert.Equal("monthly", Panel(data, "vacancy").Frequency);
        Assert.Equal(new YearMonth(2026, 6).Label, Panel(data, "vacancy").PeriodLabel);
        Assert.Equal("monthly", Panel(data, "public-vacancy").Frequency);
        Assert.Equal(new YearMonth(2026, 8).Label, Panel(data, "public-vacancy").PeriodLabel);
    }

    [Fact]
    public void Quarterly_panels_show_the_latest_quarter_across_cities()
    {
        var data = IndicatorsProjection.Build(Overview, Series, new List<RawSupplyDemandGapCity>(), CityRaw);

        // The card's value is the city sheet's quarterly increase, not the monthly rentalChange series.
        Assert.Equal("quarterly", Panel(data, "rental-increase").Frequency);
        Assert.Equal(new YearQuarter(2026, 3).Label, Panel(data, "rental-increase").PeriodLabel);
        Assert.Equal("quarterly", Panel(data, "investment-return").Frequency);
        Assert.Equal(new YearQuarter(2026, 3).Label, Panel(data, "investment-return").PeriodLabel);
        Assert.Equal("quarterly", Panel(data, "rent-to-income").Frequency);
        Assert.Equal(new YearQuarter(2026, 2).Label, Panel(data, "rent-to-income").PeriodLabel);
        Assert.Equal("quarterly", Panel(data, "housing-burden").Frequency);
        Assert.Equal(new YearQuarter(2026, 2).Label, Panel(data, "housing-burden").PeriodLabel);
    }

    [Fact]
    public void Empty_series_leave_the_period_label_empty()
    {
        var data = IndicatorsProjection.Build(Overview, new List<IndicatorSeriesPoint>(), new List<RawSupplyDemandGapCity>(), new Dictionary<string, CityRawData>());

        Assert.Equal("monthly", Panel(data, "vacancy").Frequency);
        Assert.Equal("", Panel(data, "vacancy").PeriodLabel);
        Assert.Equal("quarterly", Panel(data, "rental-increase").Frequency);
        Assert.Equal("", Panel(data, "investment-return").PeriodLabel);
    }

    [Fact]
    public void A_capped_dataset_shows_the_cap_month()
    {
        var capped = SnapshotPeriod.Trim(Series, new YearMonth(2026, 5));

        var data = IndicatorsProjection.Build(Overview, capped, new List<RawSupplyDemandGapCity>(), CityRaw);

        Assert.Equal(new YearMonth(2026, 5).Label, Panel(data, "vacancy").PeriodLabel);
        Assert.Equal("", Panel(data, "public-vacancy").PeriodLabel);
    }
}
