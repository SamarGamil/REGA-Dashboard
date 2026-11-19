using DocumentFormat.OpenXml.Packaging;
using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Reports;
using Xunit;

namespace AqarInsights.Api.Tests.RentalControls.Reports;

public class CityReportRendererTests
{
    private static CityReportContent Content() => new()
    {
        CityId = "riyadh",
        ExecutiveBullets = new() { "المعدل السنوي المركب 9.7% هو الأعلى وطنيًا." },
        IndicatorsBullets = new() { "مقدمة لوحة المؤشرات." },
        MarketBullets = new() { "يواصل عدد الصفقات الإيجارية ارتفاعه." },
        ProductTable = new()
        {
            new() { Product = "شقة", RentalDeals = "+39%", SalesDeals = "-38%", FisherIndex = "-7%" },
        },
        CityAveragesLine = "متوسط قيمة الصفقة الإيجارية: 33,157 ريال/وحدة (-4%).",
        MediaBullets = new() { "ملخص الرصد الإعلامي." },
        ConclusionBullets = new() { "استنتاج أول." },
        AssessmentAxes = new() { new() { Axis = "ضغط السوق", Status = "مرتفع" } },
        RecommendationBullets = new() { "الإبقاء على الضوابط مفعّلة." },
        WatchList = new() { "مسار نمو الإيجارات" },
    };

    private static CityReportIndicatorFact F(string label, string value, string reference, string status, string month = "", string frequency = "")
        => new() { Label = label, Value = value, Reference = reference, Status = status, Month = month, Period = ReportPeriodText.Word(month), Frequency = frequency };

    private static CityReportFacts Facts() => new()
    {
        OperationalStatus = "يتطلب دراسة استباقية",
        Classification = "نمو مستدام",
        ClassificationLabel = "sustainable",
        Recommendation = "مراقبة - العائد 5%",
        Cagr = F("معدل النمو الايجاري المركب", "2.4%", "≤6%", "نمو مستدام"),
        RentalIncrease = F("الزيادة الإيجارية", "+3%", "أكبر من 6%", "ضمن الحد"),
        RentToIncome = F("نسبة الإيجار إلى الدخل", "12%", "أكبر من 30%", "ضمن الحد"),
        HousingBurden = F("العبء السكني لذوي الدخل المحدود", "24%", "أكبر من 30%", "ضمن الحد"),
        Vacancy = F("الشواغر الإيجارية", "7%", "أقل من 5%", "ضمن الحد"),
        InvestmentReturn = F("العائد الإيجاري", "5%", "أقل من 6%", "دون الحد"),
        ActualRentCpi = F("الإيجار الفعلي للمسكن (CPI)", "+5.3%", "—", "ارتفاع", "يونيو 2026", ReportFrequency.Monthly),
        GeneralInflation = F("التضخم العام (CPI)", "+1.5%", "—", "مستقر", "أغسطس 2026", ReportFrequency.Monthly),
    };

    private static string WordBodyText(CityReportModel m)
    {
        using var ms = new MemoryStream(CityReportWordRenderer.Render(m));
        using var doc = WordprocessingDocument.Open(ms, false);
        return doc.MainDocumentPart!.Document.Body!.InnerText;
    }

    private static CityAnalysisData City(bool mediaAvailable = true) => new()
    {
        CityName = "الرياض",
        Period = "الربع الثاني 2026",
        MediaMonitoring = mediaAvailable
            ? new MediaMonitoring { Available = true, OverallSentiment = "سلبي", PositivePct = 4, NeutralPct = 69, NegativePct = 27 }
            : new MediaMonitoring { Available = false },
    };

    private static CityReportModel Model(bool mediaAvailable = true)
        => CityReportModelBuilder.Build(City(mediaAvailable), Facts(), Content(), new DateOnly(2026, 9, 6));

