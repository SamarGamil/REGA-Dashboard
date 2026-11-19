using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Options;
using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Projections;
using AqarInsights.Api.Services.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Snapshots;

namespace AqarInsights.Api.Tests.RentalControls.Reports;

// Support for RequirementsVerification*: the generated reports of every period plus the platform's own figures for the
// same period, which are the independent source of truth the checks compare the reports against.
internal static class Rv
{
    private static readonly ConcurrentDictionary<string, ReportDoc> Docs = new();

    public static IEnumerable<object[]> AllPeriods()
    {
        var p = RealReportData.Periods.GetPeriods();
        return p.Monthly.Concat(p.Quarterly).Select(x => new object[] { x.Id });
    }

    public static IEnumerable<object[]> MonthlyPeriods() => RealReportData.Periods.GetPeriods().Monthly.Select(x => new object[] { x.Id });

    public static IEnumerable<object[]> QuarterlyPeriods() => RealReportData.Periods.GetPeriods().Quarterly.Select(x => new object[] { x.Id });

    public static RentalControlsPeriodData Period(string id) => RealReportData.Periods.ResolveAsync(id).GetAwaiter().GetResult()!;

    public static RentalControlsPeriodData? PeriodOrNull(string id) => RealReportData.Periods.ResolveAsync(id).GetAwaiter().GetResult();

    public static IReadOnlyList<string> CityIds(string id) => Period(id).Aggregation.GetOverview().Cities.Select(c => c.Id).ToList();

    public static ReportDoc National(string id) => Docs.GetOrAdd($"{id}|national", _ => ReportDoc.Load(
        new NationalReportService(Options.Create(new NationalReportOptions()), TimeProvider.System).GenerateNational(Period(id), ReportFormat.Word)));

    public static ReportDoc City(string id, string cityId) => Docs.GetOrAdd($"{id}|{cityId}", _ => ReportDoc.Load(
        new CityReportService(TimeProvider.System).GenerateCity(Period(id), cityId, ReportFormat.Word)!));

    public static IEnumerable<(string Name, ReportDoc Doc)> CityDocs(string id) => CityIds(id).Select(c => ($"{id}/{c}", City(id, c)));

    public static IEnumerable<(string Name, ReportDoc Doc)> AllDocs(string id) => CityDocs(id).Prepend(($"{id}/national", National(id)));

    public static bool IsMonthly(string id) => YearMonth.TryParse(id, out _);

    // ---- The platform's figures for a city and period ----------------------------------------------------------------

    public sealed record CityPortal(CityData Overview, CityAnalysisData City, IReadOnlyList<IndicatorPanel> Panels);

    public static CityPortal Portal(string id, string cityId)
    {
        var data = Period(id);
        var agg = data.Aggregation;
        return new CityPortal(
            agg.GetOverview().Cities.Single(c => c.Id == cityId),
            PeriodValueMask.Apply(agg.GetCity(cityId)!, data.Scope),
            PeriodValueMask.Apply(agg.GetIndicators(), data.Scope).CurrentPeriodIndicators);
    }

    public static IReadOnlyList<IndicatorPanel> Panels(string id)
    {
        var data = Period(id);
        return PeriodValueMask.Apply(data.Aggregation.GetIndicators(), data.Scope).CurrentPeriodIndicators;
    }

    // "الربع الثالث 2026" → "الربع الثالث"; "سبتمبر 2026" → "سبتمبر".
    public static string PeriodWord(string label) => Regex.Replace(label ?? "", @"\s*\d{4}\s*$", "").Trim();

    // The platform's monthly rent-vs-inflation table: the last month with a figure in the column, and the figure.
    public static (string Month, string Value)? LastFilled(CityAnalysisData city, string column)
    {
        var tab = city.MarketAnalysisTabs.FirstOrDefault(t => t.Id == "rent-inflation");
        var index = tab?.TableColumns.IndexOf(column) ?? -1;
        if (tab is null || index < 0) return null;
        var row = tab.TableRows.LastOrDefault(r => r.Count > index && !string.IsNullOrWhiteSpace(r[index]) && r[index] != "—");
        return row is null ? null : (row[0], row[index]);
    }

    public static List<(string Month, double Value)> Filled(CityAnalysisData city, string column)
    {
        var tab = city.MarketAnalysisTabs.FirstOrDefault(t => t.Id == "rent-inflation");
        var index = tab?.TableColumns.IndexOf(column) ?? -1;
        var result = new List<(string, double)>();
        if (tab is null || index < 0) return result;
        foreach (var r in tab.TableRows.Where(r => r.Count > index))
            if (Num(r[index]) is { } v) result.Add((r[0], v));
        return result;
    }

    public static double? Num(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = Regex.Match(text.Replace('\u2212', '-'), @"[+-]?\d+(?:\.\d+)?");
        return m.Success && double.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    // ---- Word helpers ------------------------------------------------------------------------------------------------

    public static List<TableCell> Cells(Table table) => table.Descendants<TableCell>().ToList();

    public static TableCell? CellContaining(ReportDoc doc, string phrase)
        => doc.TableElements.SelectMany(t => t.Elements<TableRow>().SelectMany(r => r.Elements<TableCell>()))
            .FirstOrDefault(c => ReportDoc.Norm(c.InnerText).Contains(ReportDoc.Norm(phrase), StringComparison.Ordinal));

    // Colours of the runs in a cell that carry a percentage (the figure of a card).
    public static List<string?> PercentRunColors(TableCell cell) => cell.Descendants<Run>()
        .Where(r => r.InnerText.Contains('%'))
        .Select(r => r.RunProperties?.Color?.Val?.Value).ToList();

    public static (int R, int G, int B)? Rgb(string? hex)
    {
        if (hex is null || hex.Length < 6) return null;
        var h = hex.TrimStart('#');
        return (Convert.ToInt32(h[..2], 16), Convert.ToInt32(h[2..4], 16), Convert.ToInt32(h[4..6], 16));
    }

    public static bool IsRed(string? hex) => Rgb(hex) is { } c && c.R >= 0xB0 && c.G <= 0x60 && c.B <= 0x60;

    public static bool IsGreen(string? hex) => Rgb(hex) is { } c && c.G > c.R + 30 && c.G > c.B + 10;

    public static bool IsGrey(string? hex) => Rgb(hex) is { } c && Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B)) <= 28
        && c.R is >= 0x50 and <= 0xB8;

    public static void NoViolations(List<string> violations, string what)
        => Assert.True(violations.Count == 0, $"{what}: {violations.Count} violation(s)\n" + string.Join("\n", violations.Take(30)));

    public static List<string> Scan(IEnumerable<(string Name, ReportDoc Doc)> docs, params (string Why, Regex Pattern)[] rules)
        => ScanText(docs, ReportDoc.Norm, rules);

    // Scans the paragraphs exactly as written (no-break spaces kept), for patterns that depend on them.
    public static List<string> ScanRaw(IEnumerable<(string Name, ReportDoc Doc)> docs, params (string Why, Regex Pattern)[] rules)
        => ScanText(docs, s => s, rules);

    private static List<string> ScanText(IEnumerable<(string Name, ReportDoc Doc)> docs, Func<string, string> prepare, (string Why, Regex Pattern)[] rules)
    {
        var violations = new List<string>();
        foreach (var (name, doc) in docs)
            foreach (var p in doc.Paragraphs.Select(prepare).Distinct())
                foreach (var (why, pattern) in rules)
                    foreach (Match m in pattern.Matches(p))
                        violations.Add($"{name} [{why}] \"{m.Value}\" in: {p.Replace('\u00a0', '~')}");
        return violations;
    }
}
