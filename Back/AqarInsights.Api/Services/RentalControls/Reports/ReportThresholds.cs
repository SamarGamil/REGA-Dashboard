using System.Text.RegularExpressions;
using AqarInsights.Api.Models.RentalControls.Reports;

namespace AqarInsights.Api.Services.RentalControls.Reports;

/// <summary>
/// Client review: every mention of an indicator's reference reads «الحد المرجعي للمراقبة» followed by that indicator's
/// threshold, e.g. «العائد الإيجاري (5.1%) دون الحد المرجعي للمراقبة (6%)». Runs on narrative text after
/// <see cref="ReportTerminology"/> (which writes the indicators' full names). The threshold is that of the indicator
/// named closest before the reference in the same sentence; a sentence that names none keeps the label alone.
/// </summary>
public static class ReportThresholds
{
    public const string Label = "الحد المرجعي للمراقبة";

    public sealed record Indicator(Regex Name, string Threshold);

    private static Regex Rx(string pattern) => new(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // The indicators' names as ReportTerminology writes them. CPI indicators have no reference: listing them stops a
    // reference after their name from taking an earlier indicator's threshold.
    private static readonly Regex CagrName = Rx(@"معدل النمو الإيجاري المركب");
    private static readonly Regex IncreaseName = Rx(@"(?:ال)?زيادة (?:ال)?إيجارية");
    private static readonly Regex RentToIncomeName = Rx(@"نسبة الإيجار إلى الدخل");
    private static readonly Regex BurdenName = Rx(@"(?:ال)?عبء (?:ال)?سكني(?: لذوي الدخل المحدود)?");
    private static readonly Regex VacancyName = Rx(@"(?:ال)?شواغر (?:ال)?إيجارية");
    private static readonly Regex YieldName = Rx(@"(?:ال)?عائد (?:ال)?إيجاري");
    private static readonly Regex RentCpiName = Rx(@"(?:ال)?إيجار (?:ال)?فعلي(?: للمسكن)?(?: CPI)?");
    private static readonly Regex InflationName = Rx(@"(?:ال)?تضخم (?:ال)?عام(?: \(CPI\))?");

    // "(حد 7-9%)" → "(الحد المرجعي للمراقبة 7%–9%)".
    private static readonly Regex ShortRef = Rx(@"\(حد\s+(?<v>[^()]+)\)");
    private static readonly Regex Range = Rx(@"(?<a>\d+(?:\.\d+)?)%?\s*[-–]\s*(?<b>\d+(?:\.\d+)?)%");
    // «الحد» / «للحد» / «والحد» … as a reference (not «الحد من», «الحد الأدنى»), with or without «المرجعي».
    private static readonly Regex Bare = Rx(@"(?<![\p{L}])((?:و|ف)?(?:ب|ك)?(?:ال|لل))حد(?![\p{L}])(?!\s+(?:المرجعي|الأدنى|الأقصى|الأعلى|من(?![\p{L}])))");
    private static readonly Regex Unlabelled = Rx(@"(?<![\p{L}])((?:و|ف)?(?:ب|ك)?(?:ال|لل))حد المرجعي(?! للمراقبة)");
    // Several indicators' references at once: no single threshold.
    private static readonly Regex Plural = Rx(@"(?<![\p{L}])((?:و|ف)?(?:ب|ك|ل)?)الحدود(?![\p{L}])(?!\s+المرجعية)");
    private static readonly Regex PluralUnlabelled = Rx(@"الحدود المرجعية(?! للمراقبة)");

    private static readonly Regex Labelled = Rx(@"(?<=(?:ال|لل))حد المرجعي للمراقبة");
    // A full stop (a decimal point has a digit after it: "5.1"; "2026." ends a sentence) or other sentence end.
    private static readonly Regex SentenceEnd = Rx(@"\.(?!\d)|[!؟؛\n]");
    private static readonly Regex ClauseEnd = Rx(@"\.(?!\d)|[!؟؛،—:\n]");
    private static readonly Regex Parenthesis = Rx(@"^\s*\((?<v>[^()]*)\)");
    private static readonly Regex SinglePercent = Rx(@"^\s*[+-]?\d+(?:\.\d+)?%\s*$");
    // "الحد المرجعي للمراقبة: أقل من 6%" / "… 30%": a threshold already follows (a colon before prose does not count).
    private static readonly Regex FigureAfter = Rx(@"^\s*(?::\s*(?:[<>≤≥]|أقل من|أكبر من|أكثر من)?\s*)?\d");
    // Inside parentheses, the figure after the label up to the closing parenthesis: "(الحد المرجعي للمراقبة 7%–9%)".
    private static readonly Regex FigureInParenthesis = Rx(@"^ (?<fig>[^()]*?\d[^()]*?)(?=\))");
    private static readonly Regex FollowedByValue = Rx(@"^\s*\(?\s*[+-]?\d");

    public static IReadOnlyList<Indicator> From(CityReportFacts facts) => new Indicator[]
    {
        new(CagrName, Band(facts.Cagr.Reference)),
        new(IncreaseName, Threshold(facts.RentalIncrease.Reference)),
        new(RentToIncomeName, Threshold(facts.RentToIncome.Reference)),
        new(BurdenName, Threshold(facts.HousingBurden.Reference)),
        new(VacancyName, Threshold(facts.Vacancy.Reference)),
        new(YieldName, Threshold(facts.InvestmentReturn.Reference)),
        new(RentCpiName, ""),
        new(InflationName, ""),
    };

    /// <summary>The figure of a reference: "أكبر من 30%" → "30%", "6% فأقل" → "6%", "7%–9%" as is; "" when none.</summary>
    public static string Threshold(string? reference)
    {
        var r = (reference ?? "").Trim();
        if (r is "" or "—") return "";
        r = Regex.Replace(r, @"^(?:أكبر من|أكثر من|أقل من|[<>≤≥])\s*", "");
        r = Regex.Replace(r, @"\s*(?:فأكثر|فأقل|أو أقل|أو أكثر)$", "");
        return r.Trim();
    }

    /// <summary>The compound growth reference is a classification band ("10% فأكثر"): it keeps its qualifier, as the KPI card prints it.</summary>
    public static string Band(string? reference)
    {
        var r = (reference ?? "").Trim();
        return r is "—" ? "" : r;
    }

    /// <summary>A policy status word with the reference named in full: "دون الحد" → "دون الحد المرجعي للمراقبة".</summary>
    public static string StatusText(string status) => status is "ضمن الحد" or "دون الحد" ? $"{status} المرجعي للمراقبة" : status;

    /// <param name="fallbackThreshold">Used when the sentence names no indicator (an assessment axis's status).</param>
    public static string Apply(string? text, IReadOnlyList<Indicator> indicators, string? fallbackThreshold = null)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        text = ShortRef.Replace(text, m => $"({Label} {Range.Replace(m.Groups["v"].Value.Trim(), r => $"{r.Groups["a"].Value}%–{r.Groups["b"].Value}%")})");
        text = Unlabelled.Replace(text, m => m.Groups[1].Value + "حد المرجعي للمراقبة");
        text = Bare.Replace(text, m => m.Groups[1].Value + "حد المرجعي للمراقبة");
        text = PluralUnlabelled.Replace(text, "الحدود المرجعية للمراقبة");
        text = Plural.Replace(text, m => m.Groups[1].Value + "الحدود المرجعية للمراقبة");

        var position = 0;
        while (Labelled.Match(text, position) is { Success: true } m)
            (text, position) = WithThreshold(text, m.Index, m.Index + m.Length, indicators, fallbackThreshold);
        return text;
    }

