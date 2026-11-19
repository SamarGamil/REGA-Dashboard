using System.Text.RegularExpressions;
using AqarInsights.Api.Utilities;

namespace AqarInsights.Api.Services.RentalControls.MonthlyUpdate;

public sealed record MediaCityMonth(string CityId, string OverallSentiment, int PositivePct, int NeutralPct,
    int NegativePct, double SharePct, IReadOnlyList<string> Topics, IReadOnlyList<(string Type, string Text)> Impressions);

// Reads the monthly citizens'-impressions deck (one analysis slide per city; README "Monthly data update").
// A content shape belongs to the nearest section header above it.
public static class MediaDeckParser
{
    private static readonly string AnalysisTitle = Key("تحليل الانطباعات");
    private static readonly string TopicsHeader = Key("أبرز الموضوعات المتداولة");
    private static readonly string TrendColumn = Key("الاتجاه العام");
    private static readonly string CityColumn = Key("المدينة");
    private static readonly HashSet<string> SummaryHeaders =
        new[] { "الملخص", "أبرز الموضوعات المتداولة", "أبرز التحديات المتداولة", "تحليل مدى معقولية الأسعار" }.Select(Key).ToHashSet();
    private static readonly Regex PageNumber = new(@"^\d+$");

    public static List<MediaCityMonth> Parse(IReadOnlyList<DeckSlide> slides, MonthlyUpdateReport report)
    {
        var shares = Shares(slides);
        var trends = Trends(slides);
        var result = new List<MediaCityMonth>();
        foreach (var slide in slides)
        {
            if (AnalysisCity(slide) is not { } cityId) continue;
            var topics = slides.FirstOrDefault(s => IsSummaryOf(s, cityId)) is { } summary ? Section(summary, TopicsHeader) : [];
            if (topics.Count == 0) report.Warnings.Add($"media {cityId}: no «أبرز الموضوعات المتداولة» found");
            if (!shares.TryGetValue(cityId, out var share)) report.Warnings.Add($"media {cityId}: no discussion share in the city chart");
            if (!trends.TryGetValue(cityId, out var trend)) report.Warnings.Add($"media {cityId}: no «الاتجاه العام» in the comparison table");
            var (pos, neu, neg) = Sentiment(slide, cityId, report);
            result.Add(new MediaCityMonth(cityId, trend ?? "", pos, neu, neg, share, topics, Impressions(slide)));
        }
        return result;
    }

    // Tatweel, repeated spaces and a trailing full stop are decoration; a leading dash is a bullet.
    public static string Clean(string text)
        => Regex.Replace(text.Replace("\u0640", ""), @"\s+", " ").Trim().TrimStart('-', '–', ' ').TrimEnd('.', '\u066B', ' ').Trim();

    private static string Key(string text) => ArabicNormalizer.Normalize(Clean(text));

    private static string? AnalysisCity(DeckSlide slide)
        => slide.Texts.SelectMany(t => t.Paragraphs).Select(Key)
            .Where(k => k.StartsWith(AnalysisTitle + " ", StringComparison.Ordinal))
            .Select(k => RentalControlsMappings.CityId(k[(AnalysisTitle.Length + 1)..]))
            .FirstOrDefault(id => id is not null);

    private static bool IsSummaryOf(DeckSlide slide, string cityId)
        => slide.Texts.Any(t => Key(t.Paragraphs[0]) == TopicsHeader)
           && slide.Texts.Any(t => t.Paragraphs.Count == 1 && RentalControlsMappings.CityId(Clean(t.Paragraphs[0])) == cityId);

    private static List<string> Section(DeckSlide slide, string header)
    {
        var headers = slide.Texts.Where(t => t.Paragraphs.Count == 1 && SummaryHeaders.Contains(Key(t.Paragraphs[0]))).ToList();
        return slide.Texts.Except(headers).Where(t => !IsPageNumber(t))
            .Where(t => NearestHeaderAbove(t, headers) is { } h && Key(h.Paragraphs[0]) == header)
            .SelectMany(t => t.Paragraphs).Select(Clean).Where(p => p.Length > 0).ToList();
    }

