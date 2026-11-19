using AqarInsights.Api.Models.RentalControls.Raw;

namespace AqarInsights.Api.Services.RentalControls.Store;

/// <summary>
/// Abstraction for loading and accessing rental controls raw data.
/// Supports swapping storage backends (JSON files, database, etc.).
/// </summary>
public interface IRentalControlsStore
{
    /// <summary>
    /// Load all data from the underlying storage.
    /// </summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all city records.
    /// </summary>
    IReadOnlyList<CityRecord> GetCities();

    /// <summary>
    /// Get all indicator series points.
    /// </summary>
    IReadOnlyList<IndicatorSeriesPoint> GetIndicatorSeries();

    /// <summary>
    /// Get the supply/demand gap per city (annual + monthly, expected and actual).
    /// </summary>
    IReadOnlyList<RawSupplyDemandGapCity> GetSupplyDemandGap();

    /// <summary>
    /// Get the raw per-city data by city ID, or null if not found.
    /// </summary>
    CityRawData? GetCityRaw(string cityId);
}
