using System.Text.RegularExpressions;
using ClosedXML.Excel;
using AqarInsights.Api.Services.RentalControls.Snapshots;
using AqarInsights.Api.Utilities;

namespace AqarInsights.Api.Services.RentalControls.MonthlyUpdate;

// Adds the monthly actual gaps of the summary workbook (blocks "شهر <month> <year>" with rows city | expected |
// actual, thousand units) to supply-demand-gap.xlsx: Gap Act = the actual surplus (data owner, 2026-11-18: every
// city's actual is supply above demand) and Monthly Supply ACt = Monthly Demand + Gap Act, the relation the
// workbook's own actual months follow.
public static class GapSummaryPatcher
{
    public sealed record SummaryRow(YearMonth Month, string CityId, double ExpectedK, double ActualK);
    private sealed record Block(int Demand, int GapExp, int SupplyAct, int GapAct, IReadOnlyDictionary<YearMonth, int> Columns);

    /// <summary>
    /// Save options for the gap workbook: editing cells invalidates its cached formula values (e.g. jeddah's annual
    /// Gap Exp = C4-B4), and the import reads cached values only, so they are re-evaluated into the file on save.
    /// </summary>
    public static readonly SaveOptions SaveOptions = new() { EvaluateFormulasBeforeSaving = true };

    public static List<SummaryRow> ReadSummary(IXLWorkbook summary)
    {
        var rows = new List<SummaryRow>();
        YearMonth? month = null;
        foreach (var row in summary.Worksheet(1).RowsUsed())
        {
            var first = row.Cell(1).GetString().Trim();
            if (MonthTitle(first) is { } m) { month = m; continue; }
            if (month is null || !row.Cell(2).TryGetValue<double>(out var expected) || !row.Cell(3).TryGetValue<double>(out var actual)) continue;
            // "الدمام/الخبر" is Dammam's row.
            if (RentalControlsMappings.CityId(first.Split('/')[0]) is { } id) rows.Add(new(month.Value, id, expected, actual));
        }
        return rows;
    }

    public static void Apply(IXLWorkbook gapWorkbook, IReadOnlyList<SummaryRow> rows, MonthlyUpdateReport report)
    {
        foreach (var row in rows.OrderBy(r => r.Month))
        {
            var sheet = gapWorkbook.Worksheets.FirstOrDefault(w => string.Equals(w.Name.Trim(), row.CityId, StringComparison.OrdinalIgnoreCase));
            if (sheet is null || MonthlyBlock(sheet) is not { } block || !block.Columns.TryGetValue(row.Month, out var col))
            {
                report.Warnings.Add($"gap {row.CityId} {row.Month.Id}: no monthly column in supply-demand-gap.xlsx — skipped");
                continue;
            }
            var demand = sheet.Cell(block.Demand, col).GetValue<double>();
            var gapExp = sheet.Cell(block.GapExp, col).GetValue<double>();
            // Compared unrounded: the summary may truncate (1,963 → "1.9"), which a rounded 2.0 would falsely reject.
            if (Math.Abs(Math.Abs(gapExp) / 1000 - row.ExpectedK) > 0.1)
            {
                report.Warnings.Add($"gap {row.CityId} {row.Month.Id}: summary expects {row.ExpectedK}k, the workbook model {Math.Abs(gapExp) / 1000:F1}k — skipped");
                continue;
            }
            if (gapExp != 0 && Math.Sign(gapExp) != Sign)
                report.Warnings.Add($"gap {row.CityId} {row.Month.Id}: the actual is a surplus while the model expects a shortage ({gapExp:N0})");
            var gapAct = Math.Round(Sign * row.ActualK * 1000, MidpointRounding.AwayFromZero);
            var supplyAct = Math.Round(demand + gapAct, MidpointRounding.AwayFromZero);
            sheet.Cell(block.GapAct, col).Value = gapAct;
            sheet.Cell(block.SupplyAct, col).Value = supplyAct;
            report.Changes.Add($"gap {row.CityId} {row.Month.Id}: actual gap {gapAct:N0}, actual supply {supplyAct:N0}");
        }
    }

    // D1 resolved (data owner, 2026-11-18): the summary's actual gaps are surpluses — supply above demand — for every
    // city, so they are stored positive even where the model's expected gap is a shortage.
    private const int Sign = 1;

    // "شهر أكتوبر 2026" → 2026-10 (month names as YearMonth.Label writes them).
    private static YearMonth? MonthTitle(string text)
    {
        var year = Regex.Match(text, @"\d{4}");
        if (!text.StartsWith("شهر", StringComparison.Ordinal) || !year.Success) return null;
        var key = ArabicNormalizer.Normalize(text);
        for (var m = 1; m <= 12; m++)
        {
            var month = new YearMonth(int.Parse(year.Value), m);
            if (key.EndsWith(ArabicNormalizer.Normalize(month.Label), StringComparison.Ordinal)) return month;
        }
        return null;
    }

    // Same labels SupplyDemandGapReader looks for: title "Monthly Demand yyyy", row labels in one column, month
    // numbers 1..12 in the nearest header row above the labels.
    private static Block? MonthlyBlock(IXLWorksheet ws)
    {
        var used = ws.CellsUsed().ToList();
        var title = used.Select(c => Regex.Match(Norm(c.GetString()), @"^monthly demand (\d{4})$")).FirstOrDefault(m => m.Success);
        var demand = used.FirstOrDefault(c => Norm(c.GetString()) == "monthly demand");
        if (title is null || demand is null) return null;
        var year = int.Parse(title.Groups[1].Value);
        var labelCol = demand.Address.ColumnNumber;
        int? Row(string label) => used.FirstOrDefault(c => c.Address.ColumnNumber == labelCol && Norm(c.GetString()) == label)?.Address.RowNumber;

        var columns = new Dictionary<YearMonth, int>();
        for (var r = demand.Address.RowNumber - 1; r >= 1 && columns.Count == 0; r--)
            foreach (var c in ws.Row(r).CellsUsed())
                if (c.Address.ColumnNumber > labelCol && c.Value.IsNumber && c.Value.GetNumber() is var n && n == Math.Floor(n) && n is >= 1 and <= 12)
                    columns.TryAdd(new YearMonth(year, (int)n), c.Address.ColumnNumber);

        return Row("gap exp") is int gapExp && Row("monthly supply act") is int supplyAct && Row("gap act") is int gapAct
            ? new Block(demand.Address.RowNumber, gapExp, supplyAct, gapAct, columns)
            : null;
    }

    private static string Norm(string s)
        => Regex.Replace(s.Trim().ToLowerInvariant().Replace(".", "").Replace("suppy", "supply"), @"\s+", " ");
}
