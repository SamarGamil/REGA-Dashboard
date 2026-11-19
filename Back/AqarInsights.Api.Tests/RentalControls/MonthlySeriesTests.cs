using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Raw;
using AqarInsights.Api.Services.RentalControls.Projections;
using AqarInsights.Api.Services.RentalControls.Snapshots;
using AqarInsights.Api.Tests.RentalControls.Reports;

namespace AqarInsights.Api.Tests.RentalControls;

// Vacancy (rental and public) takes value and month from the same series, so a value is never shown under another
// month's label. Live data: the rental-vacancy series runs to October 2026 for all 14 cities (the October monthly
// update filled the gaps); the overview workbook's vacancy column is no longer used for it.
public class MonthlySeriesTests
{
    private static IndicatorSeriesPoint P(string indicator, string city, int year, int month, double value)
        => new() { Indicator = indicator, CityId = city, Year = year, Period = month, Value = value };

    [Fact]
    public void Latest_takes_the_newest_month_with_any_value_and_each_citys_first_row()
    {
        var series = new[]
        {
            P("vacancy", "riyadh", 2026, 6, 7), P("vacancy", "riyadh", 2026, 6, 9), P("vacancy", "jeddah", 2026, 2, 5),
            P("vacancy", "hail", 2026, 6, 8), P("publicVacancy", "riyadh", 2026, 8, 1.4),
        };

        var (month, byCity) = MonthlySeries.Latest(series, "vacancy");

        Assert.Equal(new YearMonth(2026, 6), month);
        Assert.Equal(new Dictionary<string, double> { ["riyadh"] = 7, ["hail"] = 8 }, byCity);
    }

    [Fact]
    public void Latest_of_an_empty_series_has_no_month()
    {
        var (month, byCity) = MonthlySeries.Latest(Array.Empty<IndicatorSeriesPoint>(), "vacancy");

        Assert.Null(month);
        Assert.Empty(byCity);
    }

    [Fact]
    public void The_vacancy_panel_shows_the_series_values_of_its_month()
    {
        var panels = RealReportData.Aggregation.GetIndicators().CurrentPeriodIndicators;
        var vacancy = panels.Single(p => p.Id == "vacancy");
        string Shown(string cityId) => vacancy.Cities.Single(c => c.CityId == cityId).DisplayValue;

        Assert.Equal(("2026-10", "أكتوبر 2026"), (vacancy.PeriodId, vacancy.PeriodLabel));
        Assert.Equal("7%", Shown("buraidah")); // October's series value
        Assert.Equal("8%", Shown("jeddah"));
        Assert.Equal(14, vacancy.Cities.Count(c => c.DisplayValue != "—"));
        Assert.Equal("2026-10", panels.Single(p => p.Id == "public-vacancy").PeriodId);
        Assert.Equal("2026-Q3", panels.Single(p => p.Id == "rental-increase").PeriodId);
    }

    [Fact]
    public void A_public_vacancy_city_without_the_panels_month_shows_a_dash()
    {
        var overview = new OverviewData { Cities = new() { new() { Id = "riyadh", Name = "الرياض" }, new() { Id = "jeddah", Name = "جدة" } } };
        var series = new List<IndicatorSeriesPoint> { P("publicVacancy", "riyadh", 2026, 8, 1.4), P("publicVacancy", "jeddah", 2026, 9, 2.1) };

        var panel = IndicatorsProjection.Build(overview, series, new List<RawSupplyDemandGapCity>(), new Dictionary<string, CityRawData>())
            .CurrentPeriodIndicators.Single(p => p.Id == "public-vacancy");

        Assert.Equal("2026-09", panel.PeriodId);
        Assert.Equal(("2.1%", "—"), (panel.Cities.Single(c => c.CityId == "jeddah").DisplayValue, panel.Cities.Single(c => c.CityId == "riyadh").DisplayValue));
    }

    [Fact]
    public void The_city_vacancy_card_and_watch_row_use_the_series_value()
    {
        var buraidah = RealReportData.Aggregation.GetCity("buraidah")!;
        var jeddah = RealReportData.Aggregation.GetCity("jeddah")!;
        KpiItem Card(CityAnalysisData c) => c.Kpis.Single(k => k.Label == "الشواغر الإيجارية");
        string Watch(CityAnalysisData c) => c.WhatToMonitor.Single(w => w.Item == "معدل الشواغر").CurrentStatus;

        Assert.Equal(("7%", "2026-10", "7%"), (Card(buraidah).Value, Card(buraidah).PeriodId, Watch(buraidah)));
        Assert.Equal(("8%", "2026-10", "8%"), (Card(jeddah).Value, Card(jeddah).PeriodId, Watch(jeddah)));
        Assert.Equal("2026-Q3", buraidah.Kpis.Single(k => k.Label == "الزيادة الإيجارية").PeriodId);
    }
}
