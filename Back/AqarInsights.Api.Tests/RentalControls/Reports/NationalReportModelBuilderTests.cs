using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Reports;
using Xunit;

namespace AqarInsights.Api.Tests.RentalControls.Reports;

public class NationalReportModelBuilderTests
{
    private static MarketAnalysisData Sample() => new()
    {
        LastQuarter = "الربع الثاني 2026",
        Cities = new()
        {
            new() { Name = "الرياض", Classification = "نمو متسارع", Cagr = 9.7, HousingBurden = 35, InvestmentReturn = 5.1, ActualRentalGrowth = 5.9 },
            new() { Name = "نجران", Classification = "نمو مرتفع", Cagr = 6.8, HousingBurden = 46, InvestmentReturn = 7.3, ActualRentalGrowth = 0.6 },
            new() { Name = "مكة المكرمة", Classification = "نمو مستدام", Cagr = 4.0, HousingBurden = 30, InvestmentReturn = 4.0, ActualRentalGrowth = 6.2 },
            new() { Name = "عرعر", Classification = "نمو مستدام", Cagr = 5.0, HousingBurden = 37, InvestmentReturn = 10, ActualRentalGrowth = 16.4 },
            new() { Name = "أبها", Classification = "نمو مستدام", Cagr = 3.0, HousingBurden = 34, InvestmentReturn = null, ActualRentalGrowth = null },
        },
    };

    private static readonly NationalReportOptions Options = new();

    // The overview rounds yields to whole percents: Madinah 4.34, Makkah 3.95 and Tabuk 3.88 all read "4%", so the
    // lowest is decided on the city's own unrounded yield (its ROI series), not on list order.
    [Fact]
    public void Lowest_yield_breaks_a_rounded_tie_on_the_exact_yield()
    {
        var data = new MarketAnalysisData
        {
            LastQuarter = "الربع الثاني 2026",
            Cities = new()
            {
                new() { Name = "المدينة المنورة", Classification = "نمو مستدام", InvestmentReturn = 4, InvestmentReturnExact = 4.34 },
                new() { Name = "مكة المكرمة", Classification = "نمو مستدام", InvestmentReturn = 4, InvestmentReturnExact = 3.95 },
                new() { Name = "تبوك", Classification = "نمو مستدام", InvestmentReturn = 4, InvestmentReturnExact = 3.88 },
                new() { Name = "الرياض", Classification = "نمو متسارع", InvestmentReturn = 5, InvestmentReturnExact = 5.1 },
            },
        };

        var card = NationalReportModelBuilder.Build(data, new DateOnly(2026, 8, 24), Options).KpiCards.Single(c => c.Label == "أدنى عائد إيجاري");

        Assert.Equal(("تبوك", "4%"), (card.CityName, card.ValueText));
    }

    [Fact]
    public void Intervention_groups_map_status_and_split_data_poor_cities()
    {
        var data = new MarketAnalysisData
        {
            LastQuarter = "الربع الثاني 2026",
            Cities = new()
            {
                new() { Name = "الرياض", Classification = "نمو متسارع", OperationalStatus = "الضوابط مفعلة", HousingBurden = 35, InvestmentReturn = 5, ActualRentalGrowth = 6 },
                new() { Name = "نجران", Classification = "نمو مرتفع", OperationalStatus = "يتطلب دراسة تفصيلية", HousingBurden = 46, InvestmentReturn = 7, ActualRentalGrowth = 1 },
                new() { Name = "جدة", Classification = "نمو مستدام", OperationalStatus = "يتطلب دراسة استباقية", Cagr = 4.6, HousingBurden = 35, InvestmentReturn = 6, ActualRentalGrowth = 1 },
                new() { Name = "أبها", Classification = "نمو مستدام", OperationalStatus = "يتطلب دراسة استباقية", Cagr = 3.0, HousingBurden = 34, InvestmentReturn = null, ActualRentalGrowth = null },
            },
        };

        var m = NationalReportModelBuilder.Build(data, new DateOnly(2026, 8, 24), Options);
        var g = m.InterventionGroups.ToDictionary(x => x.Label, x => x.Cities);

        Assert.Equal(new[] { "الرياض" }, g["التدخل مفعّل"]);
        Assert.Equal(new[] { "نجران" }, g["يتطلب دراسة تفصيلية"]);
        Assert.Equal(new[] { "جدة", "أبها" }, g["مراقبة استباقية"]);
        Assert.Equal(4, m.MonitoringBullets.Count);
        Assert.Equal("التقرير الوطني لحالة المدن للضوابط الإيجارية", m.HeaderTitle);
        Assert.Equal("تقييم حالة المدن استنادًا إلى مؤشرات الضوابط الإيجارية – الربع الثاني 2026", m.AssessmentLine);
    }

