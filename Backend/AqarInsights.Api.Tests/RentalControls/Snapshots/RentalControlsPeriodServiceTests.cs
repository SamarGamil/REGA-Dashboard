using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Options;
using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Models.RentalControls.Tables;
using AqarInsights.Api.Services.RentalControls;
using AqarInsights.Api.Services.RentalControls.Projections;
using AqarInsights.Api.Services.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Snapshots;
using AqarInsights.Api.Services.RentalControls.Store;
using AqarInsights.Api.Services.RentalControls.Tables;
using AqarInsights.Api.Tests.RentalControls.Reports;

namespace AqarInsights.Api.Tests.RentalControls.Snapshots;

// Live release (October 2026 monthly data on the Q3 quarter) + the closed quarter 2026-Q2 + monthly_movement
// (June, August, September, October rows).
public class RentalControlsPeriodServiceTests
{
    private static IRentalControlsPeriodService Periods => RealReportData.Periods;

    private static RentalControlsPeriodData Resolve(string? period) => Periods.ResolveAsync(period).GetAwaiter().GetResult()!;

    [Fact]
    public void Lists_every_month_of_the_quarters_with_data_newest_first()
    {
        var periods = Periods.GetPeriods();

        Assert.Equal("2026-Q3", periods.Current);
        Assert.Equal(new[] { "2026-10", "2026-09", "2026-08", "2026-07", "2026-06", "2026-05", "2026-04" }, periods.Monthly.Select(p => p.Id));
        Assert.Equal(new[] { "2026-Q3", "2026-Q2" }, periods.Quarterly.Select(p => p.Id));
        Assert.Equal("الربع الثاني 2026", periods.Quarterly.Last().Label);
        Assert.Equal("أبريل 2026", periods.Monthly.Last().Label);
    }

    [Fact]
    public void No_period_resolves_to_the_live_quarter()
    {
        var current = Resolve(null);

        // Live now holds October data, so the quarter view is live cut at the quarter's last month.
        Assert.Same(Resolve("2026-Q3").Aggregation, current.Aggregation);
        Assert.Equal("2026-Q3", current.Id);
        Assert.Equal("الربع الثالث 2026", current.Label);
        Assert.False(current.IsMonthly);
        Assert.Equal(Resolve("2026-Q3").Scope, current.Scope);
    }

    [Theory]
    [InlineData(null, "2026-Q3", null, "2026-10")]
    [InlineData("2026-Q3", "2026-Q3", null, "2026-10")]
    [InlineData("2026-Q2", "2026-Q2", null, "2026-06")]
    [InlineData("2026-10", "2026-Q4", "2026-10", "2026-10")]
    [InlineData("2026-09", "2026-Q3", "2026-09", "2026-10")]
    [InlineData("2026-07", "2026-Q3", "2026-07", "2026-10")]
    [InlineData("2026-06", "2026-Q2", "2026-06", "2026-06")]
    [InlineData("2026-04", "2026-Q2", "2026-04", "2026-06")]
    public void Each_period_carries_its_quarter_month_and_text_month(string? period, string quarter, string? month, string textMonth)
    {
        var scope = Resolve(period).Scope;

        Assert.Equal((quarter, month, textMonth), (scope.Quarter?.Id, scope.Month?.Id, scope.TextMonth?.Id));
    }

    [Fact]
    public void Live_month_and_quarter_use_the_live_dataset()
    {
        // October (live's own month, after the newest quarter) is the live dataset as is; the quarter view is live
        // cut at September, so it no longer aliases the uncut aggregation.
        Assert.Same(RealReportData.Aggregation, Resolve("2026-10").Aggregation);
        Assert.True(Resolve("2026-10").IsMonthly);
        Assert.Equal("الربع الثالث 2026", Resolve("2026-Q3").Aggregation.GetMarketAnalysis().LastQuarter);
    }

