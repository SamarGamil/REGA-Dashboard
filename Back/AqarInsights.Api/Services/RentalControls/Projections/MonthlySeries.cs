using AqarInsights.Api.Models.RentalControls.Raw;
using AqarInsights.Api.Services.RentalControls.Snapshots;

namespace AqarInsights.Api.Services.RentalControls.Projections;

/// <summary>
/// A monthly indicator's value per city for one month, so a figure is never shown under another month's label:
/// the month is the latest one with any city's value; each city has its value for that month or none.
/// </summary>
public static class MonthlySeries
{
    public static (YearMonth? Month, IReadOnlyDictionary<string, double> ByCity) Latest(
        IEnumerable<IndicatorSeriesPoint> series, string indicator)
    {
        var points = series.Where(p => p.Indicator == indicator && p.Period is >= 1 and <= 12).ToList();
        if (points.Count == 0) return (null, new Dictionary<string, double>());
        var month = points.Select(p => new YearMonth(p.Year, p.Period)).Max();
        var byCity = new Dictionary<string, double>(StringComparer.Ordinal);
        // The vacancy sheet repeats some city-months (always with the same value; the reader rejects conflicts).
        foreach (var p in points.Where(p => p.Year == month.Year && p.Period == month.Month))
            byCity.TryAdd(p.CityId, p.Value);
        return (month, byCity);
    }
}