    [Fact]
    public void Kpis_pick_the_correct_extremes_and_format()
    {
        var m = NationalReportModelBuilder.Build(Sample(), new DateOnly(2026, 8, 23), Options);

        Assert.Equal("مؤشرات الضوابط الإيجارية", m.Title);
        Assert.Equal("الربع الثاني 2026", m.PeriodLabel);

        var rent = m.KpiCards[0];
        Assert.Equal("أعلى إيجار فعلي للمسكن CPI", rent.Label);
        Assert.Equal("عرعر", rent.CityName);
        Assert.Equal("+16.4%", rent.ValueText);

        Assert.Equal("مكة المكرمة", m.KpiCards[1].CityName);
        Assert.Equal("4%", m.KpiCards[1].ValueText);
        Assert.Equal("نجران", m.KpiCards[2].CityName);
        Assert.Equal("46%", m.KpiCards[2].ValueText);
        Assert.Equal("الرياض", m.KpiCards[3].CityName);
        Assert.Equal("9.7%", m.KpiCards[3].ValueText);
        Assert.Equal(new[] { "شهري", "ربعي", "ربعي", "" }, m.KpiCards.Select(k => k.Frequency));
    }

    [Fact]
    public void Summary_lines_count_tracks_and_threshold_crossings()
    {
        var m = NationalReportModelBuilder.Build(Sample(), new DateOnly(2026, 8, 23), Options);

        Assert.Equal(
            "من أصل 5 مدينة: مدينة واحدة في المسار المتسارع، ومدينة واحدة في المسار المرتفع، و3 مدن في المسار الاستباقي.",
            m.SummaryLines[0]);
        // burden > 30: الرياض35, نجران46, عرعر37, أبها34 = 4 ; yield < 6: الرياض5.1, مكة4.0 = 2
        Assert.Equal(
            "الضغط الأبرز وطنيًا يتركز في القدرة على تحمل السكن، إذ يتجاوز العبء السكني لذوي الدخل المحدود الحد المرجعي للمراقبة (30%) في 4 مدن، بينما يقل العائد الإيجاري عن الحد المرجعي للمراقبة (6%) في مدينتين.",
            m.SummaryLines[1]);
        Assert.Equal("المدن مرتبة تنازليًا حسب معدل النمو الإيجاري المركب.", m.SortNote);
    }

    // Client wording standards (city-report review, applied report-wide): canonical burden label,
    // full "الشواغر الإيجارية"/"العائد الإيجاري" terms.
    [Fact]
    public void Uses_canonical_terms_everywhere()
    {
        var m = NationalReportModelBuilder.Build(Sample(), new DateOnly(2026, 8, 23), Options);
        var all = string.Join("\n", m.SummaryLines
            .Concat(m.MonitoringBullets)
            .Concat(m.KpiCards.Select(k => k.Label))
            .Append(m.SortNote).Append(m.Title).Append(m.MonitoringTitle));

        Assert.DoesNotContain("سكني للدخل", all);
        Assert.Contains("العبء السكني لذوي الدخل المحدود", all);
        Assert.DoesNotContain("العائد الاستثماري", all);
        Assert.DoesNotMatch(@"الشواغر(?!\s*الإيجارية)", all);
        Assert.DoesNotMatch(@"العائد(?!\s*الإيجاري)", all);
    }