    [Fact]
    public void Builder_maps_content_and_media()
    {
        var m = Model();
        Assert.Equal("تقرير الضوابط الإيجارية – الرياض", m.HeaderTitle);
        Assert.Equal("مؤشرات مدينة الرياض – الربع الثاني 2026", m.HeaderSubtitle);
        Assert.Equal("الربع الثاني 2026", m.PeriodLabel);
        Assert.True(m.MediaAvailable);
        // Client review: the overall impression follows the largest share (69% محايد), whatever the source label says.
        Assert.Equal("محايد", m.MediaSentiment);
        Assert.Equal("#1CA59A", m.MediaSentimentColorHex);
        Assert.Contains("69%", m.MediaBreakdown);
        Assert.Equal(new[] { "ملخص الرصد الإعلامي." }, m.MediaBullets);
    }

    [Theory]
    [InlineData(10, 39, 51, "سلبي", "#DC2626", "السلبي")]
    [InlineData(50, 0, 50, "متباين", "#C67A1E", "متباينة")]
    [InlineData(62, 25, 13, "إيجابي", "#2E9B5B", "الإيجابي")]
    [InlineData(0, 67, 33, "محايد", "#1CA59A", "المحايد")]
    public void Builder_media_sentiment_and_default_bullet_follow_the_shares(int pos, int neu, int neg, string sentiment, string color, string bulletWord)
    {
        var city = City();
        (city.MediaMonitoring.PositivePct, city.MediaMonitoring.NeutralPct, city.MediaMonitoring.NegativePct) = (pos, neu, neg);
        var content = Content();
        content.MediaBullets = new();

        var m = CityReportModelBuilder.Build(city, Facts(), content, new DateOnly(2026, 9, 6));

        Assert.Equal(sentiment, m.MediaSentiment);
        Assert.Equal(color, m.MediaSentimentColorHex);
        Assert.Contains(bulletWord, Assert.Single(m.MediaBullets));
    }

    [Fact]
    public void Monthly_report_labels_the_cpi_rows_with_their_own_period_word()
    {
        // Report month August: inflation (August) reads "أغسطس"; actual rent (last figure June) reads "يونيو".
        var m = CityReportModelBuilder.Build(City(), Facts(), Content(), new DateOnly(2026, 9, 6), "أغسطس 2026", isMonthly: true);

        var rows = m.IndicatorRows.ToDictionary(r => r.Indicator);
        Assert.Equal("أغسطس", rows["التضخم العام (CPI)"].Period);
        Assert.Equal("يونيو", rows["الإيجار الفعلي للمسكن (CPI)"].Period);
    }

    [Fact]
    public void Quarterly_report_labels_the_cpi_rows_with_their_month_word()
    {
        var m = CityReportModelBuilder.Build(City(), Facts(), Content(), new DateOnly(2026, 9, 6), "الربع الثالث 2026", isMonthly: false);

        Assert.Contains(m.IndicatorRows, r => r.Indicator == "التضخم العام (CPI)" && r.Period == "أغسطس");
    }

    [Fact]
    public void Builder_caps_media_bullets_at_three()
    {
        var content = Content();
        content.MediaBullets = new() { "1", "2", "3", "4" };
        var m = CityReportModelBuilder.Build(City(), Facts(), content, new DateOnly(2026, 9, 6));
        Assert.Equal(3, m.MediaBullets.Count);
    }

    [Fact]
    public void Builder_uses_not_available_note_when_media_missing()
    {
        var m = Model(mediaAvailable: false);
        Assert.False(m.MediaAvailable);
        Assert.Contains("لا تتوفر بيانات رصد إعلامي لمدينة الرياض", Assert.Single(m.MediaBullets));
    }

    [Fact]
    public void Builder_binds_market_state_and_assessment_to_platform_status()
    {
        var m = Model();
        Assert.Equal("يتطلب دراسة استباقية", m.MarketState);
        Assert.Equal("#123A52", m.MarketStateColorHex);
        Assert.Equal("يتطلب دراسة استباقية — نمو مستدام", m.OverallAssessment);
    }

