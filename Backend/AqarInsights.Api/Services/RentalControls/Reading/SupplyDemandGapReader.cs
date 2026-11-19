using System.Text.RegularExpressions;
using ClosedXML.Excel;
using AqarInsights.Api.Models;
using AqarInsights.Api.Models.RentalControls.Raw;

namespace AqarInsights.Api.Services.RentalControls.Reading;

// Reads supply-demand-gap.xlsx (one sheet per city). Data blocks are found by their labels, not by fixed cells
// (only the vacancy assumption is read from B1):
// an annual block (header "Demand Exp | Supply Exp | Suppy Act | Gap Exp. | Gap Act.", one row per year in
// column A) and a monthly block (row labels in one column, month numbers 1..12 in a header row above them, year
// from the "Monthly Demand yyyy" title). Content problems become warnings; this reader never throws for them.
public static class SupplyDemandGapReader
{
    private static readonly Dictionary<string, string> CityBySheet = new(StringComparer.OrdinalIgnoreCase)
    {
        ["riyadh"] = "riyadh", ["jeddah"] = "jeddah", ["dammam"] = "dammam", ["makkah"] = "makkah", ["madinah"] = "madinah",
    };

    private static readonly Regex MonthlyTitle = new(@"^monthly demand (\d{4})$");

    private static readonly Dictionary<string, string> MonthlyLabels = new()
    {
        ["monthly demand"] = "demand",
        ["monthly supply"] = "supply",
        ["gap exp"] = "gapExp",
        ["monthly supply act"] = "supplyAct",
        ["gap act"] = "gapAct",
    };

    public static List<RawSupplyDemandGapCity> Read(IXLWorkbook wb, List<ConversionWarning> warnings)
    {
        var result = new List<RawSupplyDemandGapCity>();
        foreach (var ws in wb.Worksheets)
        {
            var sheet = ws.Name.Trim();
            if (!CityBySheet.TryGetValue(sheet, out var cityId))
            {
                warnings.Add(Warn(sheet, "unknown city sheet — skipped"));
                continue;
            }
            var city = new RawSupplyDemandGapCity
            {
                CityId = cityId,
                VacancyAssumptionPct = ReadAssumption(ws),
                Annual = ReadAnnual(ws, sheet, warnings),
                Monthly = ReadMonthly(ws, sheet, warnings),
            };
            if (city.Annual.Count > 0 || city.Monthly.Count > 0) result.Add(city);
        }
        return result;
    }

    // B1 holds the vacancy assumption as a fraction (0.1) or, if typed by hand, a whole percentage (10).
    private static double? ReadAssumption(IXLWorksheet ws) => Number(ws.Cell("B1")) switch
    {
        double v when v is > 0 and < 1 => Math.Round(v * 100, 1),
        double v when v is >= 1 and <= 100 => Math.Round(v, 1),
        _ => null,
    };

    private static List<RawGapAnnualRow> ReadAnnual(IXLWorksheet ws, string sheet, List<ConversionWarning> warnings)
    {
        var rows = new List<RawGapAnnualRow>();
        var header = ws.CellsUsed().FirstOrDefault(c => Norm(c.GetString()) == "demand exp");
        if (header is null)
        {
            warnings.Add(Warn(sheet, "annual block not found (no 'Demand Exp' header)"));
            return rows;
        }

        var headerRow = header.Address.RowNumber;
        var cols = new Dictionary<string, int>();
        foreach (var c in ws.Row(headerRow).CellsUsed())
            cols.TryAdd(Norm(c.GetString()), c.Address.ColumnNumber);
        if (!cols.TryGetValue("supply exp", out var supplyExpCol) || !cols.TryGetValue("gap exp", out var gapExpCol))
        {
            warnings.Add(Warn(sheet, "annual header is missing 'Supply Exp' or 'Gap Exp.'"));
            return rows;
        }
        var demandCol = header.Address.ColumnNumber;
        int? supplyActCol = cols.TryGetValue("supply act", out var sa) ? sa : null;
        int? gapActCol = cols.TryGetValue("gap act", out var ga) ? ga : null;

        var last = ws.LastRowUsed()?.RowNumber() ?? 0;
        for (var r = headerRow + 1; r <= last; r++)
        {
            if (Number(ws.Cell(r, 1)) is not { } y || y != Math.Floor(y) || y is < 1900 or > 2100)
            {
                if (rows.Count > 0) break; // "dif" (or any non-year) ends the block
                continue;
            }
            if (Whole(ws.Cell(r, demandCol)) is not { } demand
                || Whole(ws.Cell(r, supplyExpCol)) is not { } supply
                || Whole(ws.Cell(r, gapExpCol)) is not { } gap)
            {
                warnings.Add(Warn(sheet, $"annual row {(int)y} has no expected values — skipped"));
                continue;
            }
            rows.Add(new RawGapAnnualRow
            {
                Year = (int)y,
                DemandExp = demand,
                SupplyExp = supply,
                GapExp = gap,
                SupplyAct = supplyActCol is int sac ? Whole(ws.Cell(r, sac)) : null,
                GapAct = gapActCol is int gac ? Whole(ws.Cell(r, gac)) : null,
            });
        }
        return rows;
    }

