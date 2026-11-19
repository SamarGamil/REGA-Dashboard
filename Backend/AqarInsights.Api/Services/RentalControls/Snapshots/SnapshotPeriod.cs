using AqarInsights.Api.Models.RentalControls.Raw;

namespace AqarInsights.Api.Services.RentalControls.Snapshots;

/// <summary>
/// Rules that tie a dataset to its as-of month: which month it represents, and dropping any
/// data published for a later period so a snapshot only shows what was known for its month.
/// </summary>
public static class SnapshotPeriod
{
    // indicator-series.json indicators whose Period is a month; the rest (rentToIncome, roi) are quarters.
    private static readonly HashSet<string> MonthlyIndicators = new(StringComparer.Ordinal)
    {
        "publicVacancy", "rentalChange", "vacancy",
    };

    /// <summary>
    /// The as-of month: latest CPI month (the monthly driver of the reports), else the latest
    /// media month, else the last month of the latest quarter. Null when the data has no periods.
    /// </summary>
    public static YearMonth? DetectMonth(IEnumerable<CityRawData> cities)
    {
        var list = cities.ToList();
        var cpi = Max(list.SelectMany(c => c.RentIndexSeries.Concat(c.RentVsInflationSeries)).Select(p => p.Period));
        if (cpi is not null) return cpi;
        var media = Max(list.SelectMany(c => c.Media ?? new()).Select(m => m.Period));
        if (media is not null) return media;
        return Projections.ReportingPeriod.LatestQuarter(list)?.LastMonth;
    }

    /// <summary>Removes series points, media months and indicator points dated after <paramref name="asOf"/>.</summary>
    public static void Trim(CityRawData city, YearMonth asOf)
    {
        city.Series = city.Series.Where(p => IsWithin(p.Period, asOf)).ToList();
        if (city.Media is { } media)
            city.Media = media.Where(m => IsWithin(m.Period, asOf)).ToList();
    }

    public static List<IndicatorSeriesPoint> Trim(IEnumerable<IndicatorSeriesPoint> points, YearMonth asOf)
        => points.Where(p => IndicatorWithin(p, asOf)).ToList();

    /// <summary>
    /// Copies the gap data with actuals not yet known at <paramref name="asOf"/> removed: monthly actuals after it, and a
    /// year's actuals until that year's December. Expected values are forecasts published up front, so they are kept.
    /// </summary>
    public static List<RawSupplyDemandGapCity> Trim(IEnumerable<RawSupplyDemandGapCity> gap, YearMonth asOf)
        => gap.Select(c => new RawSupplyDemandGapCity
        {
            CityId = c.CityId,
            VacancyAssumptionPct = c.VacancyAssumptionPct,
            Annual = c.Annual
                .Select(a => YearKnown(a.Year, asOf) ? a with { } : a with { SupplyAct = null, GapAct = null })
                .ToList(),
            Monthly = c.Monthly
                .Select(m => IsWithin(m.Period, asOf) ? m with { } : m with { SupplyAct = null, GapAct = null })
                .ToList(),
        }).ToList();

    /// <summary>True when any series point, media month or indicator point is dated after <paramref name="asOf"/>.</summary>
    public static bool ExtendsBeyond(IEnumerable<CityRawData> cities, IEnumerable<IndicatorSeriesPoint> indicators, YearMonth asOf)
        => cities.Any(c => c.Series.Any(p => !IsWithin(p.Period, asOf)) || (c.Media?.Any(m => !IsWithin(m.Period, asOf)) ?? false))
           || indicators.Any(p => !IndicatorWithin(p, asOf));

    /// <summary>As above, and also when the gap data has actuals not yet known at <paramref name="asOf"/>.</summary>
    public static bool ExtendsBeyond(
        IEnumerable<CityRawData> cities, IEnumerable<IndicatorSeriesPoint> indicators,
        IEnumerable<RawSupplyDemandGapCity> gap, YearMonth asOf)
        => ExtendsBeyond(cities, indicators, asOf)
           || gap.Any(c => c.Monthly.Any(m => HasActual(m.SupplyAct, m.GapAct) && !IsWithin(m.Period, asOf))
                        || c.Annual.Any(a => HasActual(a.SupplyAct, a.GapAct) && !YearKnown(a.Year, asOf)));

    private static bool HasActual(long? supply, long? gap) => supply.HasValue || gap.HasValue;

    // A year's actual figure exists only once the year is complete.
    private static bool YearKnown(int year, YearMonth asOf) => new YearMonth(year, 12) <= asOf;

    // A month is kept up to asOf; a quarter is kept once it has started (partial quarters ship mid-quarter).
    private static bool IsWithin(string period, YearMonth asOf)
    {
        if (YearMonth.TryParse(period, out var m)) return m <= asOf;
        if (YearQuarter.TryParse(period, out var q)) return q.FirstMonth <= asOf;
        return true;
    }

    private static bool IndicatorWithin(IndicatorSeriesPoint p, YearMonth asOf)
    {
        if (MonthlyIndicators.Contains(p.Indicator))
            return p.Period is < 1 or > 12 || new YearMonth(p.Year, p.Period) <= asOf;
        return p.Period is < 1 or > 4 || new YearQuarter(p.Year, p.Period).FirstMonth <= asOf;
    }

    private static YearMonth? Max(IEnumerable<string> periods)
    {
        YearMonth? best = null;
        foreach (var p in periods)
            if (YearMonth.TryParse(p, out var m) && (best is null || m > best.Value))
                best = m;
        return best;
    }
}
