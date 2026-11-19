using AqarInsights.Api.Models.RentalControls;

namespace AqarInsights.Api.Services.RentalControls;

/// <summary>
/// Aggregation service that reads from IRentalControlsStore and projects data via
/// OverviewProjection, IndicatorsProjection, and CityProjection, caching the results.
/// </summary>
public interface IRentalControlsAggregationService
{
    /// <summary>
    /// Recompute all cached aggregations from the store.
    /// </summary>
    void Recompute();

    /// <summary>
    /// Get the cached overview data.
    /// </summary>
    OverviewData GetOverview();

    /// <summary>
    /// Get the cached indicators data.
    /// </summary>
    IndicatorsData GetIndicators();

    /// <summary>
    /// Get the cached city index (list of city summary items).
    /// </summary>
    IReadOnlyList<CityListItem> GetCityIndex();

    /// <summary>
    /// Get the cached city analysis data by city ID, or null if not found.
    /// </summary>
    CityAnalysisData? GetCity(string cityId);

    /// <summary>
    /// Get the cached all-cities market-analysis matrix.
    /// </summary>
    MarketAnalysisData GetMarketAnalysis();
}
