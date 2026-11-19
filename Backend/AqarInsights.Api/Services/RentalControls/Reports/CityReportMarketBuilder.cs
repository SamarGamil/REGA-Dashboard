using System.Globalization;
using System.Text.RegularExpressions;
using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Snapshots;

namespace AqarInsights.Api.Services.RentalControls.Reports;

/// <summary>
/// Builds the report's product table and city-averages line from the city's data (deal counts per unit type and
/// the workbook's market panel), so they always follow the imported quarter. The hand-written narrative values are
/// only a fallback for data that lacks these sources.
/// </summary>
public static class CityReportMarketBuilder
{
    private const string Dash = "—";
    private static readonly string[] Products = { "شقة", "دور", "فيلا" };
    // Below this year-ago deal count a year-on-year percentage comes from a small sample (e.g. 5 → 42 deals = +740%).
    // The product table shows it with the small-base footnote instead of hiding it (QA follow-up on the Q3 reports,
    // reversing the earlier "—"); a zero year-ago count still shows "—" as the percentage is undefined.
    public const int MinYearAgoDeals = 20;
    public const string SmallBaseMarker = "٭";
    public static readonly string SmallBaseFootnote =
        $"{SmallBaseMarker} نسبة محسوبة من قاعدة صغيرة (أقل من {MinYearAgoDeals} صفقة في الربع المقابل من العام السابق) وتُقرأ بحذر.";
    private static readonly Regex AmountWithChange = new(@"^(?<amount>[\d,]+(?:\.\d+)?)\s*\((?<change>[+-]?\d+(?:\.\d+)?%)\)$");

    public static List<CityReportProductRow> ProductRows(CityAnalysisData city, IReadOnlyList<CityReportProductRow> fallback)
    {
        var rentals = city.MarketAnalysisTabs.FirstOrDefault(t => t.Id == "rentals");
        var sales = city.MarketAnalysisTabs.FirstOrDefault(t => t.Id == "sales");
        if (rentals is null && sales is null) return fallback.ToList();

        return Products.Select(p =>
        {
            var old = fallback.FirstOrDefault(r => r.Product == p);
            return new CityReportProductRow
            {
                Product = p,
                RentalDeals = Marked(YearOnYear(rentals, p)),
                SalesDeals = Marked(YearOnYear(sales, p)),
                FisherIndex = Panel(city, $"الرقم القياسي لإيجار {p}") ?? old?.FisherIndex ?? Dash,
            };
        }).ToList();
    }

    /// <summary>Whether any row carries the small-base marker, so the table's footnote must be shown.</summary>
    public static bool HasSmallBase(IEnumerable<CityReportProductRow> rows)
        => rows.Any(r => r.RentalDeals.EndsWith(SmallBaseMarker, StringComparison.Ordinal)
                      || r.SalesDeals.EndsWith(SmallBaseMarker, StringComparison.Ordinal));

    private static string Marked((string Text, bool SmallBase) change)
        => change.SmallBase ? change.Text + SmallBaseMarker : change.Text;

    /// <summary>Year-on-year change in the latest quarter's deal count for a unit type (tabId: "rentals" | "sales").</summary>
    public static string DealsChange(CityAnalysisData city, string tabId, string product)
        => YearOnYear(city.MarketAnalysisTabs.FirstOrDefault(t => t.Id == tabId), product).Text;

    /// <summary>The market-panel change for an indicator (e.g. "الرقم القياسي لإيجار فيلا", "عدد الصفقات الإيجارية").</summary>
    public static string? PanelChange(CityAnalysisData city, string indicatorPrefix) => Panel(city, indicatorPrefix);

    // The workbook's executive point on rising rental demand; the report restates it from the platform's own figures.
    public static bool IsDemandPoint(string text) => text.StartsWith("الطلب الإيجاري في نمو", StringComparison.Ordinal);

    /// <summary>
    /// "ارتفعت الصفقات الإيجارية في {الفترة} إلى نحو N ألف صفقة بنمو سنوي P%، وتصدرتها الشقة." from the latest quarter's deals and the
    /// market panel's year-on-year change; null when the platform has no rising figure.
    /// </summary>
    public static string? DemandSentence(CityAnalysisData city)
    {
        var rentals = city.MarketAnalysisTabs.FirstOrDefault(t => t.Id == "rentals");
        if (rentals is null || !YearQuarter.TryParse(rentals.Categories.LastOrDefault(), out var quarter)
            || ArabicNumber.FirstNumber(Panel(city, "عدد الصفقات الإيجارية")) is not { } change || change <= 0) return null;
        var counts = rentals.Series.Where(s => s.Data.Count > 0 && s.Data[^1] is not null)
            .Select(s => (s.Name, Count: s.Data[^1]!.Value)).ToList();
        var thousands = Math.Round(counts.Sum(c => c.Count) / 1000, MidpointRounding.AwayFromZero);
        if (counts.Count == 0 || thousands < 1) return null;
        var leader = counts.MaxBy(c => c.Count).Name;
        var growth = Math.Round(change, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
        return $"ارتفعت الصفقات الإيجارية في {quarter.Label} إلى نحو {thousands.ToString("0", CultureInfo.InvariantCulture)} ألف صفقة بنمو سنوي {growth}%، وتصدرتها ال{leader}.";
    }

    public static string AveragesLine(CityAnalysisData city, string fallback)
    {
        var rent = AmountWithChange.Match(Panel(city, "متوسط قيمة الصفقة الإيجارية") ?? "");
        var sale = AmountWithChange.Match(Panel(city, "متوسط قيمة الصفقة البيعية") ?? "");
        if (!rent.Success || !sale.Success) return fallback;
        return $"متوسط قيمة الصفقة الإيجارية: {rent.Groups["amount"].Value} ريال/وحدة ({rent.Groups["change"].Value}) — "
             + $"متوسط قيمة الصفقة البيعية: {sale.Groups["amount"].Value} ريال ({sale.Groups["change"].Value}) على مستوى المدينة.";
    }

    // Change of the latest quarter's deal count against the same quarter a year earlier; SmallBase marks a
    // year-ago count under MinYearAgoDeals.
    private static (string Text, bool SmallBase) YearOnYear(MarketAnalysisTab? tab, string product)
    {
        var series = tab?.Series.FirstOrDefault(s => s.Name == product);
        if (tab is null || series is null || tab.Categories.Count == 0 || series.Data.Count != tab.Categories.Count) return (Dash, false);
        if (!YearQuarter.TryParse(tab.Categories[^1], out var latest)) return (Dash, false);
        var prevIndex = tab.Categories.IndexOf(new YearQuarter(latest.Year - 1, latest.Quarter).Id);
        if (prevIndex < 0 || series.Data[prevIndex] is not { } yearAgo || yearAgo <= 0 || series.Data[^1] is not { } now)
            return (Dash, false);

        var pct = Math.Round((now / yearAgo - 1) * 100, MidpointRounding.AwayFromZero);
        var text = Math.Abs(pct).ToString("0", CultureInfo.InvariantCulture);
        return (pct > 0 ? $"+{text}%" : pct < 0 ? $"-{text}%" : "0%", yearAgo < MinYearAgoDeals);
    }

    private static string? Panel(CityAnalysisData city, string indicatorPrefix)
    {
        var change = city.MarketMovementTable.FirstOrDefault(m => m.Indicator.StartsWith(indicatorPrefix, StringComparison.Ordinal))?.Change;
        return string.IsNullOrWhiteSpace(change) ? null : change.Trim();
    }
}