    [Fact]
    public void Q2_resolves_to_the_final_Q2_release_with_the_Q2_overview()
    {
        var q2 = Resolve("2026-Q2");

        Assert.NotSame(RealReportData.Aggregation, q2.Aggregation);
        Assert.Same(Resolve("2026-06").Aggregation, q2.Aggregation); // June = Q2's data cut at June, as the quarter shows it
        Assert.Equal("الربع الثاني 2026", q2.Label);
        Assert.Equal("الربع الثاني 2026", q2.Aggregation.GetMarketAnalysis().LastQuarter);
        var riyadhQ2 = q2.Aggregation.GetOverview().Cities.Single(c => c.Id == "riyadh");
        var riyadhNow = RealReportData.Aggregation.GetOverview().Cities.Single(c => c.Id == "riyadh");
        Assert.NotEqual(riyadhNow.Cagr, riyadhQ2.Cagr);
        Assert.NotNull(q2.Content.Get("riyadh"));
        // Quarterly reports stop at the quarter's last month: the Q2 release carried August inflation (+2.3%),
        // the Q2 report shows June's (+2.2%) — the same figure as the June monthly report.
        Assert.Equal(("+2.22%", "يونيو 2026"), Inflation(q2, "riyadh"));
        Assert.Equal(("+2.22%", "يونيو 2026"), Inflation(Resolve("2026-06"), "riyadh"));
        Assert.Equal(("محايد", "2026-06"), (q2.Aggregation.GetCity("riyadh")!.MediaMonitoring.OverallSentiment, q2.Aggregation.GetCity("riyadh")!.MediaMonitoring.Period));
    }

    [Fact]
    public void Capping_a_quarter_never_mutates_the_underlying_data()
    {
        var store = RealReportData.LiveStore;

        var capped = new CappedRentalControlsStore(store, new YearMonth(2026, 8));

        Assert.DoesNotContain(capped.GetCityRaw("riyadh")!.RentVsInflationSeries, p => p.Period == "2026-09");
        Assert.Contains(store.GetCityRaw("riyadh")!.RentVsInflationSeries, p => p.Period == "2026-09");
        Assert.NotEqual(store.GetCityRaw("riyadh")!.Media!.Count, capped.GetCityRaw("riyadh")!.Media!.Count);
    }

    private static (string Value, string Month) Inflation(RentalControlsPeriodData period, string cityId)
    {
        var agg = period.Aggregation;
        var facts = CityReportFactsBuilder.Build(agg.GetOverview().Cities.Single(c => c.Id == cityId), agg.GetCity(cityId)!, agg.GetIndicators().CurrentPeriodIndicators);
        return (facts.GeneralInflation.Value, facts.GeneralInflation.Month);
    }

    [Fact]
    public void A_month_is_its_quarters_data_cut_at_the_month()
    {
        var august = Resolve("2026-08");
        var april = Resolve("2026-04");
        var riyadh = (RentalControlsPeriodData p) => p.Aggregation.GetOverview().Cities.Single(c => c.Id == "riyadh");
        MarketMovementRowFacts Cpi(RentalControlsPeriodData p) => new(p.Aggregation.GetCity("riyadh")!.MarketMovementTable.Single(r => r.Indicator == "الإيجار الفعلي للمسكن (CPI)"));

        Assert.NotSame(RealReportData.Aggregation, august.Aggregation); // live cut at August
        Assert.Equal("أغسطس 2026", august.Label);
        Assert.True(august.IsMonthly);
        Assert.Same(august.Aggregation, Resolve("2026-08").Aggregation); // cached
        Assert.Equal(riyadh(Resolve("2026-Q3")).Cagr, riyadh(august).Cagr);
        Assert.Equal(riyadh(Resolve("2026-Q2")).Cagr, riyadh(april).Cagr);
        Assert.Equal("الربع الثاني 2026", april.Aggregation.GetMarketAnalysis().LastQuarter);
        // August has a dataset of its own: its CPI row with the workbook's assessment; April's comes from the series.
        Assert.EndsWith("2026-08", Cpi(august).Level);
        Assert.NotEqual("—", Cpi(august).Assessment);
        Assert.EndsWith("2026-04", Cpi(april).Level);
        Assert.Equal("—", Cpi(april).Assessment);
        Assert.DoesNotContain(april.Aggregation.GetCity("riyadh")!.MediaMonitoring.History, h => string.CompareOrdinal(h.Period, "2026-04") > 0);
        Assert.Equal("2026-07", Resolve("2026-07").Aggregation.GetCity("riyadh")!.MediaMonitoring.Period);
    }

