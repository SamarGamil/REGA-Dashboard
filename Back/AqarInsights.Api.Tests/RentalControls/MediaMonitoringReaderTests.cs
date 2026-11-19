using ClosedXML.Excel;
using AqarInsights.Api.Services.RentalControls.Reading;

namespace AqarInsights.Api.Tests.RentalControls;

public class MediaMonitoringReaderTests
{
    // Period-aware workbook: riyadh has two months (2026-06, 2026-07); najran only 2026-06.
    private static XLWorkbook BuildWorkbook()
    {
        var wb = new XLWorkbook();

        var cities = wb.AddWorksheet("المدن");
        var ch = new[] { "period", "cityId", "available", "overallSentiment", "positivePct", "neutralPct", "negativePct", "discussionSharePct" };
        for (var c = 0; c < ch.Length; c++) cities.Cell(1, c + 1).Value = ch[c];
        WriteCity(cities, 2, "2026-06", "riyadh", true, "سلبي", 4, 69, 27, 88.8);
        WriteCity(cities, 3, "2026-07", "riyadh", true, "سلبي", 10, 60, 30, 78.9);
        WriteCity(cities, 4, "2026-06", "najran", true, "محايد", 0, 100, 0, 0.7);

        var topics = wb.AddWorksheet("الموضوعات");
        foreach (var (col, head) in new[] { (1, "period"), (2, "cityId"), (3, "topic") }) topics.Cell(1, col).Value = head;
        WriteRow(topics, 2, "2026-06", "riyadh", "ارتفاع أسعار الإيجارات");
        WriteRow(topics, 3, "2026-07", "riyadh", "مؤشرات تراجع أسعار الإيجارات");
        WriteRow(topics, 4, "2026-06", "unknown-city", "موضوع مهمل");   // ignored

        var imp = wb.AddWorksheet("الانطباعات");
        foreach (var (col, head) in new[] { (1, "period"), (2, "cityId"), (3, "type"), (4, "text") }) imp.Cell(1, col).Value = head;
        WriteImp(imp, 2, "2026-06", "riyadh", "positive", "انطباع إيجابي يونيو");
        WriteImp(imp, 3, "2026-06", "riyadh", "neutral", "انطباع محايد يونيو");
        WriteImp(imp, 4, "2026-06", "riyadh", "negative", "انطباع سلبي يونيو");
        WriteImp(imp, 5, "2026-07", "riyadh", "neutral", "انطباع محايد يوليو");
        WriteImp(imp, 6, "2026-06", "najran", "neutral", "محتوى خبري");

        return wb;
    }

    private static void WriteCity(IXLWorksheet s, int r, string period, string city, bool avail, string sent, int pos, int neu, int neg, double share)
    {
        s.Cell(r, 1).Value = period; s.Cell(r, 2).Value = city; s.Cell(r, 3).Value = avail;
        s.Cell(r, 4).Value = sent; s.Cell(r, 5).Value = pos; s.Cell(r, 6).Value = neu;
        s.Cell(r, 7).Value = neg; s.Cell(r, 8).Value = share;
    }

    private static void WriteRow(IXLWorksheet s, int r, string period, string city, string text)
    {
        s.Cell(r, 1).Value = period; s.Cell(r, 2).Value = city; s.Cell(r, 3).Value = text;
    }

    private static void WriteImp(IXLWorksheet s, int r, string period, string city, string type, string text)
    {
        s.Cell(r, 1).Value = period; s.Cell(r, 2).Value = city; s.Cell(r, 3).Value = type; s.Cell(r, 4).Value = text;
    }

    [Fact]
    public void Read_ParsesMonthlySeriesPerCity()
    {
        using var wb = BuildWorkbook();

        var result = MediaMonitoringReader.Read(wb);

        Assert.Equal(2, result.Count);

        var riyadh = result["riyadh"];
        Assert.Equal(2, riyadh.Count);
        Assert.Equal(new[] { "2026-06", "2026-07" }, riyadh.Select(m => m.Period));   // chronological

        var june = riyadh[0];
        Assert.Equal(4, june.PositivePct);
        Assert.Equal(69, june.NeutralPct);
        Assert.Equal(88.8, june.DiscussionSharePct, 3);
        Assert.Equal(new[] { "ارتفاع أسعار الإيجارات" }, june.Topics);
        Assert.Equal(new[] { "انطباع إيجابي يونيو" }, june.PositiveImpressions);
        Assert.Equal(new[] { "انطباع محايد يونيو" }, june.NeutralImpressions);
        Assert.Equal(new[] { "انطباع سلبي يونيو" }, june.NegativeImpressions);

        var july = riyadh[1];
        Assert.Equal(10, july.PositivePct);
        Assert.Equal(new[] { "مؤشرات تراجع أسعار الإيجارات" }, july.Topics);
        Assert.Equal(new[] { "انطباع محايد يوليو" }, july.NeutralImpressions);
        Assert.Empty(july.PositiveImpressions);

        var najran = result["najran"];
        Assert.Single(najran);
        Assert.Equal("2026-06", najran[0].Period);
        Assert.Single(najran[0].NeutralImpressions);
    }

    [Fact]
    public void Read_IgnoresTopicsForUnknownCities()
    {
        using var wb = BuildWorkbook();

        var result = MediaMonitoringReader.Read(wb);

        Assert.DoesNotContain("unknown-city", result.Keys);
        Assert.All(result.Values, months => Assert.All(months, m => Assert.DoesNotContain("موضوع مهمل", m.Topics)));
    }

    [Fact]
    public void Read_NoPeriodColumn_TreatsAllRowsAsSingleMonth()
    {
        using var wb = new XLWorkbook();
        var cities = wb.AddWorksheet("المدن");
        var ch = new[] { "cityId", "available", "overallSentiment", "positivePct", "neutralPct", "negativePct", "discussionSharePct" };
        for (var c = 0; c < ch.Length; c++) cities.Cell(1, c + 1).Value = ch[c];
        cities.Cell(2, 1).Value = "riyadh"; cities.Cell(2, 2).Value = true; cities.Cell(2, 3).Value = "سلبي";
        cities.Cell(2, 4).Value = 4; cities.Cell(2, 5).Value = 69; cities.Cell(2, 6).Value = 27; cities.Cell(2, 7).Value = 88.8;

        var topics = wb.AddWorksheet("الموضوعات");
        topics.Cell(1, 1).Value = "cityId"; topics.Cell(1, 2).Value = "topic";
        topics.Cell(2, 1).Value = "riyadh"; topics.Cell(2, 2).Value = "موضوع";

        var imp = wb.AddWorksheet("الانطباعات");
        imp.Cell(1, 1).Value = "cityId"; imp.Cell(1, 2).Value = "type"; imp.Cell(1, 3).Value = "text";
        imp.Cell(2, 1).Value = "riyadh"; imp.Cell(2, 2).Value = "neutral"; imp.Cell(2, 3).Value = "انطباع";

        var result = MediaMonitoringReader.Read(wb);

        var riyadh = Assert.Single(result["riyadh"]);
        Assert.Equal("", riyadh.Period);
        Assert.Equal(new[] { "موضوع" }, riyadh.Topics);
        Assert.Equal(new[] { "انطباع" }, riyadh.NeutralImpressions);
    }

    [Fact]
    public void Read_ThrowsWhenSheetMissing()
    {
        using var wb = new XLWorkbook();
        wb.AddWorksheet("المدن");   // only one of three required sheets

        Assert.Throws<RentalControlsReadException>(() => MediaMonitoringReader.Read(wb));
    }
}