    [Fact]
    public void Rows_sorted_by_compound_growth_with_null_dashes()
    {
        var m = NationalReportModelBuilder.Build(Sample(), new DateOnly(2026, 8, 23), Options);

        // compound growth: الرياض 9.7, نجران 6.8, عرعر 5.0, مكة 4.0, أبها 3.0
        Assert.Equal(new[] { "الرياض", "نجران", "عرعر", "مكة المكرمة", "أبها" },
            m.Rows.Select(r => r.CityName).ToArray());
        Assert.Equal(1, m.Rows[0].Rank);
        Assert.Equal("تفاعلي متسارع", m.Rows[0].TrackLabel);
        Assert.Equal("استباقي", m.Rows[2].TrackLabel);

        var abha = m.Rows.Single(r => r.CityName == "أبها");
        Assert.Equal("—", abha.YieldText);
        Assert.Equal("—", abha.ActualRentText);
        Assert.Equal("34%", abha.BurdenText);

        var riyadh = m.Rows.Single(r => r.CityName == "الرياض");
        Assert.Equal("الوسطى", riyadh.Region);
    }

    private static CityReportFacts Facts(string burden, string burdenStatus, string rent, string rentStatus) => new()
    {
        HousingBurden = new() { Label = "العبء السكني لذوي الدخل المحدود", Value = burden, Status = burdenStatus },
        ActualRentCpi = new() { Label = "الإيجار الفعلي للمسكن CPI", Value = rent, Status = rentStatus },
        InvestmentReturn = new() { Label = "العائد الإيجاري", Value = "5.1%", Status = "دون الحد" },
        RentalIncrease = new() { Label = "الزيادة الإيجارية", Value = "+3%", Status = "ضمن الحد" },
        RentToIncome = new() { Label = "نسبة الإيجار إلى الدخل", Value = "15%", Status = "ضمن الحد" },
    };

    // Client review (national, August 2026, note 1): a cell takes the colour the city's own report gives the figure
    // (its platform status), not a national scale: burden 35% is red «متجاوز», a rising actual rent red «ارتفاع».
    [Fact]
    public void Cells_take_the_city_report_colour_of_the_platform_status()
    {
        var context = new NationalReportContext(new Dictionary<string, string>(), CityFacts: new Dictionary<string, CityReportFacts>
        {
            ["الرياض"] = Facts("35%", "متجاوز", "+5.9%", "ارتفاع"),
            ["نجران"] = Facts("46%", "متجاوز", "+0.6%", "تراجع"),
            ["مكة المكرمة"] = Facts("30%", "ضمن الحد", "+6.2%", "ارتفاع"),
        });

        var m = NationalReportModelBuilder.Build(Sample(), new DateOnly(2026, 8, 23), Options, context: context);
        var row = m.Rows.ToDictionary(r => r.CityName);

        Assert.Equal(("#DC2626", "#DC2626"), (row["الرياض"].BurdenColorHex, row["الرياض"].RentColorHex));
        Assert.Equal("#2E9B5B", row["نجران"].RentColorHex);
        Assert.Equal("#2E9B5B", row["مكة المكرمة"].BurdenColorHex);
        Assert.Equal("#C67A1E", row["الرياض"].YieldColorHex);
        // No facts for the city, or no figure: grey («غير متاح»).
        Assert.Equal("#6B7280", row["عرعر"].BurdenColorHex);
        Assert.Equal("#6B7280", row["أبها"].RentColorHex);
    }