    [Theory]
    [InlineData("2026-04", "abha")]
    [InlineData("2026-04", "jazan")]
    [InlineData("2026-05", "abha")]
    public void A_month_without_a_cpi_series_value_shows_a_dash_not_the_sources_row(string period, string cityId)
    {
        // The Q2 release has no actual-rent series for Abha and Jazan: their April/May row must not keep June's figure.
        var view = Resolve(period);
        var row = view.Aggregation.GetCity(cityId)!.MarketMovementTable.Single(r => r.Indicator == "الإيجار الفعلي للمسكن (CPI)");

        Assert.Equal(new MarketMovementRowFacts($"مدينة - \u200E{period}", "—", "—"), new MarketMovementRowFacts(row));
        Assert.Null(view.Aggregation.GetMarketAnalysis().Cities.Single(c => c.Id == cityId).ActualRentalGrowth);
    }

    private sealed record MarketMovementRowFacts(string Level, string Change, string Assessment)
    {
        public MarketMovementRowFacts(Models.RentalControls.MarketMovementRow r) : this(r.Level, r.Change, r.Assessment) { }
    }

    [Theory]
    [InlineData("2026-Q2")]
    [InlineData("2026-06")]
    [InlineData("2026-08")]
    [InlineData("2026-09")]
    public void Rental_market_rank_claims_match_each_period_data(string period)
    {
        var data = Resolve(period);
        var cities = data.Aggregation.GetMarketAnalysis().Cities;
        var ordinals = new[] { "", "", "ثاني", "ثالث", "رابع", "خامس" };
        var rankClaim = new System.Text.RegularExpressions.Regex(@"(?:(?<ord>ثاني|ثالث|رابع|خامس)\s+)?أكبر مدينة من حيث الصفقات الإيجارية");
        var wrong = new List<string>();
        foreach (var city in cities)
        {
            var content = data.Content.Get(city.Id);
            if (content is null) continue;
            var rank = 1 + cities.Count(c => c.RentalTransactionCount > city.RentalTransactionCount);
            var text = System.Text.Json.JsonSerializer.Serialize(content, new System.Text.Json.JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });
            wrong.AddRange(rankClaim.Matches(text)
                .Where(m => (m.Groups["ord"].Success ? Array.IndexOf(ordinals, m.Groups["ord"].Value) : 1) != rank)
                .Select(m => $"{city.Id}: \"{m.Value}\" but rank {rank}"));
        }
        Assert.Empty(wrong);
    }

    [Theory]
    [InlineData("2026-03")]
    [InlineData("2026-11")]
    [InlineData("2025-Q4")]
    [InlineData("latest")]
    public void Unknown_periods_resolve_to_null(string period)
    {
        Assert.Null(Periods.ResolveAsync(period).GetAwaiter().GetResult());
    }

    [Fact]
    public void July_city_report_shows_julys_values_and_the_quarters_narrative_without_its_monthly_statements()
    {
        // July: Q3's values cut at July — CPI and media are July's (no workbook assessment); the Q3 text was written for
        // September, so its monthly statements are dropped.
        var july = Resolve("2026-07");
        var file = new CityReportService(TimeProvider.System).GenerateCity(july, "riyadh", ReportFormat.Word)!;
        var model = Model(july, "riyadh");

        Assert.EndsWith("_REGA_2026-07.docx", file.FileName);
        Assert.Contains("– يوليو 2026", WordText(file.Content));
        Assert.All(model.IndicatorRows.Where(r => r.Indicator.Contains("CPI")), r => Assert.Equal("—", r.Status));
        Assert.All(model.IndicatorRows.Where(r => r.Indicator.Contains("CPI")), r => Assert.NotEqual("—", r.Value));
        Assert.True(model.MediaAvailable);
        Assert.StartsWith("يوليو 2026:", model.MediaBreakdown);

        Assert.Contains("العبء السكني لذوي الدخل المحدود (35%) يتجاوز الحد المرجعي للمراقبة (30%).", model.ExecutiveBullets);
        Assert.DoesNotContain(model.IndicatorsBullets, b => b.Contains("الإيجار الفعلي"));
        Assert.Equal("ما يجب مراقبته كمتابعة دورية", model.WatchTitle);
        Assert.Contains(model.IndicatorRows, r => r.Indicator == "العبء السكني لذوي الدخل المحدود" && r.Value != "—");
    }

    [Theory]
    [InlineData("2026-07")]
    [InlineData("2026-04")]
    public void Months_outside_the_text_month_quote_no_monthly_statement_but_their_own_cpi(string period)
    {
        var data = Resolve(period);
        // The only monthly sentence allowed: the CPI sentence rebuilt with this month's own figure and label.
        bool OwnCpi(string b) => b.StartsWith("الإيجار الفعلي للمسكن CPI ") && b.EndsWith($"({data.Scope.Month!.Value.Label}).");
        foreach (var city in data.Aggregation.GetOverview().Cities)
        {
            var m = Model(data, city.Id);
            var prose = m.ExecutiveBullets.Concat(m.IndicatorsBullets).Concat(m.MarketBullets).Concat(m.ConclusionBullets)
                .Concat(m.RecommendationBullets.Skip(1)).Concat(m.WatchList).Concat(m.Axes.Select(a => a.Status));
            Assert.DoesNotContain(prose, b => CityReportModelBuilder.IsMonthlyStatement(b) && !OwnCpi(b));
            Assert.All(new[] { m.ExecutiveBullets, m.IndicatorsBullets, m.ConclusionBullets, m.WatchList }, s => Assert.NotEmpty(s));
        }
        // Dammam's Q3 conclusions are all CPI statements: in July the section shows a dash.
        if (period == "2026-07") Assert.Equal(new[] { "—" }, Model(data, "dammam").ConclusionBullets);
    }

    [Fact]
    public void April_city_report_has_no_media_and_aprils_cpi()
    {
        var april = Resolve("2026-04");
        var model = Model(april, "riyadh");
        var file = new CityReportService(TimeProvider.System).GenerateCity(april, "riyadh", ReportFormat.Pdf)!;

        Assert.EndsWith("_REGA_2026-04.pdf", file.FileName);
        Assert.False(model.MediaAvailable);
        Assert.Equal("لا تتوفر بيانات رصد إعلامي لمدينة الرياض لهذا الشهر.", Assert.Single(model.MediaBullets));
        Assert.All(model.IndicatorRows.Where(r => r.Indicator.Contains("CPI")), r => Assert.NotEqual("—", r.Value));
    }

    [Fact]
    public void The_text_month_keeps_the_narratives_monthly_statements()
    {
        var model = Model(Resolve("2026-10"), "riyadh");

        Assert.Contains(model.IndicatorsBullets.Concat(model.ExecutiveBullets), CityReportModelBuilder.IsMonthlyStatement);
        Assert.True(model.MediaAvailable);
    }

    [Theory]
    [InlineData("الإيجار الفعلي للمسكن (CPI) (+5.1%) مقابل الزيادة الإيجارية (1%).", true)]
    [InlineData("المستويات المطلقة لم تبلغ الأمان (عبء 44%، فعلي +6.8%): انتكاسة العبء تستوجب إعادة تقييم.", true)]
    [InlineData("استمرار الإيجار الفعلي دون التضخم.", true)]
    [InlineData("معتدلة في الزيادة الإيجارية مع فجوة فعلية", true)]
    [InlineData("متسارعة فعليًا", true)]
    [InlineData("ارتداد العبء (44%) والزخم الفعلي للأسعار يستوجبان مراقبة لصيقة رغم تهدئة الزيادة الإيجارية.", true)]
    [InlineData("الأرقام القياسية مستقرة للشقة (+0%) — سوق هادئة بلا جيوب تضخم حادة.", false)]
    [InlineData("مواصلة الرصد الاستباقي.", false)]
    [InlineData("دراسة استباقية ورصد إعلامي للمدينة.", false)]
    [InlineData("العبء السكني للدخل المحدود (35%) يتجاوز الحد المرجعي (30%).", false)]
    public void Monthly_statements_are_those_built_on_cpi_inflation_media_or_a_month(string text, bool monthly)
        => Assert.Equal(monthly, CityReportModelBuilder.IsMonthlyStatement(text));

    [Fact]
    public void A_month_rebuilds_the_summary_cpi_bullet_with_its_own_figure()
    {
        string[] Bullets(string period, string cityId)
        {
            var view = Resolve(period);
            return PeriodValueMask.Apply(view.Aggregation.GetCity(cityId)!, view.Scope).ExecutiveSummary.Bullets.ToArray();
        }

        // April and July: the text was written for June / September, so the CPI bullet is rebuilt with the month's figure.
        Assert.Contains(Bullets("2026-04", "riyadh"), b => b.Contains("الإيجار الفعلي (CPI) يرتفع 7.3% سنويا على مستوى المدينة (أبريل 2026)."));
        Assert.Contains(Bullets("2026-07", "riyadh"), b => b.Contains("الإيجار الفعلي (CPI) يرتفع 5.3% سنويا على مستوى المدينة (يوليو 2026)."));
        // The text month keeps the workbook's own sentence; a month without a CPI figure has no CPI bullet.
        Assert.Contains(Bullets("2026-06", "riyadh"), b => b.Contains("الإيجار الفعلي (CPI) يرتفع 5.9% سنويا على مستوى المدينة."));
        Assert.DoesNotContain(Bullets("2026-04", "abha"), b => b.Contains("CPI"));
    }

    [Fact]
    public void A_month_report_replaces_the_dropped_actual_rent_sentence_with_the_months_figure()
    {
        var april = Model(Resolve("2026-04"), "madinah");
        var june = Model(Resolve("2026-06"), "madinah");
        // Jazan's text says "إيجار فعلي (-2.7%)…" (no article): it is an actual-rent sentence too.
        var julyJazan = Model(Resolve("2026-07"), "jazan");

        Assert.Contains(april.ExecutiveBullets, b => b.StartsWith("الإيجار الفعلي للمسكن CPI ") && b.Contains("سنويا على مستوى المدينة (أبريل 2026)"));
        Assert.DoesNotContain(april.ExecutiveBullets, b => b.Contains("+10.2%"));
        Assert.Contains(june.ExecutiveBullets, b => b.Contains("الإيجار الفعلي للمسكن CPI (+10.2% سنويًا)"));
        Assert.Equal("الإيجار الفعلي للمسكن CPI يرتفع 0.1% سنويا على مستوى المدينة (يوليو 2026).", julyJazan.ExecutiveBullets[0]);
    }

    // The model exactly as CityReportService builds it for the period.
    private static CityReportModel Model(RentalControlsPeriodData period, string cityId)
    {
        var agg = period.Aggregation;
        var city = PeriodValueMask.Apply(agg.GetCity(cityId)!, period.Scope);
        var panels = PeriodValueMask.Apply(agg.GetIndicators(), period.Scope).CurrentPeriodIndicators;
        return CityReportModelBuilder.Build(
            city,
            CityReportFactsBuilder.Build(agg.GetOverview().Cities.Single(c => c.Id == cityId), city, panels),
            CityReportModelBuilder.ForPeriod(period.Content.Get(cityId)!, period.Scope, MonthlyCpiStatement.ForView(city, period.Scope)),
            new DateOnly(2026, 10, 26), period.Label, period.IsMonthly);
    }

    [Fact]
    public void April_national_report_shows_aprils_actual_rent()
    {
        var april = Resolve("2026-04");
        var model = NationalReportModelBuilder.Build(april.Aggregation.GetMarketAnalysis(), new DateOnly(2026, 10, 26), new NationalReportOptions(), april.Label, isMonthly: true);

        Assert.Contains(model.Rows, r => r.ActualRentText != "—");
        Assert.Equal("ما يجب مراقبته كمتابعة دورية", model.MonitoringTitle);
        Assert.Contains("أبريل 2026", model.AssessmentLine);
        var file = new NationalReportService(Options.Create(new NationalReportOptions()), TimeProvider.System).GenerateNational(april, ReportFormat.Pdf);
        Assert.Equal("التقرير_الوطني_REGA_2026-04.pdf", file.FileName);
    }
    [Theory]
    [InlineData("2026-Q2")]
    [InlineData("2026-04")]
    [InlineData("2026-06")]
    [InlineData("2026-07")]
    [InlineData("2026-08")]
    [InlineData("2026-09")]
    [InlineData(null)]
    public void The_supply_demand_gap_is_the_live_workbook_for_every_period(string? period)
    {
        // The gap workbook is a single sheet with no history: no period cuts it or replaces it with a release's copy.
        var live = System.Text.Json.JsonSerializer.Serialize(RealReportData.Aggregation.GetIndicators().SupplyDemandGap);

        Assert.Equal(live, System.Text.Json.JsonSerializer.Serialize(Resolve(period).Aggregation.GetIndicators().SupplyDemandGap));
    }

    [Fact]
    public async Task Only_live_and_quarter_releases_are_period_sources()
    {
        var root = Path.Combine(Path.GetTempPath(), $"rc-periods-{Guid.NewGuid():N}");
        try
        {
            var repo = new JsonTablesRepository(root);
            var store = RealReportData.LiveStore;
            var live = new RentalControlsAggregationService(store);
            live.Recompute();
            var data = RealReportData.LiveRelease;
            // A leftover monthly release is ignored; a stored copy of the live quarter never replaces live.
            await repo.WriteReleaseAsync(ReleaseMapper.ToRows(Release("2026-11", ReleaseKinds.Monthly), data));
            await repo.WriteReleaseAsync(ReleaseMapper.ToRows(Release("2026-Q3", ReleaseKinds.Quarterly), data));
            var periods = new RentalControlsPeriodService(repo, store, live, RealReportData.LiveContent);

            Assert.DoesNotContain(periods.GetPeriods().Monthly, p => p.Id == "2026-11");
            Assert.Null(await periods.ResolveAsync("2026-11"));
            // Live is the Q3 source (not the stored copy): its view keeps live's own text month (October); a stored
            // release would have its text month capped at the quarter.
            Assert.Equal("2026-10", (await periods.ResolveAsync("2026-Q3"))!.Scope.TextMonth?.Id);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task A_month_shows_the_revised_series_value_with_the_published_assessment()
    {
        var root = Path.Combine(Path.GetTempPath(), $"rc-periods-{Guid.NewGuid():N}");
        try
        {
            var repo = new JsonTablesRepository(root);
            var store = RealReportData.LiveStore;
            var live = new RentalControlsAggregationService(store);
            live.Recompute();
            // Published +5.0% for July; the current workbook's series revised July to 5.26 (QA: Buraidah August
            // 9.2% → 9.9%). The value follows the series; the month's written assessment is kept.
            await repo.WriteMonthlyMovementAsync(
                [new("2026-07", "riyadh", 0, "الإيجار الفعلي للمسكن (CPI)", "مدينة - \u200E2026-07", "+5.0%", "ارتفاع", DateTimeOffset.Now)]);
            var periods = new RentalControlsPeriodService(repo, store, live, RealReportData.LiveContent);

            var row = (await periods.ResolveAsync("2026-07"))!.Aggregation.GetCity("riyadh")!.MarketMovementTable
                .Single(r => r.Indicator == "الإيجار الفعلي للمسكن (CPI)");

            Assert.Equal(("+5.3%", "ارتفاع"), (row.Change, row.Assessment));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static ReleaseRow Release(string id, string kind)
        => new(id, kind, id, "2026-Q3", "الربع الثالث 2026", DateTimeOffset.Now, null);

    // From development (report review): a monthly report shows only its own month's media.
    [Theory]
    [InlineData("najran", "نجران")]
    [InlineData("abha", "أبها")]
    [InlineData("bahah", "الباحة")]
    public void September_city_report_shows_no_media_for_a_city_without_september_media(string cityId, string cityName)
    {
        var september = Resolve("2026-09");
        var model = Model(september, cityId);
        var text = WordText(new CityReportService(TimeProvider.System).GenerateCity(september, cityId, ReportFormat.Word)!.Content);

        Assert.False(model.MediaAvailable);
        Assert.Equal($"لا تتوفر بيانات رصد إعلامي لمدينة {cityName} لهذا الشهر.", Assert.Single(model.MediaBullets));
        Assert.DoesNotContain("الانطباع العام:", text);
        Assert.DoesNotContain("أغسطس 2026: إيجابي", text);
    }

    [Fact]
    public void September_city_report_shows_september_media()
    {
        var model = Model(Resolve("2026-09"), "riyadh");

        Assert.True(model.MediaAvailable);
        Assert.StartsWith("سبتمبر 2026: ", model.MediaBreakdown);
    }

    [Fact]
    public void Q2_national_report_is_labelled_and_built_from_Q2_data()
    {
        var service = new NationalReportService(Options.Create(new NationalReportOptions()), TimeProvider.System);

        var file = service.GenerateNational(Resolve("2026-Q2"), ReportFormat.Word);

        Assert.Equal("التقرير_الوطني_REGA_2026-Q2.docx", file.FileName);
        var text = WordText(file.Content);
        Assert.DoesNotContain("مؤشرات عواصم المناطق الإدارية", text); // the subheader was removed at the client's request
        Assert.DoesNotContain("فترة التقرير", text); // client review: no separate period line in the body
        Assert.DoesNotContain("الربع الثالث 2026", text);
        Assert.Equal(("التقرير الوطني لحالة المدن للضوابط الإيجارية – الربع الثاني 2026", "فترة التقرير: الربع الثاني 2026 (تقرير ربع سنوي)"), WordTitleAndSubject(file.Content));
    }

    [Fact]
    public void Monthly_city_report_is_labelled_with_the_month()
    {
        var service = new CityReportService(TimeProvider.System);

        var file = service.GenerateCity(Resolve("2026-08"), "riyadh", ReportFormat.Word);

        Assert.NotNull(file);
        Assert.EndsWith("_REGA_2026-08.docx", file.FileName);
        var text = WordText(file.Content);
        Assert.Contains("– أغسطس 2026", text);
        Assert.DoesNotContain("فترة التقرير", text);
        Assert.Equal("فترة التقرير: أغسطس 2026 (تقرير شهري)", WordTitleAndSubject(file.Content).Subject);
        // Monthly report: the CPI labels carry the period word (the month) beside them, no month line.
        Assert.Contains("الإيجار الفعلي للمسكن CPI", text);
        Assert.DoesNotContain("الإيجار الفعلي للمسكن CPIأغسطس 2026", text);
        Assert.DoesNotContain("شهري\u00a0أغسطس 2026", text);
        Assert.DoesNotContain("التضخم العام (CPI)أغسطس 2026", text);
    }

    [Fact]
    public void Quarterly_city_report_keeps_the_cpi_month_line()
    {
        var file = new CityReportService(TimeProvider.System).GenerateCity(Resolve("2026-Q3"), "riyadh", ReportFormat.Word)!;

        Assert.Contains("الإيجار الفعلي للمسكن CPI \u00a0سبتمبر\u00a0", WordText(file.Content));
    }

    // The PDF footer prints the creation date; the Word footer carries the same line.
    [Fact]
    public void Word_city_report_footer_carries_the_creation_date()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero));
        var file = new CityReportService(clock).GenerateCity(Resolve("2026-Q3"), "riyadh", ReportFormat.Word)!;

        Assert.Contains("تاريخ الإنشاء: 2026/11/03 · صفحة ", WordText(file.Content));
    }

    [Fact]
    public void Pdf_reports_carry_the_period_in_their_metadata()
    {
        var national = new NationalReportService(Options.Create(new NationalReportOptions()), TimeProvider.System)
            .GenerateNational(Resolve("2026-Q2"), ReportFormat.Pdf);
        var city = new CityReportService(TimeProvider.System).GenerateCity(Resolve("2026-08"), "riyadh", ReportFormat.Pdf)!;

        Assert.Equal("التقرير_الوطني_REGA_2026-Q2.pdf", national.FileName);
        Assert.EndsWith("_REGA_2026-08.pdf", city.FileName);
        Assert.Contains(PdfString("فترة التقرير: الربع الثاني 2026 (تقرير ربع سنوي)"), PdfInfo(national.Content));
        Assert.Contains(PdfString("فترة التقرير: أغسطس 2026 (تقرير شهري)"), PdfInfo(city.Content));
    }

    // PDF document-info strings are UTF-16BE hex (<FEFF…>); compare in that form.
    private static string PdfString(string s)
        => "FEFF" + Convert.ToHexString(System.Text.Encoding.BigEndianUnicode.GetBytes(s));

    private static string PdfInfo(byte[] pdf)
        => System.Text.Encoding.Latin1.GetString(pdf).ToUpperInvariant();

    private static (string? Title, string? Subject) WordTitleAndSubject(byte[] content)
    {
        using var ms = new MemoryStream(content);
        using var doc = WordprocessingDocument.Open(ms, false);
        return (doc.PackageProperties.Title, doc.PackageProperties.Subject);
    }

    private static string WordText(byte[] content)
    {
        using var ms = new MemoryStream(content);
        using var doc = WordprocessingDocument.Open(ms, false);
        var main = doc.MainDocumentPart!;
        return main.Document.Body!.InnerText
            + string.Concat(main.HeaderParts.Select(h => h.Header!.InnerText))
            + string.Concat(main.FooterParts.Select(f => f.Footer!.InnerText));
    }
}
