using AqarInsights.Api.Models.Assistant;
using AqarInsights.Api.Services.Assistant;
using AqarInsights.Api.Models;
using AqarInsights.Api.Models.ForeignOwnership;
using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Snapshots;
using AqarInsights.Api.Services.RentalControls;
using AqarInsights.Api.Services.RentalControls.Snapshots;

namespace AqarInsights.Api.Tests.Assistant;

public class AssistantSettingsTests
{
    [Fact]
    public void IsConfigured_false_when_no_key_and_remote_endpoint()
    {
        var s = new AssistantSettings { BaseUrl = "https://api.groq.com/openai/v1", Model = "llama-3.3-70b-versatile", ApiKey = "" };
        Assert.False(s.IsConfigured);
    }

    [Fact]
    public void IsConfigured_true_when_key_model_and_url_present()
    {
        var s = new AssistantSettings { BaseUrl = "https://api.groq.com/openai/v1", Model = "llama-3.3-70b-versatile", ApiKey = "k" };
        Assert.True(s.IsConfigured);
    }

    [Fact]
    public void IsConfigured_true_for_keyless_local_endpoint()
    {
        var s = new AssistantSettings { BaseUrl = "http://localhost:11434/v1", Model = "llama3.1", ApiKey = "" };
        Assert.True(s.IsConfigured);
    }
}

public class AssistantContextBuilderTests
{
    private static FakeRentalControlsAgg WithRiyadh() => new()
    {
        Market = new MarketAnalysisData
        {
            LastQuarter = "2026-Q2",
            Cities = { new CityMarketMetric { Id = "riyadh", Name = "الرياض", OperationalStatus = "مفعّل", Classification = "متسارع", Cagr = 12.5, HousingBurden = 41, VacancyRate = 3.2, RentalTransactionCount = 45.6 } },
        },
        Overview = new OverviewData
        {
            NationalSummary = new NationalSummaryData { AcceleratedCities = 4, RegulationActivated = 2 },
            Cities = { new CityData { Id = "riyadh", Name = "الرياض", ClassificationLabel = "accelerated" } },
        },
    };

    private sealed class FakePeriods : IRentalControlsPeriodService
    {
        public ReportingPeriodsResponse Periods = new("", [], []);
        public Dictionary<string, RentalControlsPeriodData?> ById = new(StringComparer.Ordinal);
        public List<string?> Requested = new();

        public FakePeriods() { }
        public FakePeriods(RentalControlsPeriodData? defaultData) => _defaultData = defaultData;
        private readonly RentalControlsPeriodData? _defaultData;

        public ReportingPeriodsResponse GetPeriods() => Periods;
        public Task<RentalControlsPeriodData?> ResolveAsync(string? period, CancellationToken cancellationToken = default)
        {
            Requested.Add(period);
            return Task.FromResult(ById.TryGetValue(period ?? "", out var data) ? data : _defaultData);
        }
    }

    private static RentalControlsPeriodData AugustView() => new(
        "2026-08", "أغسطس 2026", IsMonthly: true,
        new FakeRentalControlsAgg
        {
            Market = new MarketAnalysisData
            {
                LastQuarter = "2026-Q3",
                Cities = { new CityMarketMetric { Id = "jeddah", Name = "جدة", Cagr = 7.5 } },
            },
        },
        Content: null!, PeriodScope.None);

    [Fact]
    public void HasData_false_when_no_data_in_either_product()
    {
        var b = new AssistantContextBuilder(new FakeRentalControlsAgg(), new FakeForeignAgg());
        Assert.False(b.HasData);
    }

    [Fact]
    public void DataContext_includes_city_name_and_latest_quarter()
    {
        var b = new AssistantContextBuilder(WithRiyadh(), new FakeForeignAgg());
        var ctx = b.BuildDataContext(isAr: true);
        Assert.Contains("2026-Q2", ctx);
        Assert.Contains("الرياض", ctx);
        Assert.Contains("12.5", ctx);
    }