    // Adds the threshold after the label ending at `end`; returns the text and the position to continue from.
    private static (string Text, int Next) WithThreshold(string text, int start, int end, IReadOnlyList<Indicator> indicators, string? fallback)
    {
        var sentenceStart = SentenceEnd.Matches(text[..start]).LastOrDefault() is { } boundary ? boundary.Index + boundary.Length : 0;
        var named = indicators
            .SelectMany(i => i.Name.Matches(text[sentenceStart..start]).Select(n => (i.Threshold, End: sentenceStart + n.Index + n.Length)))
            .OrderByDescending(n => n.End)
            .FirstOrDefault();
        var hasName = named.End > 0;
        var threshold = hasName ? named.Threshold : fallback ?? "";
        if (threshold == "") return (text, end);

        var rest = text[end..];
        var depth = text[..start].Count(c => c == '(') - text[..start].Count(c => c == ')');
        if (Parenthesis.Match(rest) is { Success: true } paren)
        {
            var inside = paren.Groups["v"].Value;
            if (Same(inside, threshold) || !SinglePercent.IsMatch(inside)) return (text, end + paren.Length);
            // The indicator's value written where the threshold belongs: the threshold takes its place. The value moves
            // next to the indicator's name; it is dropped when a value already follows the name, and kept after the
            // threshold when the text names no indicator (an axis status: "فوق الحد المرجعي للمراقبة (6%) عند 8.5%").
            var value = inside.Trim();
            var moved = hasName && !FollowedByValue.IsMatch(text[named.End..]);
            var head = moved ? text[..named.End] + $" ({value})" + text[named.End..end] : text[..end];
            var replaced = hasName ? $" ({threshold})" : $" ({threshold}) عند {value}";
            return (head + replaced + rest[paren.Length..], head.Length + replaced.Length);
        }
        // Inside parentheses a figure after the label is the threshold: it follows the named indicator's reference.
        if (depth > 0 && hasName && FigureInParenthesis.Match(rest) is { Success: true } fig)
        {
            if (Same(fig.Groups["fig"].Value, threshold)) return (text, end + fig.Length);
            var replaced = $" {threshold}";
            return (text[..end] + replaced + rest[fig.Length..], end + replaced.Length);
        }
        if (FigureAfter.IsMatch(rest)) return (text, end);
        // The clause already quotes the threshold a few words on: "على الحد بالضبط (30%)".
        var clause = ClauseEnd.Match(rest) is { Success: true } stop ? rest[..stop.Index] : rest;
        if (clause.Contains($"({threshold})", StringComparison.Ordinal)) return (text, end);

        // Within parentheses the threshold is not wrapped again: "(ضمن الحد المرجعي للمراقبة 5%)".
        var added = depth > 0 ? $" {threshold}" : $" ({threshold})";
        return (text[..end] + added + rest, end + added.Length);
    }

    private static bool Same(string a, string b) => a.Replace(" ", "") == b.Replace(" ", "");
}
