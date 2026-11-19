using System.Text.Json;
using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Services.RentalControls.Projections;
using AqarInsights.Api.Services.RentalControls.Snapshots;
using AqarInsights.Api.Tests.RentalControls.Reports;

namespace AqarInsights.Api.Tests.RentalControls;

// A month shows its quarter's values with its own monthly values; a quarter shows each monthly value's latest month
// inside the quarter. Live data (Q3 quarter, text month October): rental and public vacancy, CPI and media all run to
// October, past the quarter's last month.
public class PeriodValueMaskTests
{
    private static readonly YearQuarter Q2 = new(2026, 2);
    private static readonly YearQuarter Q3 = new(2026, 3);
    private static readonly YearMonth June = new(2026, 6);
    private static readonly YearMonth July = new(2026, 7);
    private static readonly YearMonth August = new(2026, 8);
    private static readonly YearMonth September = new(2026, 9);
    private static readonly YearMonth October = new(2026, 10);
    private static readonly PeriodScope Q3View = new(Q3, null, October);
    private static PeriodScope Month(YearMonth m, YearMonth textMonth) => new(m.Quarter, m, textMonth);

    private static string Json(object? v) => JsonSerializer.Serialize(v);

    private static IndicatorPanel Panel(IndicatorsData data, string id) => data.CurrentPeriodIndicators.Single(p => p.Id == id);
    private static KpiItem Vacancy(CityAnalysisData data) => data.Kpis.Single(k => k.Label == "الشواغر الإيجارية");
    private static (string Status, string Priority) VacancyWatch(CityAnalysisData data)
        => data.WhatToMonitor.Single(w => w.Item == "معدل الشواغر") is var w ? (w.CurrentStatus, w.Priority) : default;

    [Fact]
    public void A_month_blanks_the_monthly_panels_of_other_months_only()
    {
        var source = RealReportData.Aggregation.GetIndicators();
        var before = Json(source);

        var july = PeriodValueMask.Apply(source, Month(July, October));
        var october = PeriodValueMask.Apply(source, Month(October, October));

        Assert.Equal(before, Json(source)); // the cached view is untouched
        Assert.All(new[] { Panel(july, "vacancy"), Panel(july, "public-vacancy") }, p =>
        {
            Assert.Equal(("يوليو 2026", "2026-07"), (p.PeriodLabel, p.PeriodId));
            // The mask blanks another month's panel values; July's own values come from the capped dataset.
            Assert.All(p.Cities, c => Assert.Equal(("—", "—"), (c.DisplayValue, c.Status)));
        });
        Assert.Equal(Json(Panel(source, "vacancy")), Json(Panel(october, "vacancy"))); // October's own value
        Assert.Equal(
            Json(source.CurrentPeriodIndicators.Where(p => p.Frequency != "monthly")),
            Json(july.CurrentPeriodIndicators.Where(p => p.Frequency != "monthly")));
        Assert.Equal(Json(source.HistoricalIndicators), Json(july.HistoricalIndicators));
        Assert.Equal(Json(source.SupplyDemandGap), Json(july.SupplyDemandGap));
    }

    [Fact]
    public void A_quarter_blanks_monthly_values_from_after_and_before_the_quarter()
    {
        // The production Q3 view: live capped at September, then masked for the quarter (as the endpoints apply it).
        var data = RealReportData.Periods.ResolveAsync("2026-Q3").GetAwaiter().GetResult()!;
        var q3 = PeriodValueMask.Apply(data.Aggregation.GetIndicators(), data.Scope);

        var vacancy = Panel(q3, "vacancy");
        Assert.Equal("2026-09", vacancy.PeriodId); // the latest month inside the quarter, not October
        Assert.Equal("8%", vacancy.Cities.Single(c => c.CityId == "riyadh").DisplayValue);
        var city = PeriodValueMask.Apply(data.Aggregation.GetCity("riyadh")!, data.Scope);
        Assert.Equal("2026-09", Vacancy(city).PeriodId);
        Assert.True(city.MediaMonitoring.Available);
        Assert.Equal("2026-09", city.MediaMonitoring.Period); // September's own media, not October's
        Assert.DoesNotContain(city.ExecutiveSummary.Bullets, b => b.Contains("أكتوبر 2026"));
    }