    [Fact]
    public void Builder_builds_kpi_cards_from_facts()
    {
        var cards = Model().KpiCards;
        Assert.Equal(new[] { "معدل النمو الايجاري المركب", "العبء السكني لذوي الدخل المحدود", "العائد الإيجاري",
            "الإيجار الفعلي للمسكن CPI (التغير السنوي)" }, cards.Select(c => c.Label));
        Assert.Equal(("5%", "دون الحد المرجعي للمراقبة", "الحد المرجعي للمراقبة: أقل من 6%", "#C67A1E"), (cards[2].ValueText, cards[2].Note, cards[2].Reference, cards[2].ColorHex));
        Assert.Equal(("ارتفاع", ""), (cards[3].Note, cards[3].Reference));
    }

    // Client review (Riyadh, August 2026, notes 10–11): the yield shows one status everywhere — the policy status
    // against the reference printed on the card — not the city page's «محايد».
    [Fact]
    public void Yield_status_is_the_same_in_the_kpi_card_supply_strip_and_axis()
    {
        var city = City();
        city.Kpis.Add(new KpiItem { Label = "العائد الإيجاري", Value = "5%", Status = "محايد" });
        var content = Content();
        content.AssessmentAxes.Add(new CityReportAxis { Axis = "الاستثمار", Status = "العائد الإيجاري دون الحد المرجعي" });

        var m = CityReportModelBuilder.Build(city, Facts(), content, new DateOnly(2026, 9, 6));

        Assert.Equal("دون الحد المرجعي للمراقبة", m.KpiCards.Single(c => c.Label == "العائد الإيجاري").Note);
        Assert.Equal("دون الحد المرجعي للمراقبة", m.SupplyInvestment.Single(s => s.Label == "العائد الإيجاري").Note);
        Assert.Equal("العائد الإيجاري دون الحد المرجعي للمراقبة (6%)", m.Axes.Single(a => a.Axis == "الاستثمار").Status);
        Assert.Equal(m.KpiCards.Single(c => c.Label == "العائد الإيجاري").ColorHex, m.SupplyInvestment.Single(s => s.Label == "العائد الإيجاري").ColorHex);
    }

    // Client review notes 8, 11, 13, 15, 17: every reference in the narrative and the platform line reads
    // «الحد المرجعي للمراقبة» with the indicator's threshold.
    [Fact]
    public void Narrative_references_name_the_monitoring_reference_and_threshold()
    {
        var content = Content();
        content.ExecutiveBullets = new() { "العائد الإيجاري (5%) دون الحد." };
        content.ConclusionBullets = new() { "نسبة الإيجار إلى الدخل ضمن الحد (12%)." };
        content.WatchList = new() { "مسار معدل النمو الإيجاري المركب (حد 7-9%)." };
        content.AssessmentAxes = new() { new() { Axis = "القدرة على تحمل السكن", Status = "ضمن الحد ومستقرة" } };
        var facts = Facts() with { Recommendation = "مراقبة - العبء ذوي الدخل المحدود 24% دون الحد" };

        var m = CityReportModelBuilder.Build(City(), facts, content, new DateOnly(2026, 9, 6));

        Assert.Equal("العائد الإيجاري (5%) دون الحد المرجعي للمراقبة (6%).", Assert.Single(m.ExecutiveBullets));
        Assert.Equal("نسبة الإيجار إلى الدخل (12%) ضمن الحد المرجعي للمراقبة (30%).", Assert.Single(m.ConclusionBullets));
        // The watch item's band follows the city's own compound growth reference (≤6% here), as its KPI card shows it.
        Assert.Equal("مسار معدل النمو الإيجاري المركب (الحد المرجعي للمراقبة ≤6%).", Assert.Single(m.WatchList));
        Assert.Equal("ضمن الحد المرجعي للمراقبة (30%) ومستقرة", Assert.Single(m.Axes).Status);
        Assert.Equal("يتطلب دراسة استباقية: مراقبة - العبء السكني لذوي الدخل المحدود 24% دون الحد المرجعي للمراقبة (30%).", m.RecommendationBullets[0]);
    }

