using ClosedXML.Excel;
using AqarInsights.Api.Services.RentalControls.MonthlyUpdate;
using AqarInsights.Api.Services.RentalControls.Reading;
using AqarInsights.Api.Services.RentalControls.Snapshots;

namespace AqarInsights.Api.Tests.RentalControls.MonthlyUpdate;

public class MediaDeckParserTests
{
    private static readonly IReadOnlyList<IReadOnlyList<string>> NoTable = [];

    private static List<DeckSlide> DammamDeck() =>
    [
        new(4, [], [new("share", ["الرياض", "الدمام"], [0.81, 1.7000000000000001E-2])], NoTable),
        new(14,
        [
            new(5345238, 400946, ["الـــدمام"]),
            new(4296109, 403997, ["1.7%"]),
            new(6166082, 1224918, ["الملـــخــــــــص"]),
            new(325999, 1224918, ["أبــرز الموضــوعــــات المتـــداولـــة"]),
            new(6166082, 3554043, ["أبــرز التحديـــات المتـــداولـــة"]),
            new(325999, 3554043, ["تحليــــل مـــدى معقـــوليـــة الأسعـــار"]),
            new(999744, 1728770, ["مقارنة الإيجارات مع الرياض. ", "الانتقال من الرياض إلى الدمام."]),
            new(6210512, 4055296, ["تحدٍ لا يدخل في الموضوعات."]),
            new(6289514, 1728770, ["نص الملخص."]),
            new(11534296, 6395604, ["14"]),
        ], [], NoTable),
        new(15,
        [
            new(2956560, 514434, ["تحليـل الانطبـاعـات الـــدمام"]),
            new(11534296, 6395604, ["15"]),
            new(4711628, 2037116, ["الانطبـــاعـــــات المحـــــــــايدة"]),
            new(4237520, 3327430, ["مناقشة الفروق بين الدمام والخبر.", "- طرح فرص استثمارية."]),
            new(8485095, 2030332, ["الانطبـــاعـــات الإيجابيــــة"]),
            new(990657, 2054597, ["الانطباعـات السلبيــــة"]),
            new(8055034, 3596473, ["اعتبار الدمام خيارًا أقل تكلفة."]),
            new(965860, 2054597, ["الانطبـــاعــــات السلبـيــة"]),
            new(487385, 3647710, ["القلق من الالتزامات المالية."]),
        ],
        [new("سلبي", ["الموضوع ١"], [0.3]), new("إيجابي", ["الموضوع ١"], [0.2]), new("حيادي", ["الموضوع ١"], [0.5])],
        NoTable),
        new(32, [], [],
        [
            ["المدينة", "مستوى النقاش", "الاتجاه العام", "أبرز موضوع متداول"],
            ["الـــدمـــــــــــــــام", "محدود", "متباين", "المقارنة مع الرياض والخبر"],
        ]),
    ];

    [Fact]
    public void Parse_ReadsSentimentByName_ShareLabelTopicsAndImpressionColumns()
    {
        var report = new MonthlyUpdateReport();

        var city = Assert.Single(MediaDeckParser.Parse(DammamDeck(), report));

        Assert.Equal("dammam", city.CityId);
        Assert.Equal((20, 50, 30), (city.PositivePct, city.NeutralPct, city.NegativePct));
        Assert.Equal(1.7, city.SharePct);
        Assert.Equal("متباين", city.OverallSentiment);
        Assert.Equal(new[] { "مقارنة الإيجارات مع الرياض", "الانتقال من الرياض إلى الدمام" }, city.Topics);
        Assert.Equal(new[]
        {
            ("neutral", "مناقشة الفروق بين الدمام والخبر"), ("neutral", "طرح فرص استثمارية"),
            ("positive", "اعتبار الدمام خيارًا أقل تكلفة"), ("negative", "القلق من الالتزامات المالية"),
        }, city.Impressions.OrderBy(i => i.Type == "neutral" ? 0 : i.Type == "positive" ? 1 : 2));
        Assert.Empty(report.Warnings);
    }

    [Fact]
    public void Write_ReplacesOnlyTheMonth_AndMediaReaderReadsIt()
    {
        using var wb = MediaWorkbook();
        var city = MediaDeckParser.Parse(DammamDeck(), new MonthlyUpdateReport());
        var report = new MonthlyUpdateReport();

        MediaWorkbookWriter.Write(wb, new YearMonth(2026, 10), city, report);

        var months = MediaMonitoringReader.Read(wb)["dammam"].OrderBy(m => m.Period).ToList();
        Assert.Equal(new[] { "2026-09", "2026-10" }, months.Select(m => m.Period));
        var october = months[1];
        Assert.Equal((20, 50, 30, 1.7), (october.PositivePct, october.NeutralPct, october.NegativePct, october.DiscussionSharePct));
        Assert.Equal(2, october.Topics.Count);
        Assert.Equal(1, wb.Worksheet("المدن").RowsUsed().Count(r => r.Cell(1).GetString() == "2026-10"));
    }

    private static XLWorkbook MediaWorkbook()
    {
        var wb = new XLWorkbook();
        var cities = wb.AddWorksheet("المدن");
        string[] h = ["period", "cityId", "available", "overallSentiment", "positivePct", "neutralPct", "negativePct", "discussionSharePct"];
        for (var c = 0; c < h.Length; c++) cities.Cell(1, c + 1).Value = h[c];
        (string Period, string Sentiment, int Pos, int Neu, int Neg, double Share)[] rows =
            [("2026-09", "متوازن", 25, 42, 33, 0.8), ("2026-10", "قديم", 1, 1, 98, 9.9)];
        for (var r = 0; r < rows.Length; r++)
        {
            var x = cities.Row(r + 2);
            x.Cell(1).Value = rows[r].Period; x.Cell(2).Value = "dammam"; x.Cell(3).Value = true;
            x.Cell(4).Value = rows[r].Sentiment; x.Cell(5).Value = rows[r].Pos; x.Cell(6).Value = rows[r].Neu;
            x.Cell(7).Value = rows[r].Neg; x.Cell(8).Value = rows[r].Share;
        }
        var topics = wb.AddWorksheet("الموضوعات");
        topics.Cell(1, 1).Value = "period"; topics.Cell(1, 2).Value = "cityId"; topics.Cell(1, 3).Value = "topic";
        topics.Cell(2, 1).Value = "2026-09"; topics.Cell(2, 2).Value = "dammam"; topics.Cell(2, 3).Value = "موضوع سبتمبر";
        topics.Cell(3, 1).Value = "2026-10"; topics.Cell(3, 2).Value = "dammam"; topics.Cell(3, 3).Value = "موضوع قديم";
        var impressions = wb.AddWorksheet("الانطباعات");
        impressions.Cell(1, 1).Value = "period"; impressions.Cell(1, 2).Value = "cityId"; impressions.Cell(1, 3).Value = "type"; impressions.Cell(1, 4).Value = "text";
        impressions.Cell(2, 1).Value = "2026-09"; impressions.Cell(2, 2).Value = "dammam"; impressions.Cell(2, 3).Value = "neutral"; impressions.Cell(2, 4).Value = "انطباع";
        return wb;
    }
}