    [Fact]
    public void A_month_outside_the_text_month_blanks_monthly_kpis_rows_media_and_sentences()
    {
        var source = RealReportData.Aggregation.GetCity("riyadh")!;
        var before = Json(source);

        var july = PeriodValueMask.Apply(source, Month(July, September));

        Assert.Equal(before, Json(source));
        Assert.Equal(("—", "—", "stable", "يوليو 2026"), (Vacancy(july).Value, Vacancy(july).Status, Vacancy(july).Trend, Vacancy(july).PeriodLabel));
        Assert.Equal(("—", "—"), VacancyWatch(july));
        Assert.Equal(
            Json(source.Kpis.Where(k => k.Frequency != "monthly")),
            Json(july.Kpis.Where(k => k.Frequency != "monthly")));
        var cpi = july.MarketMovementTable.Where(r => r.Indicator.Contains("(CPI)")).ToList();
        Assert.Equal(2, cpi.Count);
        Assert.All(cpi, r => Assert.Equal(("مدينة", "—", "—"), (r.Level, r.Change, r.Assessment)));
        Assert.False(july.MediaMonitoring.Available);
        Assert.DoesNotContain(july.ExecutiveSummary.Bullets, MonthlyStatements.IsMonthly);
        Assert.NotEmpty(july.ExecutiveSummary.Bullets);
        Assert.Equal(Json(source.RentalIndicatorCharts), Json(july.RentalIndicatorCharts));
        Assert.Equal(Json(source.MarketAnalysisTabs), Json(july.MarketAnalysisTabs));
    }

    [Fact]
    public void The_text_month_keeps_its_monthly_sentences_and_values()
    {
        var source = RealReportData.Aggregation.GetCity("riyadh")!;
        Assert.Equal("2026-10", Vacancy(source).PeriodId);

        var october = PeriodValueMask.Apply(source, Month(October, October));
        var september = PeriodValueMask.Apply(source, Month(September, October));

        Assert.Equal(("8%", "أكتوبر 2026"), (Vacancy(october).Value, Vacancy(october).PeriodLabel));
        Assert.Equal(Json(source.MarketMovementTable), Json(october.MarketMovementTable));
        Assert.Equal(Json(source.MediaMonitoring), Json(october.MediaMonitoring));
        Assert.Equal(source.ExecutiveSummary.Bullets, october.ExecutiveSummary.Bullets);
        // September (not the text month) on uncapped October data: October's CPI rows and media are blanked.
        Assert.All(september.MarketMovementTable.Where(r => r.Indicator.Contains("(CPI)")), r => Assert.Equal(("مدينة", "—"), (r.Level, r.Change)));
        Assert.False(september.MediaMonitoring.Available);
    }

    [Fact]
    public void Media_and_cpi_rows_without_a_month_count_as_the_selected_month()
    {
        // The June release's workbooks stored neither the media month nor the CPI month.
        var source = Copy(RealReportData.Aggregation.GetCity("riyadh")!);
        source.MediaMonitoring.Period = "";
        foreach (var row in source.MarketMovementTable) row.Level = row.Level.Split(" - ")[0].Trim();

        var june = PeriodValueMask.Apply(source, Month(June, June));

        Assert.True(june.MediaMonitoring.Available);
        Assert.Equal(("2026-06", "يونيو 2026"), (june.MediaMonitoring.Period, june.MediaMonitoring.MonthLabel));
        Assert.Equal(Json(source.MarketMovementTable), Json(june.MarketMovementTable));
    }

    [Fact]
    public void A_month_shows_its_own_cpi_and_blanks_movement_rows_dated_to_another_month()
    {
        // August = live cut at August: the CPI rows are August's (from the August dataset); the monthly rent change
        // ("مدينة - 2026-10") is October's and only shown in October.
        const string monthlyChange = "التغير الإيجاري (شهري)";
        var source = RealReportData.Periods.ResolveAsync("2026-08").GetAwaiter().GetResult()!.Aggregation.GetCity("riyadh")!;
        Assert.Contains("2026-08", source.MarketMovementTable.Single(r => r.Indicator == "الإيجار الفعلي للمسكن (CPI)").Level);
        Assert.Contains("2026-10", source.MarketMovementTable.Single(r => r.Indicator == monthlyChange).Level);

        var august = PeriodValueMask.Apply(source, Month(August, October));
        var july = PeriodValueMask.Apply(source, Month(July, October));

        Assert.Equal(("مدينة", "—", "—"), Row(august, monthlyChange));
        Assert.All(august.MarketMovementTable.Where(r => r.Indicator.Contains("(CPI)")), r => Assert.NotEqual("—", r.Change));
        Assert.Equal(Json(source.MarketMovementTable.Where(r => r.Level.Contains("Q"))), Json(august.MarketMovementTable.Where(r => r.Level.Contains("Q"))));
        Assert.Equal("—", Row(july, monthlyChange).Change);
    }

