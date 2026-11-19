namespace AqarInsights.Api.Services.RentalControls.Projections;

/// <summary>
/// Overall media impression derived from the sentiment shares (the workbook's own label often contradicts them):
/// the largest share wins; positive = negative at the top → متباين; a direction tied with neutral wins over it.
/// </summary>
public static class MediaSentimentClassifier
{
    public const string Positive = "إيجابي";
    public const string Neutral = "محايد";
    public const string Negative = "سلبي";
    public const string Mixed = "متباين";

    public static string Classify(int positivePct, int neutralPct, int negativePct)
    {
        var top = Math.Max(positivePct, Math.Max(neutralPct, negativePct));
        if (top <= 0) return Neutral;
        if (positivePct == top && negativePct == top) return Mixed;
        if (negativePct == top) return Negative;
        if (positivePct == top) return Positive;
        return Neutral;
    }
}
