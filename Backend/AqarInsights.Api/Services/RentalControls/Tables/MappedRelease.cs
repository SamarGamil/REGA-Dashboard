using AqarInsights.Api.Models.RentalControls.Raw;
using AqarInsights.Api.Models.RentalControls.Reports;

namespace AqarInsights.Api.Services.RentalControls.Tables;

/// <summary>
/// One release in the in-memory shape the projections and reports read. CityRaw holds only cities with per-city
/// data; ReportContent only cities with a report narrative (case-insensitive ids, like the narrative files).
/// </summary>
public sealed record MappedRelease(
    IReadOnlyList<CityRecord> Cities,
    IReadOnlyList<IndicatorSeriesPoint> IndicatorSeries,
    IReadOnlyList<RawSupplyDemandGapCity> SupplyDemandGap,
    IReadOnlyDictionary<string, CityRawData> CityRaw,
    IReadOnlyDictionary<string, CityReportContent> ReportContent)
{
    public static MappedRelease Empty { get; } = new(
        [], [], [], new Dictionary<string, CityRawData>(), new Dictionary<string, CityReportContent>(StringComparer.OrdinalIgnoreCase));
}
