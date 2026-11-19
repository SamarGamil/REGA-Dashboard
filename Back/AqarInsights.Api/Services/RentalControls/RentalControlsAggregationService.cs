using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Raw;
using AqarInsights.Api.Services.RentalControls.Projections;
using AqarInsights.Api.Services.RentalControls.Store;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AqarInsights.Api.Services.RentalControls;

/// <summary>
/// Aggregates rental controls data from the store and projects it into cached views.
/// </summary>
public class RentalControlsAggregationService : IRentalControlsAggregationService
{
    // Immutable view published atomically by Recompute() and read by all getters.
    private sealed record Snapshot(
        OverviewData Overview,
        IndicatorsData Indicators,
        IReadOnlyDictionary<string, CityAnalysisData> Cities,
        IReadOnlyList<CityListItem> CityIndex,
        MarketAnalysisData MarketAnalysis);

    private readonly IRentalControlsStore _store;
    private readonly MarketAnalysisOptions _marketOptions;
    private readonly KpiThresholdsOptions _kpiThresholds;
    private readonly RentalPolicyOptions _rentalPolicy;
    private readonly ILogger<RentalControlsAggregationService> _logger;
    private volatile Snapshot _snapshot = new(new(), new(), new Dictionary<string, CityAnalysisData>(), new List<CityListItem>(), new());

    public RentalControlsAggregationService(IRentalControlsStore store, IOptions<MarketAnalysisOptions>? marketOptions = null, IOptions<KpiThresholdsOptions>? kpiThresholds = null, IOptions<RentalPolicyOptions>? rentalPolicy = null, ILogger<RentalControlsAggregationService>? logger = null)
    {
        _store = store;
        _marketOptions = marketOptions?.Value ?? new MarketAnalysisOptions();
        _kpiThresholds = kpiThresholds?.Value ?? new KpiThresholdsOptions();
        _rentalPolicy = rentalPolicy?.Value ?? new RentalPolicyOptions();
        _logger = logger ?? NullLogger<RentalControlsAggregationService>.Instance;
    }

    public void Recompute()
    {
        var overview = OverviewProjection.Build(_store.GetCities(), _store.GetCityRaw);

        // Map each overview city to its raw workbook data for city-sourced historical series.
        var cityRawMap = new Dictionary<string, CityRawData>();
        foreach (var city in overview.Cities)
        {
            var raw = _store.GetCityRaw(city.Id);
            if (raw != null)
                cityRawMap[city.Id] = raw;
        }

        var indicators = IndicatorsProjection.Build(
            overview,
            _store.GetIndicatorSeries(),
            _store.GetSupplyDemandGap(),
            cityRawMap,
            _rentalPolicy
        );

        var cities = new Dictionary<string, CityAnalysisData>();
        foreach (var cityRecord in _store.GetCities())
        {
            var cityData = overview.Cities.FirstOrDefault(c => c.Id == cityRecord.Id);
            if (cityData == null)
                continue;

            var cityRaw = _store.GetCityRaw(cityRecord.Id);
            if (cityRaw == null)
                continue; // Skip cities without raw data (matches legacy behavior)

            cities[cityRecord.Id] = CityProjection.Build(cityData, cityRaw, _kpiThresholds, _rentalPolicy, _store.GetIndicatorSeries());
        }

        var cityIndex = new List<CityListItem>();
        foreach (var cityData in overview.Cities)
        {
            cityIndex.Add(new CityListItem
            {
                CityId = cityData.Id,
                CityName = cityData.Name,
                OperationalStatus = cityData.OperationalStatus,
                Classification = cityData.Classification,
                ClassificationColor = RentalControlsMappings.ClassificationColor(cityData.ClassificationLabel)
            });
        }

        var marketAnalysis = MarketAnalysisProjection.Build(overview, cityRawMap, _marketOptions);

        // Publish the fully-built view in a single reference swap.
        _snapshot = new Snapshot(overview, indicators, cities, cityIndex, marketAnalysis);
        _logger.LogInformation("Recomputed rental-controls views: {CityCount} cities", cities.Count);
    }

    public OverviewData GetOverview() => _snapshot.Overview;

    public IndicatorsData GetIndicators() => _snapshot.Indicators;

    public IReadOnlyList<CityListItem> GetCityIndex() => _snapshot.CityIndex;

    public CityAnalysisData? GetCity(string cityId)
        => _snapshot.Cities.TryGetValue(cityId, out var city) ? city : null;

    public MarketAnalysisData GetMarketAnalysis() => _snapshot.MarketAnalysis;
}
