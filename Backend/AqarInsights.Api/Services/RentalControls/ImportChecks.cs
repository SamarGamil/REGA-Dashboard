using AqarInsights.Api.Services.RentalControls.Snapshots;
using AqarInsights.Api.Services.RentalControls.Tables;

namespace AqarInsights.Api.Services.RentalControls;

/// <summary>Checks an import must pass before anything is written.</summary>
public static class ImportChecks
{
    public static string? Rollback(YearQuarter? live, YearQuarter? incoming)
        => live is { } l && incoming is { } n && n < l
            ? $"The workbooks' latest quarter {n.Id} is older than the dashboard's {l.Id}; nothing was written."
            : null;

    // Months live already shows that the new workbooks no longer hold. Months up to safeThrough are inside a frozen
    // quarter (kept in its release), so a workbook may trim them.
    public static IReadOnlyList<string> LostHistory(MappedRelease outgoing, MappedRelease incoming, YearMonth? safeThrough)
    {
        var have = Months(incoming).ToHashSet();
        return Months(outgoing)
            .Where(k => safeThrough is not { } s || k.Month > s)
            .Where(k => !have.Contains(k))
            .Distinct()
            .OrderBy(k => k.Series, StringComparer.Ordinal).ThenBy(k => k.CityId, StringComparer.Ordinal).ThenBy(k => k.Month)
            .Select(k => $"{k.Series} {k.CityId} {k.Month.Id}")
            .ToList();
    }

    private static IEnumerable<(string Series, string CityId, YearMonth Month)> Months(MappedRelease data)
    {
        foreach (var p in data.IndicatorSeries.Where(p => p.Indicator is "vacancy" or "publicVacancy" && p.Period is >= 1 and <= 12))
            yield return (p.Indicator, p.CityId, new YearMonth(p.Year, p.Period));
        foreach (var raw in data.CityRaw.Values)
        {
            foreach (var media in raw.Media ?? [])
                if (YearMonth.TryParse(media.Period, out var m)) yield return ("media", raw.CityId, m);
            foreach (var point in raw.RentVsInflationSeries)
                if (YearMonth.TryParse(point.Period, out var c)) yield return ("cpi", raw.CityId, c);
        }
    }
}