    // Client review follow-up: the market-pressure axis reads the city page's «حالة السوق» badge wording, so the
    // row's word always agrees with its classification colour, whatever the curated axis text says.
    [Theory]
    [InlineData("نمو متسارع", "accelerated", "تحت ضغط مرتفع", "#DC2626")]
    [InlineData("نمو مرتفع", "elevated", "نمو مرتفع", "#C67A1E")]
    [InlineData("نمو مستدام", "sustainable", "ضمن الحدود المرجعية للمراقبة", "#2E9B5B")]
    public void Market_pressure_axis_wording_follows_the_platform_market_state_badge(
        string classification, string label, string status, string color)
    {
        var facts = Facts() with { Classification = classification, ClassificationLabel = label };

        var m = CityReportModelBuilder.Build(City(), facts, Content(), new DateOnly(2026, 9, 6));

        var axis = m.Axes.Single(a => a.Axis == "ضغط السوق");
        Assert.Equal(status, axis.Status);
        Assert.Equal(color, axis.ColorHex);
    }

    // Client review note 14: the platform line's "بتراجع 0.8 نقطة" names the period, as the city page states it.
    [Fact]
    public void Point_change_in_the_platform_line_names_its_period_from_the_city_page()
    {
        var city = City();
        city.ExecutiveSummary.Bullets.Add("\u200f1. نمو مرتفع للمعدل السنوي المركب لأسعار الإيجار (2022-2026)( 8.9%) حتى الربع الثالث 2026 بتراجع 0.8 نقطة عن الربع السابق.");
        var facts = Facts() with { OperationalStatus = "الضوابط مفعلة", Recommendation = "المعدل السنوي المركب 8.9% (نمو مرتفع) بتراجع 0.8 نقطة والعبء السكني للدخل المحدود 35% يتجاوز الحد" };

        var m = CityReportModelBuilder.Build(city, facts, Content(), new DateOnly(2026, 9, 6));

        Assert.Equal("الضوابط مفعلة: معدل النمو الإيجاري المركب 8.9% (نمو مرتفع) بتراجع 0.8 نقطة عن الربع السابق والعبء السكني لذوي الدخل المحدود 35% يتجاوز الحد المرجعي للمراقبة (30%).",
            m.RecommendationBullets[0]);
    }

    [Fact]
    public void Point_change_period_keeps_decimals_of_the_city_page_phrase()
    {
        var facts = Facts() with { Recommendation = "المعدل السنوي المركب 8.9% بتراجع 0.8 نقطة" };
        var city = City();
        city.ExecutiveSummary.Bullets.Add("حتى الربع الثالث 2026 بتراجع 0.8 نقطة عن مستوى 9.7% في الربع السابق.");

        var m = CityReportModelBuilder.Build(city, facts, Content(), new DateOnly(2026, 9, 6));

        Assert.EndsWith("بتراجع 0.8 نقطة عن مستوى 9.7% في الربع السابق.", m.RecommendationBullets[0]);
    }

    // A monthly report does not carry the platform line's quarter-on-quarter change (any city): the month shows its
    // quarter's level, not the change between quarters.
    [Fact]
    public void Monthly_report_drops_the_quarter_change_from_the_platform_line()
    {
        var city = City();
        city.ExecutiveSummary.Bullets.Add("حتى الربع الثالث 2026 بتراجع 0.8 نقطة عن الربع السابق.");
        var facts = Facts() with { OperationalStatus = "الضوابط مفعلة", Recommendation = "المعدل السنوي المركب 8.9% (نمو مرتفع) بتراجع 0.8 نقطة والعبء السكني للدخل المحدود 35% يتجاوز الحد" };

        var monthly = CityReportModelBuilder.Build(city, facts, Content(), new DateOnly(2026, 9, 6), "أغسطس 2026", isMonthly: true);
        var quarterly = CityReportModelBuilder.Build(city, facts, Content(), new DateOnly(2026, 9, 6), "الربع الثالث 2026", isMonthly: false);

        Assert.Equal("الضوابط مفعلة: معدل النمو الإيجاري المركب 8.9% (نمو مرتفع) والعبء السكني لذوي الدخل المحدود 35% يتجاوز الحد المرجعي للمراقبة (30%).",
            monthly.RecommendationBullets[0]);
        Assert.Contains("بتراجع 0.8 نقطة عن الربع السابق", quarterly.RecommendationBullets[0]);
    }

