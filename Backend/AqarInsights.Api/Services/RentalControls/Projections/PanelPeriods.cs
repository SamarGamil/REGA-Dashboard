using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Raw;
using AqarInsights.Api.Services.RentalControls.Snapshots;

namespace AqarInsights.Api.Services.RentalControls.Projections;

// Stamps each ranking panel (and each city-analysis KPI card) with its data frequency and the latest period present in
// its own series, so the label follows the (possibly capped) dataset being shown.
public static class PanelPeriods
{
    // The monthly panels' values come from monthly series; the quarterly ones' from the city sheets' quarterly
    // series (rental-increase shows the sheet's quarterly increase, not the monthly rentalChange series).
    private static readonly Dictionary<string, string> MonthlySeries = new()
    {
        ["vacancy"] = "vacancy",
        ["public-vacancy"] = "publicVacancy",
    };

    private static readonly Dictionary<string, Func<CityRawData, List<RawSeriesPoint>>> QuarterlySeries = new()
    {
        ["rental-increase"] = c => c.IncreaseSeries,
        ["investment-return"] = c => c.RoiSeries,
        ["rent-to-income"] = c => c.AffordabilitySeries,
        ["housing-burden"] = c => c.BurdenSeries,
    };

    // City-analysis KPI card labels → panel ids.
    private static readonly Dictionary<string, string> KpiPanelIds = new()
    {
        ["الزيادة الإيجارية"] = "rental-increase",
        ["الشواغر الإيجارية"] = "vacancy",
        ["العائد الإيجاري"] = "investment-return",
        ["نسبة الإيجار إلى الدخل"] = "rent-to-income",
        ["العبء السكني للدخل المحدود"] = "housing-burden",
    };

    public static void Apply(
        IEnumerable<IndicatorPanel> panels,
        IReadOnlyList<IndicatorSeriesPoint> series,
        IReadOnlyDictionary<string, CityRawData> cityRaw)
    {
        foreach (var panel in panels)
            (panel.Frequency, panel.PeriodLabel, panel.PeriodId) = For(panel.Id, series, cityRaw.Values);
    }

    // City page: quarterly periods come from this city's own sheet; monthly ones from the whole series (the overview
    // workbook's vacancy is one release, not a per-city month).
    public static void Apply(IEnumerable<KpiItem> kpis, IReadOnlyList<IndicatorSeriesPoint> series, CityRawData city)
    {
        foreach (var kpi in kpis)
            if (KpiPanelIds.TryGetValue(kpi.Label, out var id))
                (kpi.Frequency, kpi.PeriodLabel, kpi.PeriodId) = For(id, series, new[] { city });
    }

    public static string FrequencyOf(string panelId)
        => MonthlySeries.ContainsKey(panelId) ? "monthly" : QuarterlySeries.ContainsKey(panelId) ? "quarterly" : "";

    private static (string Frequency, string PeriodLabel, string PeriodId) For(
        string panelId, IReadOnlyList<IndicatorSeriesPoint> series, IEnumerable<CityRawData> quarterlySources)
    {
        if (MonthlySeries.TryGetValue(panelId, out var indicator))
            return Monthly(Projections.MonthlySeries.Latest(series, indicator).Month);
        if (QuarterlySeries.TryGetValue(panelId, out var select))
            return LatestQuarter(quarterlySources.SelectMany(select)) is { } q ? ("quarterly", q.Label, q.Id) : ("quarterly", "", "");
        return ("", "", "");
    }

    private static (string, string, string) Monthly(YearMonth? month) => ("monthly", month?.Label ?? "", month?.Id ?? "");

    private static YearQuarter? LatestQuarter(IEnumerable<RawSeriesPoint> points)
    {
        YearQuarter? best = null;
        foreach (var p in points)
            if (YearQuarter.TryParse(p.Period, out var q) && (best is null || q.CompareTo(best.Value) > 0))
                best = q;
        return best;
    }
}