    [Fact]
    public void Sentences_about_a_later_month_are_always_dropped()
    {
        // Live's text describes October ("… في أكتوبر 2026 …"); August drops those and, not being the text month,
        // every other monthly sentence too.
        var source = RealReportData.Aggregation.GetCity("riyadh")!;
        Assert.Contains(source.ExecutiveSummary.Bullets, b => b.Contains("أكتوبر 2026"));

        var august = PeriodValueMask.Apply(source, Month(August, October));

        Assert.Equal(
            source.ExecutiveSummary.Bullets.Where(b => !MonthlyStatements.NamesMonthAfter(b, August) && !MonthlyStatements.IsMonthly(b)),
            august.ExecutiveSummary.Bullets);
        // The Q3 view's text belongs to October (after the quarter), so its monthly sentences are dropped too; a
        // quarter whose text month is inside the quarter keeps them.
        Assert.False(PeriodValueMask.KeepsSentence("الإيجار الفعلي (CPI) يرتفع 3.5% (أكتوبر 2026).", Q3View));
        Assert.True(PeriodValueMask.KeepsSentence("الإيجار الفعلي (CPI) يرتفع 2.0% (يونيو 2026).", new PeriodScope(Q2, null, June)));
        Assert.False(PeriodValueMask.KeepsSentence("الإيجار الفعلي (CPI) يرتفع 4.0% (سبتمبر 2026).", new PeriodScope(Q2, null, June)));
        Assert.True(PeriodValueMask.KeepsSentence("نسبة الإيجار للدخل 15%.", Month(July, October)));
    }

    [Fact]
    public void A_cpi_month_written_with_an_en_dash_is_still_removed_from_the_level()
    {
        var source = Copy(RealReportData.Aggregation.GetCity("riyadh")!);
        foreach (var row in source.MarketMovementTable.Where(r => r.Indicator.Contains("(CPI)"))) row.Level = "مدينة –\u200E2026-09";

        var august = PeriodValueMask.Apply(source, Month(August, September));

        Assert.All(august.MarketMovementTable.Where(r => r.Indicator.Contains("(CPI)")), r => Assert.Equal(("مدينة", "—"), (r.Level, r.Change)));
    }

    [Fact]
    public void No_period_rules_leave_the_documents_as_they_are()
    {
        var indicators = RealReportData.Aggregation.GetIndicators();
        var city = RealReportData.Aggregation.GetCity("riyadh")!;

        Assert.Same(indicators, PeriodValueMask.Apply(indicators, PeriodScope.None));
        Assert.Same(city, PeriodValueMask.Apply(city, PeriodScope.None));
    }

    [Theory]
    [InlineData("التغير الإيجاري الشهري تراجع -2.7% في سبتمبر 2026 بعد -1.1% في أغسطس.", 2026, 8, true)]
    [InlineData("التغير الإيجاري الشهري تراجع -2.7% في سبتمبر 2026 بعد -1.1% في أغسطس.", 2026, 9, false)]
    [InlineData("الإيجار الفعلي (CPI) يرتفع 4.0% سنويا (يناير 2027).", 2026, 12, true)]
    [InlineData("نسبة الإيجار للدخل 15% (الربع الثالث 2026).", 2026, 6, false)]
    public void Detects_a_named_month_after_the_selected_one(string text, int year, int month, bool later)
        => Assert.Equal(later, MonthlyStatements.NamesMonthAfter(text, new YearMonth(year, month)));

    [Theory]
    [InlineData("التضخم العام +2.2%", true)]
    [InlineData("الانطباع العام للرصد الإعلامي محايد", true)]
    [InlineData("بيانات يونيو 2026", true)]
    [InlineData("الزيادة الإيجارية 9% ضمن الحد", false)]
    public void Monthly_statements_are_detected(string text, bool monthly)
        => Assert.Equal(monthly, MonthlyStatements.IsMonthly(text));

    private static (string Level, string Change, string Assessment) Row(CityAnalysisData data, string indicator)
        => data.MarketMovementTable.Single(r => r.Indicator == indicator) is var r ? (r.Level, r.Change, r.Assessment) : default;

    private static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
}