    [Theory]
    [InlineData("المعدل السنوي المركب 3.9% دون تغير عن الربع السابق والعبء السكني للدخل المحدود 26%", "معدل النمو الإيجاري المركب 3.9% والعبء السكني لذوي الدخل المحدود 26%.")]
    [InlineData("المعدل السنوي المركب 5.2% بارتفاع 0.3 نقطة مئوية عن الربع الثاني 2026", "معدل النمو الإيجاري المركب 5.2%.")]
    public void Monthly_report_drops_any_form_of_the_quarter_change(string platformLine, string expectedTail)
    {
        var m = CityReportModelBuilder.Build(City(), Facts() with { Recommendation = platformLine }, Content(), new DateOnly(2026, 9, 6), "أغسطس 2026", isMonthly: true);
        Assert.Equal($"يتطلب دراسة استباقية: {expectedTail}", m.RecommendationBullets[0]);
    }

    // A monthly report drops the narrative's quarter-on-quarter comparisons (all cities), keeping the rest of each sentence;
    // a quarterly report keeps them.
    [Theory]
    [InlineData("العبء السكني للدخل المحدود (36%) فوق الحد المرجعي (30%) وارتد صعودًا في الربع الثالث (34% ← 36%) بعد تحسن سابق.",
        "العبء السكني لذوي الدخل المحدود (36%) فوق الحد المرجعي للمراقبة (30%).")]
    [InlineData("العبء السكني للدخل المحدود (37%) فوق الحد وارتفع بحدة في الربع الثالث (31% ← 37%) في أصغر سوق مرصودة.",
        "العبء السكني لذوي الدخل المحدود (37%) فوق الحد المرجعي للمراقبة (30%) في أصغر سوق مرصودة.")]
    [InlineData("العبء السكني للدخل المحدود (35%) فوق الحد المرجعي واستقر في الربع الثالث بعد تحسن سابق (37% ← 35%).",
        "العبء السكني لذوي الدخل المحدود (35%) فوق الحد المرجعي للمراقبة (30%).")]
    [InlineData("تؤكد الزيادة الإيجارية الأخيرة (9%) استمرار الزخم رغم تراجعها من 14% في الربع السابق.",
        "تؤكد الزيادة الإيجارية الأخيرة (9%) استمرار الزخم.")]
    public void Monthly_report_drops_quarter_comparisons_from_the_narrative(string sentence, string monthly)
    {
        var content = Content();
        content.ExecutiveBullets = new() { sentence };

        var month = CityReportModelBuilder.Build(City(), Facts(), content, new DateOnly(2026, 9, 6), "أغسطس 2026", isMonthly: true);
        var quarter = CityReportModelBuilder.Build(City(), Facts(), content, new DateOnly(2026, 9, 6), "الربع الثالث 2026", isMonthly: false);

        Assert.Equal(monthly, Assert.Single(month.ExecutiveBullets));
        Assert.Contains("الربع", Assert.Single(quarter.ExecutiveBullets));
    }

    [Fact]
    public void Point_change_is_left_as_is_when_the_city_page_does_not_state_it()
    {
        var facts = Facts() with { Recommendation = "المعدل السنوي المركب 8.9% بتراجع 0.5 نقطة" };
        var city = City();
        city.ExecutiveSummary.Bullets.Add("حتى الربع الثالث 2026 بتراجع 0.8 نقطة عن الربع السابق.");

        var m = CityReportModelBuilder.Build(city, facts, Content(), new DateOnly(2026, 9, 6));

        Assert.EndsWith("بتراجع 0.5 نقطة.", m.RecommendationBullets[0]);
    }