    // Client review notes 2 and 4: an arrow (or «—» when unchanged) beside every value that has a previous one, and the
    // summary counts the table's ↓ arrows and quotes the named city's value as the table prints it.
    [Fact]
    public void Arrows_mark_unchanged_values_and_the_summary_counts_the_table()
    {
        var previous = new MarketAnalysisData
        {
            Cities = new()
            {
                new() { Name = "الرياض", HousingBurden = 35, ActualRentalGrowth = 6.4 },
                new() { Name = "نجران", HousingBurden = 44, ActualRentalGrowth = 0.6 },
                new() { Name = "مكة المكرمة", HousingBurden = 31, ActualRentalGrowth = 6.5 },
                new() { Name = "عرعر", HousingBurden = 37, ActualRentalGrowth = 16.0 },
            },
        };
        var context = new NationalReportContext(new Dictionary<string, string>(), PreviousQuarter: previous, PreviousMonth: previous);

        var m = NationalReportModelBuilder.Build(Sample(), new DateOnly(2026, 8, 23), Options, context: context);
        var row = m.Rows.ToDictionary(r => r.CityName);

        Assert.Equal(("—", "↓"), (row["الرياض"].BurdenTrend, row["الرياض"].ActualRentTrend));
        Assert.Equal(("↑", "—"), (row["نجران"].BurdenTrend, row["نجران"].ActualRentTrend));
        Assert.Equal(("↓", "↓"), (row["مكة المكرمة"].BurdenTrend, row["مكة المكرمة"].ActualRentTrend));
        Assert.Equal(("—", "↑"), (row["عرعر"].BurdenTrend, row["عرعر"].ActualRentTrend));
        // أبها has no previous values: no symbol.
        Assert.Equal(("", ""), (row["أبها"].BurdenTrend, row["أبها"].ActualRentTrend));
        Assert.Equal("تباطؤ الإيجار الفعلي للمسكن CPI في مدينتين، وانخفض في الرياض إلى +5.9%.", m.SummaryLines[2]);
    }

    [Fact]
    public void Summary_names_the_steepest_fall_when_riyadh_did_not_fall()
    {
        var previous = new MarketAnalysisData { Cities = new() { new() { Name = "الرياض", ActualRentalGrowth = 5.9 }, new() { Name = "مكة المكرمة", ActualRentalGrowth = 7.0 }, new() { Name = "نجران", ActualRentalGrowth = 1.0 } } };
        var m = NationalReportModelBuilder.Build(Sample(), new DateOnly(2026, 8, 23), Options,
            context: new NationalReportContext(new Dictionary<string, string>(), PreviousMonth: previous));

        Assert.Equal("تباطؤ الإيجار الفعلي للمسكن CPI في مدينتين، وانخفض في مكة المكرمة إلى +6.2%.", m.SummaryLines[2]);
    }

    [Fact]
    public void Monitoring_title_is_the_city_reports_title()
        => Assert.Equal("ما يجب مراقبته كمتابعة دورية", NationalReportModelBuilder.Build(Sample(), new DateOnly(2026, 8, 23), Options).MonitoringTitle);

    // The impact section leads with the compound growth: 15.7% before the controls to the period's own figure.
    [Fact]
    public void Impact_section_states_the_compound_growth_drop_since_activation()
    {
        var rentIncrease = new List<RentalIndicatorPoint>
        {
            new() { Period = "2025-Q2", Value = 17 },
            new() { Period = "2026-Q3", Value = -1 },
        };
        var city = new ImpactCitySeries("الرياض", rentIncrease, [], Cagr: 8.9, CagrPeriod: "الربع الثالث 2026");

        var m = NationalReportModelBuilder.Build(Sample(), new DateOnly(2026, 11, 19), Options,
            context: new NationalReportContext(new Dictionary<string, string>(), ImpactCities: [city]));

        Assert.Equal(
            "انخفض معدل النمو الإيجاري المركب بمقدار 6.8 نقطة مئوية: من 15.7% قبل التفعيل (الربع الثاني 2025) إلى 8.9% (الربع الثالث 2026).",
            m.Impact.Single().Points[0]);
        Assert.Contains("الزيادة الإيجارية", m.Impact.Single().Points[1]);
    }
}