    private static List<(string Type, string Text)> Impressions(DeckSlide slide)
    {
        var headers = slide.Texts.Where(t => t.Paragraphs.Count == 1 && ImpressionType(t.Paragraphs[0]) is not null).ToList();
        var result = new List<(string, string)>();
        foreach (var text in slide.Texts.Except(headers))
        {
            if (IsPageNumber(text) || NearestHeaderAbove(text, headers) is not { } header) continue;
            var type = ImpressionType(header.Paragraphs[0])!;
            result.AddRange(text.Paragraphs.Select(Clean).Where(p => p.Length > 0).Select(p => (type, p)));
        }
        return result;
    }

    private static string? ImpressionType(string text)
    {
        var k = Key(text);
        if (!k.StartsWith("الانطباعات", StringComparison.Ordinal)) return null;
        return k.Contains("ايجابي") ? "positive" : k.Contains("سلبي") ? "negative" : k.Contains("محايد") ? "neutral" : null;
    }

    // Shares by series name, so the chart's series order does not matter; a missing point is 0.
    private static (int Pos, int Neu, int Neg) Sentiment(DeckSlide slide, string cityId, MonthlyUpdateReport report)
    {
        int? Pct(params string[] names)
            => slide.Series.FirstOrDefault(s => names.Any(n => Key(s.Name).Contains(n))) is { } s
                ? (int)Math.Round((s.Values.FirstOrDefault() ?? 0) * 100, MidpointRounding.AwayFromZero)
                : null;
        var (pos, neu, neg) = (Pct("ايجابي"), Pct("حيادي", "محايد"), Pct("سلبي"));
        if (pos is null || neu is null || neg is null)
            report.Warnings.Add($"media {cityId}: the sentiment chart lacks an إيجابي/حيادي/سلبي series");
        var (p, n, g) = (pos ?? 0, neu ?? 0, neg ?? 0);
        if (Math.Abs(p + n + g - 100) > 1) report.Warnings.Add($"media {cityId}: sentiment adds up to {p + n + g}%");
        return (p, n, g);
    }

    private static Dictionary<string, double> Shares(IReadOnlyList<DeckSlide> slides)
    {
        var shares = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var series in slides.SelectMany(s => s.Series))
        {
            var ids = series.Categories.Select(c => RentalControlsMappings.CityId(Clean(c))).ToList();
            if (ids.Count(id => id is not null) < 2) continue;
            for (var i = 0; i < ids.Count && i < series.Values.Count; i++)
                if (ids[i] is { } id && series.Values[i] is { } v) shares.TryAdd(id, Math.Round(v * 100, 1));
        }
        return shares;
    }

    private static Dictionary<string, string> Trends(IReadOnlyList<DeckSlide> slides)
    {
        var trends = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var slide in slides)
        {
            int trendCol = -1, cityCol = 0;
            foreach (var row in slide.TableRows)
            {
                var keys = row.Select(Key).ToList();
                if (keys.IndexOf(TrendColumn) is var found and >= 0)
                {
                    (trendCol, cityCol) = (found, Math.Max(0, keys.IndexOf(CityColumn)));
                    continue;
                }
                if (trendCol >= 0 && trendCol < row.Count && cityCol < row.Count
                    && RentalControlsMappings.CityId(Clean(row[cityCol])) is { } id)
                    trends.TryAdd(id, Clean(row[trendCol]));
            }
        }
        return trends;
    }

    private static DeckText? NearestHeaderAbove(DeckText text, IEnumerable<DeckText> headers)
        => headers.Where(h => h.Y < text.Y)
            .OrderBy(h => Math.Pow(h.X - text.X, 2) + Math.Pow(h.Y - text.Y, 2))
            .FirstOrDefault();

    private static bool IsPageNumber(DeckText text) => text.Paragraphs.All(p => PageNumber.IsMatch(Clean(p)));
}