    // Client review follow-up: a rising actual rent («ارتفاع») is red in the indicators table as on the KPI card and the
    // supply strip (one figure, one colour); general inflation keeps the status palette.
    [Fact]
    public void Actual_rent_row_takes_the_card_colour()
    {
        var m = Model();
        var row = m.IndicatorRows.Single(r => r.Indicator.StartsWith("الإيجار الفعلي"));
        var card = m.KpiCards.Single(c => c.Label.StartsWith("الإيجار الفعلي"));

        Assert.Equal(("ارتفاع", "#DC2626", "#FDECEC"), (row.Status, row.StatusColorHex, row.StatusBgHex));
        Assert.Equal(card.ColorHex, row.StatusColorHex);
        Assert.Equal(card.ColorHex, m.SupplyInvestment.Single(s => s.Label.StartsWith("الإيجار الفعلي")).ColorHex);
    }

    // A falling actual rent («تراجع») is green (no pressure signal) in every place, so the national table reads it as
    // «ضمن الحد المرجعي للمراقبة», not as «غير متاح» grey.
    [Fact]
    public void Falling_actual_rent_is_green_in_every_place()
    {
        var facts = Facts() with { ActualRentCpi = F("الإيجار الفعلي للمسكن CPI", "-1.8%", "—", "تراجع", "أغسطس 2026", ReportFrequency.Monthly) };
        var m = CityReportModelBuilder.Build(City(), facts, Content(), new DateOnly(2026, 9, 6));

        var row = m.IndicatorRows.Single(r => r.Indicator.StartsWith("الإيجار الفعلي"));
        Assert.Equal(("#2E9B5B", "#E8F4EC"), (row.StatusColorHex, row.StatusBgHex));
        Assert.Equal("#2E9B5B", m.KpiCards.Single(c => c.Label.StartsWith("الإيجار الفعلي")).ColorHex);
        Assert.Equal("#2E9B5B", m.SupplyInvestment.Single(s => s.Label.StartsWith("الإيجار الفعلي")).ColorHex);
        Assert.Equal("#2E9B5B", CityReportModelBuilder.ValueColorOf(facts.ActualRentCpi, isActualRent: true));
    }

    [Fact]
    public void Builder_builds_indicator_rows_with_months_and_status_colours()
    {
        var rows = Model().IndicatorRows;
        Assert.Equal(6, rows.Count);
        Assert.Equal(("نمو مستدام", "#2E9B5B", "#E8F4EC"), (rows[0].Status, rows[0].StatusColorHex, rows[0].StatusBgHex));
        Assert.Equal(("الزيادة الإيجارية", "+3%", "> 6%", "ضمن الحد المرجعي للمراقبة", "#2E9B5B"), (rows[1].Indicator, rows[1].Value, rows[1].Reference, rows[1].Status, rows[1].StatusColorHex));
        Assert.Equal(("التضخم العام (CPI)", "+1.5%", "مستقر"), (rows[5].Indicator, rows[5].Value, rows[5].Status));
    }

    [Fact]
    public void Word_and_pdf_show_cpi_period_beside_the_name_and_monitoring_reference_header()
    {
        var m = Model();
        var word = WordBodyText(m);
        Assert.Contains("الحد المرجعي للمراقبة", word);
        // The executive-summary card carries the client's wording; no other place does.
        Assert.Contains("الإيجار الفعلي للمسكن CPI (التغير السنوي)", word);
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(word, "التغير السنوي").Count);

        using var ms = new MemoryStream(CityReportWordRenderer.Render(m));
        using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(ms, false);
        var cpiPara = doc.MainDocumentPart!.Document.Body!.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>()
            .First(p => p.InnerText.StartsWith("التضخم العام (CPI)", StringComparison.Ordinal));
        Assert.Empty(cpiPara.Descendants<DocumentFormat.OpenXml.Wordprocessing.Break>());
        Assert.Equal(new[] { "التضخم العام (CPI)", " ", "\u00a0أغسطس\u00a0" },
            cpiPara.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>().Select(t => t.Text));

