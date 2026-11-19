using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Snapshots;
using AqarInsights.Api.Services.RentalControls;
using AqarInsights.Api.Services.RentalControls.Snapshots;

namespace AqarInsights.Api.Tests;

// Shared configurable test double for IRentalControlsAggregationService (replaces per-file FakeAgg copies).
internal class FakeRentalControlsAgg : IRentalControlsAggregationService
{
    public OverviewData Overview = new();
    public IndicatorsData Indicators = new();
    public List<CityListItem> CityIndex = new();
    public MarketAnalysisData Market = new();
    public Func<string, CityAnalysisData?> CityLookup = _ => null;

    public void Recompute() { }
    public OverviewData GetOverview() => Overview;
    public IndicatorsData GetIndicators() => Indicators;
    public IReadOnlyList<CityListItem> GetCityIndex() => CityIndex;
    public CityAnalysisData? GetCity(string cityId) => CityLookup(cityId);
    public MarketAnalysisData GetMarketAnalysis() => Market;
}

// A period service with no stored releases (tests that only use the live data).
internal sealed class NoPeriods : IRentalControlsPeriodService
{
    public ReportingPeriodsResponse GetPeriods() => new("", [], []);

    public Task<RentalControlsPeriodData?> ResolveAsync(string? period, CancellationToken cancellationToken = default)
        => Task.FromResult<RentalControlsPeriodData?>(null);
}
