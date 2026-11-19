using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Raw;
using AqarInsights.Api.Services.RentalControls.Snapshots;

namespace AqarInsights.Api.Services.RentalControls.Projections;

public static class SupplyDemandGapProjection
{
    private static readonly string[] CityOrder = { "riyadh", "jeddah", "dammam", "makkah", "madinah" };

    public static List<SupplyDemandGapCity> Build(
        IReadOnlyList<RawSupplyDemandGapCity> gap, IReadOnlyDictionary<string, string> cityIdToName)
    {
        var byId = gap.GroupBy(g => g.CityId).ToDictionary(g => g.Key, g => g.First());
        var result = new List<SupplyDemandGapCity>();
        foreach (var id in CityOrder)
        {
            if (!byId.TryGetValue(id, out var raw) || !cityIdToName.TryGetValue(id, out var name)) continue;
            result.Add(BuildCity(raw, name));
        }
        return result;
    }

    private static SupplyDemandGapCity BuildCity(RawSupplyDemandGapCity raw, string name)
    {
        var annual = raw.Annual.OrderBy(a => a.Year).ToList();
        var monthly = raw.Monthly
            .Where(m => YearMonth.TryParse(m.Period, out _))
            .OrderBy(m => m.Period, StringComparer.Ordinal)
            .ToList();
        var lastActual = monthly.LastOrDefault(m => m.SupplyAct.HasValue && m.GapAct.HasValue);

        return new SupplyDemandGapCity
        {
            CityId = raw.CityId,
            CityName = name,
            VacancyAssumptionPct = raw.VacancyAssumptionPct,
            LastActualPeriod = lastActual?.Period,
            Kpis = new GapKpis
            {
                FirstYear = annual.Count > 0 ? YearKpi(annual[0]) : null,
                SecondYear = annual.Count > 1 ? YearKpi(annual[1]) : null,
                MonthExpected = lastActual is null ? null
                    : new GapMonthKpi { Period = lastActual.Period, Label = Label(lastActual.Period), Gap = lastActual.GapExp },
                MonthActual = lastActual is null ? null
                    : new GapMonthKpi { Period = lastActual.Period, Label = Label(lastActual.Period), Gap = lastActual.GapAct!.Value },
            },
            Annual = annual.Select(a =>
            {
                var isActual = HasActuals(a);
                return new GapAnnualPoint
                {
                    Year = a.Year,
                    Demand = a.DemandExp,
                    Supply = isActual ? a.SupplyAct!.Value : a.SupplyExp,
                    Gap = isActual ? a.GapAct!.Value : a.GapExp,
                    ExpectedGap = a.GapExp,
                    IsActual = isActual,
                };
            }).ToList(),
            Monthly = monthly.Select(m =>
            {
                var isActual = lastActual is not null
                    && string.CompareOrdinal(m.Period, lastActual.Period) <= 0
                    && m.SupplyAct.HasValue && m.GapAct.HasValue;
                return new GapMonthlyPoint
                {
                    Period = m.Period,
                    Label = Label(m.Period),
                    Demand = m.DemandExp,
                    Supply = isActual ? m.SupplyAct!.Value : m.SupplyExp,
                    Gap = isActual ? m.GapAct!.Value : m.GapExp,
                    ExpectedGap = m.GapExp,
                    IsActual = isActual,
                };
            }).ToList(),
        };
    }

    private static bool HasActuals(RawGapAnnualRow a) => a.SupplyAct.HasValue && a.GapAct.HasValue;

    private static GapYearKpi YearKpi(RawGapAnnualRow a)
        => new() { Year = a.Year, Gap = a.GapExp, ActualGap = HasActuals(a) ? a.GapAct : null };

    private static string Label(string period) => YearMonth.TryParse(period, out var m) ? m.Label : period;
}
