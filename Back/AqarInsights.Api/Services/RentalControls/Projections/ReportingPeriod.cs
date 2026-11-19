using AqarInsights.Api.Models.RentalControls.Raw;
using AqarInsights.Api.Services.RentalControls.Snapshots;

namespace AqarInsights.Api.Services.RentalControls.Projections;

/// <summary>Derives the current reporting period from the latest quarterly period present in the city series.</summary>
public static class ReportingPeriod
{
    // Used only when no quarterly period is found in the data.
    private const string Fallback = "الربع الثاني 2026";

    public static string Latest(IEnumerable<CityRawData> cities)
        => LatestQuarter(cities) is { } q ? q.Label : Fallback;

    public static YearQuarter? LatestQuarter(IEnumerable<CityRawData> cities)
    {
        YearQuarter? best = null;
        foreach (var city in cities)
        {
            foreach (var period in QuarterlyPeriods(city))
            {
                if (!YearQuarter.TryParse(period, out var q)) continue;
                if (best is null || q.CompareTo(best.Value) > 0)
                    best = q;
            }
        }
        return best;
    }

    private static IEnumerable<string> QuarterlyPeriods(CityRawData c)
    {
        foreach (var p in c.RentalsSeries) yield return p.Period;
        foreach (var p in c.SalesSeries) yield return p.Period;
        foreach (var p in c.AffordabilitySeries) yield return p.Period;
        foreach (var p in c.IncreaseSeries) yield return p.Period;
        foreach (var p in c.RoiSeries) yield return p.Period;
        foreach (var p in c.RentIndexByTypeSeries) yield return p.Period;
    }
}