    [Fact]
    public void RentalContext_no_longer_emits_indicators_block()
    {
        var b = new AssistantContextBuilder(WithRiyadh(), new FakeForeignAgg());
        var ctx = b.BuildDataContext(isAr: true);
        Assert.DoesNotContain("[INDICATORS]", ctx);
    }

    [Fact]
    public void ForeignContext_grounds_on_foreign_ownership_summary()
    {
        var foreign = new FakeForeignAgg
        {
            Summary = new SummaryResponse { Kpi = new KpiSummaryDto { TotalTransactions = 1038, TopNationality = "الهند", TopMarket = "الدمام" } },
        };
        var b = new AssistantContextBuilder(new FakeRentalControlsAgg(), foreign);
        var ctx = b.BuildDataContext(isAr: true, product: AssistantContextBuilder.ForeignProduct);
        Assert.Contains("foreign real-estate ownership", ctx);
        Assert.Contains("totalTransactions=1038", ctx);
        Assert.Contains("الهند", ctx);
        Assert.DoesNotContain("[CITIES]", ctx);
    }

    [Fact]
    public void SystemPrompt_states_grounding_rules_and_language()
    {
        var b = new AssistantContextBuilder(WithRiyadh(), new FakeForeignAgg());
        var prompt = b.BuildSystemPrompt("ar");
        Assert.Contains("Use ONLY the DASHBOARD_DATA", prompt);
        Assert.Contains("Arabic", prompt);
    }

    [Fact]
    public void RentalContext_includes_city_market_state_badge()
    {
        var b = new AssistantContextBuilder(WithRiyadh(), new FakeForeignAgg());
        var ctx = b.BuildDataContext(isAr: true);
        Assert.Contains("marketState", ctx);
        Assert.Contains("تحت ضغط مرتفع", ctx);
    }

    [Fact]
    public async Task SystemPromptAsync_grounds_on_the_selected_period()
    {
        var periods = new FakePeriods(AugustView());
        var b = new AssistantContextBuilder(WithRiyadh(), new FakeForeignAgg(), periods);

        var prompt = await b.BuildSystemPromptAsync("ar", AssistantContextBuilder.RentalProduct, "2026-08");

        Assert.Contains("2026-08", periods.Requested);
        Assert.Contains("selected period: أغسطس 2026", prompt);
        Assert.Contains("جدة", prompt);
        Assert.DoesNotContain("الرياض", prompt); // the live dataset is not mixed in
    }

    [Fact]
    public async Task SystemPromptAsync_falls_back_to_live_data_for_unknown_period()
    {
        var b = new AssistantContextBuilder(WithRiyadh(), new FakeForeignAgg(), new FakePeriods(null));
        var prompt = await b.BuildSystemPromptAsync("ar", AssistantContextBuilder.RentalProduct, "2031-Q9");
        Assert.Contains("latest quarter: 2026-Q2", prompt);
        Assert.Contains("الرياض", prompt);
    }

    [Fact]
    public void RentalContext_includes_gap_and_media_blocks()
    {
        var agg = WithRiyadh();
        agg.Indicators = new IndicatorsData
        {
            SupplyDemandGap =
            {
                new SupplyDemandGapCity
                {
                    CityId = "riyadh", CityName = "الرياض",
                    Kpis = new GapKpis
                    {
                        FirstYear = new GapYearKpi { Year = 2025, Gap = -12000, ActualGap = -11500 },
                        SecondYear = new GapYearKpi { Year = 2026, Gap = -9000 },
                        MonthActual = new GapMonthKpi { Period = "2026-08", Label = "أغسطس 2026", Gap = -800 },
                    },
                },
            },
        };
        agg.CityIndex = new List<CityListItem> { new() { CityId = "riyadh", CityName = "الرياض" } };
        agg.CityLookup = _ => new CityAnalysisData
        {
            MediaMonitoring = new MediaMonitoring
            {
                Available = true, MonthLabel = "أغسطس 2026", OverallSentiment = "متباين",
                PositivePct = 40, NeutralPct = 35, NegativePct = 25,
            },
        };

        var ctx = new AssistantContextBuilder(agg, new FakeForeignAgg()).BuildDataContext(isAr: true);

        Assert.Contains("[SUPPLY_DEMAND_GAP]", ctx);
        Assert.Contains("الرياض|2025:-11500|2026:-9000|أغسطس 2026:-800", ctx);
        Assert.Contains("[MEDIA] media sentiment, أغسطس 2026", ctx);
        Assert.Contains("الرياض|متباين|40|35|25", ctx);
    }

