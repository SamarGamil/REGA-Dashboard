using System.Text.Json;
using AqarInsights.Api.Controllers;
using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Tests.RentalControls.Reports;
using Microsoft.AspNetCore.Mvc;

namespace AqarInsights.Api.Tests.RentalControls;

// Live = October 2026 monthly data on the Q3 quarter; Q2 = the final Q2 release. A month shows its quarter's values
// (cut at the month) with its own monthly values; rental and public vacancy, CPI and media now run to October.
public class RentalControlsPeriodEndpointTests
{
    private static RentalControlsController Controller() => new(RealReportData.Aggregation, RealReportData.Periods);

    private static T Value<T>(IActionResult result) => (T)Assert.IsType<OkObjectResult>(result).Value!;

    private static string Json(object? v) => JsonSerializer.Serialize(v);

    [Fact]
    public async Task No_period_serves_the_live_quarter()
    {
        Assert.Equal(Json(Value<OverviewData>(await Controller().GetOverview("2026-Q3"))), Json(Value<OverviewData>(await Controller().GetOverview())));
        Assert.Equal(Json(Value<IndicatorsData>(await Controller().GetIndicators("2026-Q3"))), Json(Value<IndicatorsData>(await Controller().GetIndicators())));
        Assert.Equal(Json(Value<CityAnalysisData>(await Controller().GetCity("riyadh", "2026-Q3"))), Json(Value<CityAnalysisData>(await Controller().GetCity("riyadh"))));
    }

    [Theory]
    [InlineData("2026-04", "2026-Q2")]
    [InlineData("2026-05", "2026-Q2")]
    [InlineData("2026-06", "2026-Q2")]
    [InlineData("2026-07", "2026-Q3")]
    [InlineData("2026-08", "2026-Q3")]
    [InlineData("2026-09", "2026-Q3")]
    public async Task A_month_shows_its_quarters_overview(string month, string quarter)
    {
        Assert.Equal(Json(Value<OverviewData>(await Controller().GetOverview(quarter))), Json(Value<OverviewData>(await Controller().GetOverview(month))));
        Assert.Equal(Json(Value<IReadOnlyList<CityListItem>>(await Controller().GetCities(quarter))), Json(Value<IReadOnlyList<CityListItem>>(await Controller().GetCities(month))));
    }

    [Theory]
    [InlineData("2026-04", "أبريل 2026", "أبريل 2026")]
    [InlineData("2026-06", "يونيو 2026", "يونيو 2026")]
    [InlineData("2026-07", "يوليو 2026", "يوليو 2026")]
    [InlineData("2026-08", "أغسطس 2026", "أغسطس 2026")]
    [InlineData("2026-09", "سبتمبر 2026", "سبتمبر 2026")]
    [InlineData("2026-10", "أكتوبر 2026", "أكتوبر 2026")]
    [InlineData("2026-Q2", "يونيو 2026", "يونيو 2026")]
    [InlineData("2026-Q3", "سبتمبر 2026", "سبتمبر 2026")]
    public async Task Vacancy_panels_show_the_periods_own_month(string period, string publicVacancy, string rentalVacancy)
    {
        var panels = Value<IndicatorsData>(await Controller().GetIndicators(period)).CurrentPeriodIndicators;
        string State(string id) => panels.Single(p => p.Id == id) is var p && p.Cities.All(c => c.DisplayValue == "—")
            ? $"— {p.PeriodLabel}" : p.PeriodLabel;

        Assert.Equal((publicVacancy, rentalVacancy), (State("public-vacancy"), State("vacancy")));
    }

    [Fact]
    public async Task July_shows_its_own_media_and_cpi_without_september_text()
    {
        var july = Value<CityAnalysisData>(await Controller().GetCity("riyadh", "2026-07"));

        Assert.Equal(("2026-07", "يوليو 2026"), (july.MediaMonitoring.Period, july.MediaMonitoring.MonthLabel));
        Assert.All(july.MarketMovementTable.Where(r => r.Indicator.Contains("(CPI)")), r =>
        {
            Assert.EndsWith("2026-07", r.Level);
            Assert.NotEqual("—", r.Change);
            Assert.Equal("—", r.Assessment); // no workbook assessment for July
        });
        Assert.DoesNotContain(july.ExecutiveSummary.Bullets, b => b.Contains("سبتمبر") || b.Contains("أكتوبر"));
        Assert.Equal("7%", july.Kpis.Single(k => k.Label == "الشواغر الإيجارية").Value);
    }

    [Fact]
    public async Task September_shows_its_published_cpi_its_media_and_the_rebuilt_cpi_sentence()
    {
        var september = Value<CityAnalysisData>(await Controller().GetCity("riyadh", "2026-09"));

        Assert.Equal(("2026-09", "سبتمبر 2026", true),
            (september.MediaMonitoring.Period, september.MediaMonitoring.MonthLabel, september.MediaMonitoring.Available));
        Assert.All(september.MarketMovementTable.Where(r => r.Indicator.Contains("(CPI)")), r =>
        {
            Assert.EndsWith("2026-09", r.Level);
            Assert.NotEqual("—", r.Change);
            Assert.NotEqual("—", r.Assessment); // as published (monthly_movement rows)
        });
        Assert.Contains(september.ExecutiveSummary.Bullets, b => b.Contains("4.0") && b.Contains("سبتمبر 2026"));
        Assert.DoesNotContain(september.ExecutiveSummary.Bullets, b => b.Contains("أكتوبر 2026"));
    }

    [Fact]
    public async Task April_has_no_media_and_its_cpi_from_the_series()
    {
        var april = Value<CityAnalysisData>(await Controller().GetCity("riyadh", "2026-04"));
        var q2 = Value<CityAnalysisData>(await Controller().GetCity("riyadh", "2026-Q2"));

        Assert.False(april.MediaMonitoring.Available);
        Assert.All(april.MarketMovementTable.Where(r => r.Indicator.Contains("(CPI)")), r => Assert.Equal(("2026-04", "—"), (r.Level[^7..], r.Assessment)));
        Assert.Equal(
            Json(q2.Kpis.Where(k => k.Frequency == "quarterly")),
            Json(april.Kpis.Where(k => k.Frequency == "quarterly")));
    }
    [Theory]
    [InlineData("2019-Q1")]
    [InlineData("2026-13")]
    [InlineData("2026-q3")]
    [InlineData("2026-Q5")]
    [InlineData(" 2026-Q3")]
    [InlineData("live")]
    public async Task An_unknown_or_malformed_period_is_404_on_every_endpoint(string period)
    {
        var controller = Controller();

        Assert.All(new[]
        {
            await controller.GetOverview(period),
            await controller.GetIndicators(period),
            await controller.GetCities(period),
            await controller.GetCity("riyadh", period),
        }, r => Assert.Equal(404, Assert.IsType<ObjectResult>(r).StatusCode));
    }

    [Fact]
    public async Task Cities_follow_the_period()
    {
        Assert.NotEmpty(Value<IReadOnlyList<CityListItem>>(await Controller().GetCities("2026-Q2")));
        Assert.Equal(404, Assert.IsType<ObjectResult>(await Controller().GetCity("xyz", "2026-Q2")).StatusCode);
    }
}