        Assert.NotEmpty(CityReportPdfRenderer.Render(m));
    }

    [Fact]
    public void Builder_builds_supply_strip_from_facts()
    {
        var s = Model().SupplyInvestment;
        Assert.Equal(new[] { "7%", "+5.3%", "5%" }, s.Select(x => x.Value));
        Assert.Equal(new[] { "ضمن الحد المرجعي للمراقبة", "ارتفاع", "دون الحد المرجعي للمراقبة" }, s.Select(x => x.Note));
        Assert.Equal("الشواغر الإيجارية", s[0].Label);
    }

    [Fact]
    public void Builder_puts_platform_recommendation_first()
    {
        var m = Model();
        Assert.Equal("يتطلب دراسة استباقية: مراقبة - العائد الإيجاري 5%.", m.RecommendationBullets[0]);
        Assert.Equal(Content().RecommendationBullets, m.RecommendationBullets.Skip(1));
    }

    [Fact]
    public void Builder_uses_status_alone_when_recommendation_empty()
    {
        var m = CityReportModelBuilder.Build(City(), Facts() with { Recommendation = "" }, Content(), new DateOnly(2026, 9, 6));
        Assert.Equal("يتطلب دراسة استباقية.", m.RecommendationBullets[0]);
    }

    [Fact]
    public void Builder_prefixes_media_breakdown_with_month_only_when_known()
    {
        var withMonth = City();
        withMonth.MediaMonitoring.MonthLabel = "أغسطس 2026";
        Assert.StartsWith("أغسطس 2026: إيجابي 4%", CityReportModelBuilder.Build(withMonth, Facts(), Content(), new DateOnly(2026, 9, 6)).MediaBreakdown);
        Assert.StartsWith("إيجابي 4%", Model().MediaBreakdown);
    }

    [Fact]
    public void Monthly_report_shows_no_media_when_the_city_has_none_for_the_report_month()
    {
        // The city's latest media is August; a September report must not present it as September's.
        var city = City();
        city.MediaMonitoring.MonthLabel = "أغسطس 2026";

        var m = CityReportModelBuilder.Build(city, Facts(), Content(), new DateOnly(2026, 10, 6), "سبتمبر 2026", isMonthly: true);

        Assert.False(m.MediaAvailable);
        Assert.Equal("لا تتوفر بيانات رصد إعلامي لمدينة الرياض لهذا الشهر.", Assert.Single(m.MediaBullets));
        Assert.Equal("", m.MediaBreakdown);
    }

    [Fact]
    public void Monthly_report_shows_media_of_the_report_month()
    {
        var city = City();
        city.MediaMonitoring.MonthLabel = "سبتمبر 2026";

        var m = CityReportModelBuilder.Build(city, Facts(), Content(), new DateOnly(2026, 10, 6), "سبتمبر 2026", isMonthly: true);

        Assert.True(m.MediaAvailable);
        Assert.Contains("69%", m.MediaBreakdown);
        Assert.Equal(new[] { "ملخص الرصد الإعلامي." }, m.MediaBullets);
    }

    [Fact]
    public void Builder_shows_dash_for_missing_values()
    {
        var facts = Facts() with { InvestmentReturn = Facts().InvestmentReturn with { Value = "—" } };
        var m = CityReportModelBuilder.Build(City(), facts, Content(), new DateOnly(2026, 9, 6));
        Assert.Equal("—", m.KpiCards[2].ValueText);
        Assert.Equal("—", m.SupplyInvestment[2].Value);
    }

    [Fact]
    public void Builder_colours_accelerated_city_red()
    {
        var facts = Facts() with
        {
            OperationalStatus = "الضوابط مفعلة", Classification = "نمو متسارع", ClassificationLabel = "accelerated",
            Cagr = Facts().Cagr with { Status = "نمو متسارع" },
        };
        var m = CityReportModelBuilder.Build(City(), facts, Content(), new DateOnly(2026, 9, 6));
        Assert.Equal("#C0392B", m.MarketStateColorHex);
        Assert.Equal("#DC2626", m.IndicatorRows[0].StatusColorHex);
    }

    [Fact]
    public void Builder_does_not_repeat_status_already_in_recommendation()
    {
        var facts = Facts() with { OperationalStatus = "لا إجراء", Recommendation = "لا إجراء - ضمن الحدود ولكن يوجد عبء ذوي الدخل المحدود" };
        var m = CityReportModelBuilder.Build(City(), facts, Content(), new DateOnly(2026, 9, 6));
        Assert.Equal("لا إجراء - ضمن الحدود المرجعية للمراقبة ولكن يوجد عبء سكني لذوي الدخل المحدود.", m.RecommendationBullets[0]);
    }

    [Fact]
    public void Builder_keeps_an_existing_final_period_on_the_recommendation()
    {
        var m = CityReportModelBuilder.Build(City(), Facts() with { Recommendation = "مراقبة العائد." }, Content(), new DateOnly(2026, 9, 6));
        Assert.Equal("يتطلب دراسة استباقية: مراقبة العائد الإيجاري.", m.RecommendationBullets[0]);
    }

    [Fact]
    public void Builder_watch_title_follows_the_report_period()
    {
        Assert.Equal("ما يجب مراقبته كمتابعة دورية", Model().WatchTitle);
        Assert.Equal("ما يجب مراقبته كمتابعة دورية",
            CityReportModelBuilder.Build(City(), Facts(), Content(), new DateOnly(2026, 9, 6), "أغسطس 2026", isMonthly: true).WatchTitle);
    }

    [Fact]
    public void Word_render_has_no_trend_column_and_no_direction_arrows()
    {
        var text = WordBodyText(Model());
        Assert.DoesNotContain("الاتجاه العام", text);
        Assert.DoesNotContain("▲", text);
        Assert.DoesNotContain("▼", text);
    }

    [Fact]
    public void Word_render_shows_kpi_reference_line()
    {
        Assert.Contains("الحد المرجعي للمراقبة: أقل من 6%", WordBodyText(Model()));
    }

    [Fact]
    public void Word_render_places_city_averages_line_right_after_market_bullets()
    {
        var text = WordBodyText(Model());
        var bullet = text.IndexOf(Content().MarketBullets[^1], StringComparison.Ordinal);
        var averages = text.IndexOf(Content().CityAveragesLine, StringComparison.Ordinal);
        var productHeader = text.IndexOf("الصفقات البيعيةالرقم القياسي", StringComparison.Ordinal);
        Assert.True(bullet >= 0 && bullet < averages && averages < productHeader, $"bullet={bullet} averages={averages} product={productHeader}");
    }

    [Fact]
    public void Word_render_produces_valid_docx_with_sections()
    {
        var bytes = CityReportWordRenderer.Render(Model());
        Assert.NotEmpty(bytes);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var text = doc.MainDocumentPart!.Document.Body!.InnerText;
        Assert.Contains("الملخص التنفيذي", text);
        Assert.Contains("لوحة المؤشرات للضوابط الإيجارية", text);
        Assert.Contains("حركة السوق والأسعار", text);
        Assert.Contains("الرصد الإعلامي", text);
        Assert.Contains("أبرز الاستنتاجات والتوصية", text);
        Assert.Contains("ما يجب مراقبته كمتابعة دورية", text);
        // city name now lives in the header part (title/subtitle), not the body
        var headerText = doc.MainDocumentPart!.HeaderParts.First().Header!.InnerText;
        Assert.Contains("الرياض", headerText);
    }

    [Fact]
    public void Pdf_render_produces_pdf()
    {
        var bytes = CityReportPdfRenderer.Render(Model());
        Assert.NotEmpty(bytes);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }
}