    private static List<RawGapMonthlyRow> ReadMonthly(IXLWorksheet ws, string sheet, List<ConversionWarning> warnings)
    {
        var rows = new List<RawGapMonthlyRow>();
        var used = ws.CellsUsed().ToList();
        var title = used.Select(c => MonthlyTitle.Match(Norm(c.GetString()))).FirstOrDefault(m => m.Success);
        var demandLabel = used.FirstOrDefault(c => Norm(c.GetString()) == "monthly demand");
        if (title is null || demandLabel is null)
        {
            warnings.Add(Warn(sheet, "monthly block not found (no 'Monthly Demand yyyy' title or 'Monthly Demand' row)"));
            return rows;
        }
        var year = int.Parse(title.Groups[1].Value);
        var labelCol = demandLabel.Address.ColumnNumber;
        var labelRow = demandLabel.Address.RowNumber;

        // Month numbers 1..12 sit in a header row above the labels, right of the label column.
        Dictionary<int, int>? monthCols = null;
        for (var r = labelRow - 1; r >= 1 && monthCols is null; r--)
        {
            var found = new Dictionary<int, int>();
            foreach (var c in ws.Row(r).CellsUsed())
            {
                if (c.Address.ColumnNumber <= labelCol || Number(c) is not { } n || n != Math.Floor(n) || n is < 1 or > 12) continue;
                found.TryAdd((int)n, c.Address.ColumnNumber);
            }
            if (found.ContainsKey(1)) monthCols = found;
        }
        if (monthCols is null)
        {
            warnings.Add(Warn(sheet, "monthly block has no 1..12 month header"));
            return rows;
        }

        var labelRows = new Dictionary<string, int>();
        var last = ws.LastRowUsed()?.RowNumber() ?? 0;
        for (var r = labelRow; r <= last; r++)
            if (MonthlyLabels.TryGetValue(Norm(ws.Cell(r, labelCol).GetString()), out var key))
                labelRows.TryAdd(key, r);
        if (!labelRows.ContainsKey("supply") || !labelRows.ContainsKey("gapExp"))
        {
            warnings.Add(Warn(sheet, "monthly block is missing 'Monthly Supply' or 'Gap Exp'"));
            return rows;
        }

        long? At(string key, int col) => labelRows.TryGetValue(key, out var r) ? Whole(ws.Cell(r, col)) : null;
        foreach (var (month, col) in monthCols.OrderBy(kv => kv.Key))
        {
            if (At("demand", col) is not { } demand || At("supply", col) is not { } supply || At("gapExp", col) is not { } gap)
                continue;
            rows.Add(new RawGapMonthlyRow
            {
                Period = $"{year:D4}-{month:D2}",
                DemandExp = demand,
                SupplyExp = supply,
                GapExp = gap,
                SupplyAct = At("supplyAct", col),
                GapAct = At("gapAct", col),
            });
        }
        return rows;
    }

    // "Gap Exp." → "gap exp"; "Suppy Act" (workbook typo) → "supply act"; "Monthly Supply ACt" → "monthly supply act".
    private static string Norm(string s)
        => Regex.Replace(s.Trim().ToLowerInvariant().Replace(".", "").Replace("suppy", "supply"), @"\s+", " ");

    private static double? Number(IXLCell cell)
    {
        var v = cell.HasFormula ? cell.CachedValue : cell.Value;
        return v.IsNumber ? v.GetNumber() : null;
    }

    // Rounded on the decimal value Excel shows (15 significant digits): a formula result such as 1411910.4999999995 is
    // 1411910.5 in Excel and rounds up, as it is displayed.
    private static long? Whole(IXLCell cell)
        => Number(cell) is { } d ? (long)Math.Round((decimal)d, MidpointRounding.AwayFromZero) : null;

    private static ConversionWarning Warn(string sheet, string issue)
        => new() { Sheet = $"supply-demand-gap/{sheet}", Issue = issue };
}
