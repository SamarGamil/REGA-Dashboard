using AqarInsights.Api.Services.RentalControls.Projections;

namespace AqarInsights.Api.Tests.RentalControls;

public class MediaSentimentClassifierTests
{
    [Theory]
    [InlineData(4, 69, 27, "محايد")]    // Riyadh June: labelled سلبي in the workbook
    [InlineData(24, 67, 9, "محايد")]    // Makkah June: labelled سلبي in the workbook
    [InlineData(10, 39, 51, "سلبي")]    // Riyadh September
    [InlineData(17, 44, 39, "محايد")]
    [InlineData(62, 25, 13, "إيجابي")]
    [InlineData(50, 0, 50, "متباين")]   // positive = negative at the top
    [InlineData(33, 34, 33, "محايد")]
    [InlineData(34, 33, 33, "إيجابي")]
    [InlineData(0, 50, 50, "سلبي")]     // a direction tied with neutral wins
    [InlineData(50, 50, 0, "إيجابي")]
    [InlineData(0, 0, 0, "محايد")]
    public void Largest_share_decides(int pos, int neu, int neg, string expected)
        => Assert.Equal(expected, MediaSentimentClassifier.Classify(pos, neu, neg));
}
