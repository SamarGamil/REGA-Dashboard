using System.Text.RegularExpressions;
using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Reports;

namespace AqarInsights.Api.Services.RentalControls.Reports;

/// <summary>
/// Checks the hand-written city narrative against the city's data: every product/deal percentage quoted in the
/// prose must match the platform figures (client review of the Buraidah Q3 report, where the narrative still
/// quoted Q2 numbers). Run by the import so stale narratives surface as warnings, and by the test suite.
/// </summary>
public static class NarrativeAuditor
{
    // A product percentage, e.g. "الشقة +39%" / "للفيلا (+45%" — its kind comes from the nearest keyword before it.
    private static readonly Regex ProductPercent = new(@"(?:ال|لل)?(?<p>شقة|دور|فيلا)\s*\(?(?<v>[+-]?\d+(?:\.\d+)?)%");
    private static readonly Regex DealsTotal = new(@"(?:عدد )?الصفقات (?<k>الإيجارية|البيعية)[^%\d""+-]{0,20}\(?(?<v>[+-]?\d+(?:\.\d+)?)%");
    // "انكماش إيجار الفيلا" (fewer rentals) → rental deals; "إيجار الشقة" (rent level) → index; "بيعي" (sales growth)
    // → sales deals; "الإيجاري/ة" → rental deals.
    private static readonly Regex KindKeyword = new("القياسي|الأسعار|أسعار|سعري|انكماش إيجار|إيجار(?!ي)|البيع|بيعي|الإيجاري|الإيجارية|الطلب|النشاط|صفق");

    /// <summary>Figures in the narrative that contradict the city's data. Empty when consistent.</summary>
    public static List<string> Audit(CityAnalysisData city, CityReportContent content)
    {
        var findings = new List<string>();
        foreach (var bullet in Prose(content))
        {
            foreach (Match m in ProductPercent.Matches(bullet))
            {
                var product = m.Groups["p"].Value;
                var keyword = KindKeyword.Matches(bullet[..m.Index]).LastOrDefault()?.Value ?? "";
                var rental = CityReportMarketBuilder.DealsChange(city, "rentals", product);
                var sales = CityReportMarketBuilder.DealsChange(city, "sales", product);
                var index = CityReportMarketBuilder.PanelChange(city, $"الرقم القياسي لإيجار {product}");
                var (kind, expected) = keyword switch
                {
                    "القياسي" or "الأسعار" or "أسعار" or "سعري" or "إيجار" => ("index", index),
                    "البيع" or "بيعي" => ("sales deals", sales),
                    _ => ("rental deals", rental),
                };
                // The prose is often ambiguous ("انكماش إيجار الفيلا" = rental deals, "فجوة الشقة" = index): a figure
                // is stale only when it matches none of the product's current values.
                var v = m.Groups["v"].Value;
                if (!Matches(v, expected) && !Matches(v, rental, strict: true) && !Matches(v, sales, strict: true) && !Matches(v, index, strict: true))
                    findings.Add($"\"{m.Value}\" ({kind}; data: {expected}) in: {bullet}");
            }
            foreach (Match m in DealsTotal.Matches(bullet))
            {
                var expected = CityReportMarketBuilder.PanelChange(city, $"عدد الصفقات {m.Groups["k"].Value}");
                if (!Matches(m.Groups["v"].Value, expected))
                    findings.Add($"\"{m.Value}\" (deals total; data: {expected}) in: {bullet}");
            }
        }
        return findings;
    }

    private static IEnumerable<string> Prose(CityReportContent c) => c.ExecutiveBullets
        .Concat(c.IndicatorsBullets).Concat(c.MarketBullets).Concat(c.MediaBullets)
        .Concat(c.ConclusionBullets).Concat(c.RecommendationBullets).Concat(c.WatchList)
        .Concat(c.AssessmentAxes.Select(a => a.Status));

    // No data to check against (e.g. a zero year-ago base reported as "—") → the figure is left to editorial review,
    // unless strict (used for the alternative readings: they only count when the data has a value).
    // Panel values can carry bidi marks (e.g. "\u200e-2%"); compare the numbers only.
    private static bool Matches(string text, string? platform, bool strict = false)
    {
        if (string.IsNullOrWhiteSpace(platform) || !double.TryParse(Regex.Replace(platform, @"[^\d.+-]", ""), out var b))
            return !strict;
        return double.TryParse(Regex.Replace(text, @"[^\d.+-]", ""), out var a) && Math.Abs(a - b) < 0.001;
    }
}
