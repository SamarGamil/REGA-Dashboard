using System.Text.RegularExpressions;
using ClosedXML.Excel;
using AqarInsights.Api.Services.RentalControls.Reading;
using AqarInsights.Api.Services.RentalControls.Snapshots;
using AqarInsights.Api.Services.RentalControls.Tables;

namespace AqarInsights.Api.Services.RentalControls.MonthlyUpdate;

// Takes only the monthly parts of a new city workbook into the current one: the CPI sheet, the monthly
// market-movement rows (MonthlyMovement.Indicators) and the conclusions quoting them. Quarterly data stays.
public static class CityCpiPatcher
{
    private const string MarketTitle = "لوحة مؤشرات السوق العقاري";
    private const string ConclusionsTitle = "أبرز الاستنتاجات";
    private static readonly string[] MonthlyConclusionKeys = ["التغير الإيجاري الشهري", "الإيجار الفعلي (CPI)"];
    private static readonly Regex Number = new(@"^\s*\u200F?\s*\d+\.\s*", RegexOptions.CultureInvariant);

    public static bool Apply(string cityId, XLWorkbook target, XLWorkbook update, YearMonth month, MonthlyUpdateReport report)
    {
        if (LatestCpiMonth(update) is not { } latest || latest != month)
        {
            report.Warnings.Add($"{cityId}: the update's CPI ends at {LatestCpiMonth(update)?.Id ?? "none"}, expected {month.Id} — city skipped");
            return false;
        }
        var old = SheetReader.Sheet(target, "مؤشرات", "CPI");
        var (name, position) = (old.Name, old.Position);
        old.Delete();
        SheetReader.Sheet(update, "مؤشرات", "CPI").CopyTo(target, name, position);
        report.Changes.Add($"{cityId}: CPI sheet replaced (through {month.Id})");
        PatchMovementRows(cityId, target.Worksheet(1), update.Worksheet(1), month, report);
        PatchConclusions(cityId, target.Worksheet(1), update.Worksheet(1), report);
        return true;
    }

    public static YearMonth? LatestCpiMonth(IXLWorkbook wb)
    {
        YearMonth? best = null;
        foreach (var row in SheetReader.Sheet(wb, "مؤشرات", "CPI").RowsUsed())
            if (YearMonth.TryParse(row.Cell(2).GetString().Trim(), out var m) && (best is null || m > best.Value))
                best = m;
        return best;
    }

    private static void PatchMovementRows(string cityId, IXLWorksheet target, IXLWorksheet update, YearMonth month, MonthlyUpdateReport report)
    {
        var fresh = MarketRows(update).GroupBy(r => r.Cell(2).GetString().Trim(), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        foreach (var row in MarketRows(target))
        {
            var indicator = row.Cell(2).GetString().Trim();
            if (!MonthlyMovement.Indicators.Contains(indicator)) continue;
            if (!fresh.TryGetValue(indicator, out var source) || !source.Cell(3).GetString().Contains(month.Id))
            {
                report.Warnings.Add($"{cityId}: «{indicator}» has no {month.Id} row in the update — kept");
                continue;
            }
            for (var c = 3; c <= 5; c++) row.Cell(c).Value = source.Cell(c).GetString().Trim();
            report.Changes.Add($"{cityId}: {indicator} = {source.Cell(4).GetString().Trim()} ({source.Cell(5).GetString().Trim()})");
        }
    }

    // Data rows of the market-movement table: after its exact title and header row, until the first blank indicator.
    private static IEnumerable<IXLRow> MarketRows(IXLWorksheet sheet)
    {
        var last = sheet.LastRowUsed()?.RowNumber() ?? 0;
        for (var r = 1; r <= last; r++)
        {
            if (sheet.Cell(r, 2).GetString().Trim() != MarketTitle) continue;
            for (var d = r + 2; d <= last && sheet.Cell(d, 2).GetString().Trim().Length > 0; d++) yield return sheet.Row(d);
            yield break;
        }
    }

    private static void PatchConclusions(string cityId, IXLWorksheet target, IXLWorksheet update, MonthlyUpdateReport report)
    {
        if (ConclusionsCell(target) is not { } targetCell || ConclusionsCell(update) is not { } updateCell)
        {
            report.Warnings.Add($"{cityId}: «{ConclusionsTitle}» not found — conclusions kept");
            return;
        }
        var current = Conclusions(targetCell.GetString());
        var fresh = Conclusions(updateCell.GetString());
        foreach (var key in MonthlyConclusionKeys)
        {
            var i = current.FindIndex(b => b.Contains(key));
            var source = fresh.FirstOrDefault(b => b.Contains(key));
            if (i < 0 || source is null)
            {
                report.Warnings.Add($"{cityId}: no conclusion quoting «{key}» in the {(i < 0 ? "current" : "update")} workbook — kept");
                continue;
            }
            // The update's text under the current conclusion's number (the workbooks number them differently).
            current[i] = Number.Match(current[i]).Value + Number.Replace(source, "", 1);
        }
        targetCell.Value = string.Join("\n", current);
    }

    // One entry per numbered conclusion; a wrapped conclusion keeps its continuation lines.
    private static List<string> Conclusions(string text)
    {
        var list = new List<string>();
        foreach (var line in text.Split('\n'))
            if (list.Count == 0 || Number.IsMatch(line)) list.Add(line);
            else list[^1] += "\n" + line;
        return list;
    }

    private static IXLCell? ConclusionsCell(IXLWorksheet sheet)
    {
        var last = sheet.LastRowUsed()?.RowNumber() ?? 0;
        for (var r = 1; r <= last; r++)
            if (sheet.Cell(r, 2).GetString().Contains(ConclusionsTitle)) return sheet.Cell(r + 1, 2);
        return null;
    }
}
