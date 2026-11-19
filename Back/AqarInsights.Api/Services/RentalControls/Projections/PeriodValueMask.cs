using System.Text.Json;
using System.Text.RegularExpressions;
using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Services.RentalControls.Snapshots;

namespace AqarInsights.Api.Services.RentalControls.Projections;

/// <summary>
/// Applies a period's rules to the documents built from its data (works on copies; cached views are never modified):
/// <list type="bullet">
/// <item>Month view: a monthly value (vacancy panels and card, movement rows dated to a month, media) shows only when
/// it is the selected month's; otherwise "—" labelled with the month.</item>
/// <item>Quarter view: a monthly value dated before the quarter shows "—" labelled with the quarter.</item>
/// <item>Text: monthly sentences are kept only when the view's month is the text month (and in quarter views);
/// sentences naming a later month are always dropped.</item>
/// </list>
/// </summary>
public static class PeriodValueMask
{
    public const string Dash = "—";
    private const string Monthly = "monthly";

    // Newer workbooks append each row's period to the level ("مدينة - ‎2026-09", with a left-to-right mark); a month
    // marks a monthly row (CPI, "التغير الإيجاري (شهري)"), a quarter ("‎2026-Q3") a quarterly one. A CPI row whose level
    // has no month (older workbooks) is the view's month.
    private static readonly Regex LevelMonth = new(@"\s*[-–]\s*\u200E?(\d{4}-\d{2})\s*$");

    // The watch-list row that shows the vacancy card's figure.
    private const string VacancyWatchItem = "معدل الشواغر";

    public static IndicatorsData Apply(IndicatorsData source, PeriodScope scope)
    {
        if (scope.Month is null && scope.Quarter is null) return source;
        var data = Copy(source);
        foreach (var panel in data.CurrentPeriodIndicators.Where(p => p.Frequency == Monthly))
        {
            if (Replacement(panel.PeriodId, scope) is not { } shown) continue;
            (panel.PeriodLabel, panel.PeriodId) = shown;
            foreach (var city in panel.Cities)
            {
                city.DisplayValue = Dash;
                city.Status = Dash;
                city.Value = 0;
            }
        }
        return data;
    }

    public static CityAnalysisData Apply(CityAnalysisData source, PeriodScope scope)
    {
        if (scope.Month is null && scope.Quarter is null) return source;
        var data = Copy(source);
        // The report-only monthly series are not part of the portal JSON, so the copy does not carry them.
        (data.RentalIncreaseMonthly, data.ActualRentMonthly) = (source.RentalIncreaseMonthly, source.ActualRentMonthly);
        var vacancyMasked = false;
        foreach (var kpi in data.Kpis.Where(k => k.Frequency == Monthly))
        {
            if (Replacement(kpi.PeriodId, scope) is not { } shown) continue;
            kpi.Value = Dash;
            kpi.QuarterlyChange = "";
            kpi.Status = Dash;
            kpi.Trend = "stable";
            (kpi.PeriodLabel, kpi.PeriodId) = shown;
            vacancyMasked = true;
        }
        if (vacancyMasked)
        {
            foreach (var item in data.WhatToMonitor.Where(w => w.Item == VacancyWatchItem))
            {
                item.CurrentStatus = Dash;
                item.Priority = Dash;
                item.Trend = "stable";
            }
        }
        foreach (var row in data.MarketMovementTable)
        {
            if (RowMonth(row.Level) is not { } month || Replacement(month.Id, scope) is null) continue;
            row.Level = LevelMonth.Replace(row.Level, "").Trim();
            row.Change = Dash;
            row.Assessment = Dash;
        }
        // Media without a month (the June release's workbook) is the view's month, like a CPI level without one.
        var media = data.MediaMonitoring;
        if (media.Available && media.Period.Length == 0 && scope.Month is { } m)
            (media.Period, media.MonthLabel) = (m.Id, m.Label);
        else if (media.Available && Replacement(media.Period, scope) is not null)
            data.MediaMonitoring = new MediaMonitoring { Available = false };
        var bullets = data.ExecutiveSummary.Bullets;
        // The summary's CPI bullet quotes its own month's figure: outside that month it is rebuilt with the selected
        // month's figure (in its place), or dropped when the month has none.
        var cpi = MonthlyCpiStatement.ForView(data, scope);
        var kept = new List<string>();
        foreach (var b in bullets)
        {
            if (KeepsSentence(b, scope)) kept.Add(b);
            else if (cpi is not null && MonthlyCpiStatement.IsSummaryBullet(b)) kept.Add(BulletNumber.Match(b).Value + cpi);
        }
        if (kept.Count != bullets.Count || kept.Where((b, i) => b != bullets[i]).Any())
            data.ExecutiveSummary.Bullets = kept.Count > 0 ? kept : [Dash];
        return data;
    }

    // A summary bullet's own numbering ("‏6. "), kept when its sentence is rebuilt.
    private static readonly Regex BulletNumber = new(@"^\s*\u200F?\s*\d+\s*[.\-)]\s*");

    /// <summary>
    /// Whether a sentence of the source's written text belongs in the view: a monthly sentence (CPI, inflation, media,
    /// a named month) only in the text month, or in a quarter view whose quarter contains the text month; never a
    /// sentence naming a later month. Text written for a month after the quarter (a monthly update moved live past
    /// its quarter) is masked like any other foreign month's text.
    /// </summary>
    public static bool KeepsSentence(string text, PeriodScope scope)
    {
        if (scope.Month is { } month)
            return !MonthlyStatements.NamesMonthAfter(text, month)
                && (month == scope.TextMonth || !MonthlyStatements.IsMonthly(text));
        if (scope.Quarter is not { } quarter) return true;
        return !MonthlyStatements.NamesMonthAfter(text, quarter.LastMonth)
            && (!MonthlyStatements.IsMonthly(text) || scope.TextMonth is not { } t || t <= quarter.LastMonth);
    }

    // Null when an item dated periodId stays in the view; otherwise the period label and id shown next to its "—".
    // An item without a month (no data, or a quarterly item) always stays.
    private static (string Label, string Id)? Replacement(string periodId, PeriodScope scope)
    {
        if (!YearMonth.TryParse(periodId, out var month)) return null;
        if (scope.Month is { } selected) return month == selected ? null : (selected.Label, selected.Id);
        if (scope.Quarter is { } quarter) return month >= quarter.FirstMonth ? null : (quarter.Label, quarter.Id);
        return null;
    }

    private static YearMonth? RowMonth(string level)
        => LevelMonth.Match(level) is { Success: true } m && YearMonth.TryParse(m.Groups[1].Value, out var ym) ? ym : null;

    private static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.SerializeToUtf8Bytes(value))!;
}