    [Fact]
    public void ForeignContext_includes_ownership_split_from_owner_type_filters()
    {
        var foreign = new FakeForeignAgg
        {
            SummaryByFilter = f => f?.OwnerType switch
            {
                "فرد" => new SummaryResponse { Kpi = new KpiSummaryDto { TotalTransactions = 80.5m, TotalValue = 400, AvgTransactionValue = 5 } },
                "شركة" => new SummaryResponse { Kpi = new KpiSummaryDto { TotalTransactions = 19.5m, TotalValue = 600, AvgTransactionValue = 30 } },
                _ => new SummaryResponse { Kpi = new KpiSummaryDto { TotalTransactions = 100, TopNationality = "الهند" } },
            },
        };
        var b = new AssistantContextBuilder(new FakeRentalControlsAgg(), foreign);

        var ctx = b.BuildDataContext(isAr: true, product: AssistantContextBuilder.ForeignProduct);

        Assert.Contains("[OWNERSHIP]", ctx);
        Assert.Contains("فرد|80.5|400|5", ctx);
        Assert.Contains("شركة|19.5|600|30", ctx);
        Assert.Contains("weighted by the foreign ownership share", ctx);
    }

    [Fact]
    public void RentalContext_includes_public_vacancy_annual_history_and_recommendations()
    {
        var agg = WithRiyadh();
        agg.Overview.Cities[0].Recommendation = "الاستمرار في تفعيل الضوابط";
        agg.Market.Recommendations.Add(new MarketRecommendation
        {
            Priority = 1, Title = "تكثيف المراقبة", AffectedCities = "الرياض، جدة", Reason = "نشاط مرتفع",
        });
        agg.Indicators = new IndicatorsData
        {
            CurrentPeriodIndicators =
            {
                new IndicatorPanel
                {
                    Id = "public-vacancy", Title = "الشواغر العامة", PeriodLabel = "أغسطس 2026",
                    Cities = { new CityIndicatorValue { CityId = "riyadh", CityName = "الرياض", Value = 1.4, DisplayValue = "1.4%" } },
                },
            },
            HistoricalIndicators =
            {
                new HistoricalIndicator
                {
                    Id = "housing-burden-hist", Title = "العبء السكني", Years = { "2024", "2025" },
                    Cities =
                    {
                        new HistoricalCityRow
                        {
                            CityName = "الرياض",
                            Values = { ["2024"] = 38.0, ["2025"] = null },
                        },
                    },
                },
            },
        };

        var ctx = new AssistantContextBuilder(agg, new FakeForeignAgg()).BuildDataContext(isAr: true);

        Assert.Contains("[PUBLIC_VACANCY] الشواغر العامة, أغسطس 2026", ctx);
        Assert.Contains("الرياض|1.4%", ctx);
        Assert.Contains("[ANNUAL العبء السكني]", ctx);
        Assert.Contains("الرياض|2024:38|2025:—", ctx);
        Assert.Contains("[CITY_RECOMMENDATIONS]", ctx);
        Assert.Contains("الرياض|الاستمرار في تفعيل الضوابط", ctx);
        Assert.Contains("[MARKET_RECOMMENDATIONS]", ctx);
        Assert.Contains("1|تكثيف المراقبة|الرياض، جدة|نشاط مرتفع", ctx);
    }

