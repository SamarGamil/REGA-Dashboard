using System.Text.RegularExpressions;
using AqarInsights.Api.Models.RentalControls.Raw;
using AqarInsights.Api.Models.RentalControls.Tables;
using AqarInsights.Api.Services.RentalControls.Snapshots;

namespace AqarInsights.Api.Services.RentalControls.Tables;

/// <summary>
/// The market-movement rows a workbook holds for its latest month only (CPI ×2, monthly rent change), kept per month
/// as published so a past month still shows its own figures and assessment.
/// </summary>
public static class MonthlyMovement
{
    public static readonly IReadOnlyList<string> Indicators =
        ["الإيجار الفعلي للمسكن (CPI)", "التضخم العام (CPI)", "التغير الإيجاري (شهري)"];

    private static readonly Regex LevelMonth = new(@"\d{4}-\d{2}(?!\d)", RegexOptions.CultureInvariant);

    // A row is the month's when its level names the month or names no month (older workbooks wrote just "مدينة").
    public static (IReadOnlyList<MonthlyMovementRow> Rows, IReadOnlyList<string> Skipped) Capture(
        MappedRelease data, YearMonth month, DateTimeOffset importedAt)
    {
        var rows = new List<MonthlyMovementRow>();
        var skipped = new List<string>();
        foreach (var city in data.Cities)
        {
            if (data.CityRaw.GetValueOrDefault(city.Id) is not { } raw) continue;
            for (var i = 0; i < raw.MovementRows.Count; i++)
            {
                var m = raw.MovementRows[i];
                if (!Indicators.Contains(m.Indicator.Trim())) continue;
                var named = LevelMonth.Match(m.Level);
                if (named.Success && named.Value != month.Id)
                {
                    skipped.Add($"{city.Id} {m.Indicator.Trim()}: level {m.Level}");
                    continue;
                }
                rows.Add(new(month.Id, city.Id, i, m.Indicator, m.Level, m.Change, m.Assessment, importedAt));
            }
        }
        return (rows, skipped);
    }

    public static IReadOnlyList<MonthlyMovementRow> Upsert(
        IReadOnlyList<MonthlyMovementRow> table, YearMonth month, IReadOnlyList<MonthlyMovementRow> rows)
        => table.Where(r => r.Month != month.Id).Concat(rows)
            .OrderBy(r => r.Month, StringComparer.Ordinal).ThenBy(r => r.CityId, StringComparer.Ordinal).ThenBy(r => r.Seq)
            .ToList();

    public static IReadOnlyDictionary<string, IReadOnlyList<RawMovementRow>>? ForMonth(
        IReadOnlyList<MonthlyMovementRow> table, YearMonth month)
    {
        var rows = table.Where(r => r.Month == month.Id).ToList();
        if (rows.Count == 0) return null;
        return rows.GroupBy(r => r.CityId, StringComparer.Ordinal).ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<RawMovementRow>)g.OrderBy(r => r.Seq)
                .Select(r => new RawMovementRow { Indicator = r.Indicator, Level = r.Level, Change = r.Change, Assessment = r.Assessment })
                .ToList(),
            StringComparer.Ordinal);
    }
}