    [Fact]
    public void ForeignContext_includes_monthly_crosstabs_distribution_and_strategic_lists()
    {
        var foreign = new FakeForeignAgg
        {
            Summary = new SummaryResponse
            {
                Kpi = new KpiSummaryDto { TotalTransactions = 100 },
                MonthlySummary = { new MonthlySummaryDto { Month = "2026-08", Label = "أغسطس", Total = 50, Value = 900, Purchases = 40, Sales = 8, Gifts = 2 } },
                GrowthRates = { new GrowthRateDto { Month = "2026-08", Rate = 12.3m } },
            },
            Nationalities = new NationalitiesResponse
            {
                PropertyTypeByNationality = { new PropertyTypeByNationalityDto { Nationality = "الهند", Types = { ["سكني"] = 30.5m } } },
            },
            Financial = new FinancialResponse
            {
                ValueDistribution = { new TransactionValueDistributionDto { Range = "أقل من 500 ألف", Count = 20, AvgValue = 320000 } },
            },
            Strategic = new StrategicResponse
            {
                Insights = new StrategicInsightsDto
                {
                    HotSpots = { new HotSpotDto { City = "الرياض", District = "العليا", Transactions = 15, Value = 90, Growth = 25 } },
                },
            },
        };

        var ctx = new AssistantContextBuilder(new FakeRentalControlsAgg(), foreign)
            .BuildDataContext(isAr: true, product: AssistantContextBuilder.ForeignProduct);

        Assert.Contains("[MONTHLY]", ctx);
        Assert.Contains("أغسطس|50|900|40|8|2|12.3", ctx);
        Assert.Contains("[PROPERTY_BY_NATIONALITY]", ctx);
        Assert.Contains("الهند|سكني:30.5", ctx);
        Assert.Contains("[VALUE_DISTRIBUTION]", ctx);
        Assert.Contains("أقل من 500 ألف|20|320000", ctx);
        Assert.Contains("[HOT_SPOTS]", ctx);
        Assert.Contains("الرياض|العليا|15|90|25", ctx);
    }

    [Fact]
    public async Task SystemPromptAsync_appends_past_quarters_as_history()
    {
        var q2 = new RentalControlsPeriodData(
            "2026-Q2", "الربع الثاني 2026", IsMonthly: false,
            new FakeRentalControlsAgg
            {
                Market = new MarketAnalysisData
                {
                    LastQuarter = "2026-Q2",
                    Cities = { new CityMarketMetric { Id = "jeddah", Name = "جدة", Cagr = 7.5, HousingBurden = 33 } },
                },
            },
            Content: null!, PeriodScope.None);
        var periods = new FakePeriods
        {
            // The live quarter is Q3; Q2 is a closed release.
            Periods = new ReportingPeriodsResponse("2026-Q3",
                [],
                [new ReportingPeriodOption("2026-Q3", "الربع الثالث 2026"), new ReportingPeriodOption("2026-Q2", "الربع الثاني 2026")]),
            ById = { ["2026-Q2"] = q2 },
        };
        var b = new AssistantContextBuilder(WithRiyadh(), new FakeForeignAgg(), periods);

        var prompt = await b.BuildSystemPromptAsync("ar", AssistantContextBuilder.RentalProduct, period: null);

        Assert.Contains("[HISTORY الربع الثاني 2026]", prompt);
        Assert.Contains("جدة|7.5|", prompt);
        Assert.DoesNotContain("2026-Q3", periods.Requested); // the grounded quarter is not re-resolved as history
        Assert.Contains("Past quarters appear in [HISTORY", prompt);
    }

    [Fact]
    public async Task SystemPromptAsync_ignores_period_for_foreign_product()
    {
        var foreign = new FakeForeignAgg
        {
            Summary = new SummaryResponse { Kpi = new KpiSummaryDto { TotalTransactions = 10, TopNationality = "الهند", TopMarket = "الدمام" } },
        };
        var periods = new FakePeriods(AugustView());
        var b = new AssistantContextBuilder(new FakeRentalControlsAgg(), foreign, periods);

        var prompt = await b.BuildSystemPromptAsync("ar", AssistantContextBuilder.ForeignProduct, "2026-08");

        Assert.Empty(periods.Requested);
        Assert.Contains("foreign real-estate ownership", prompt);
    }
}
