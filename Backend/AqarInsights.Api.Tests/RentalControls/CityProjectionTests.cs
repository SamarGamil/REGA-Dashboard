using Xunit;
using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Raw;
using AqarInsights.Api.Services.RentalControls.Projections;

namespace AqarInsights.Api.Tests.RentalControls;

public class CityProjectionTests
{
    [Fact]
    public void Build_ProducesComplete6TabCityAnalysis()
    {
        // Arrange: CityData from overview (already has classification/status/kpis)
        var overviewCity = new CityData
        {
            Id = "riyadh",
            Name = "الرياض",
            Cagr = "8.2%",
            Classification = "نمو متسارع",
            ClassificationLabel = "accelerated",
            VacancyRate = "4.2%",
            InvestmentReturn = "5.8%",
            ActualRentalGrowth = "+10.5%",
            RentToIncome = "28.3%",
            HousingBurden = "35.7%",
            OperationalStatus = "الضوابط مفعلة",
            Recommendation = "مراقبة دقيقة للنمو الإيجاري",
            ImpactData = new ImpactData { Available = true, Description = "مراقبة دقيقة" },
            Priority = 1
        };

        // CityRawData with bullets, movement, and a rentals series
        var raw = new CityRawData
        {
            CityId = "riyadh",
            Bullets = new List<string>
            {
                "ارتفاع في الطلب على الإيجار",
                "انخفاض في المعروض",
                "استقرار في الأسعار"
            },
            MovementRows = new List<RawMovementRow>
            {
                new RawMovementRow
                {
                    Indicator = "الطلب على الإيجار",
                    Level = "مرتفع",
                    Change = "↑12%",
                    Assessment = "إيجابي"
                }
            },
            RentalsSeries = new List<RawSeriesPoint>
            {
                new RawSeriesPoint
                {
                    Period = "2025-Q1",
                    Values = new Dictionary<string, double>
                    {
                        ["الإجمالي"] = 45000,
                        ["شقة"] = 25000,
                        ["فيلا"] = 12000,
                        ["دور"] = 5000,
                        ["دوبلاكس"] = 2000,
                        ["استديو"] = 1000
                    }
                },
                new RawSeriesPoint
                {
                    Period = "2025-Q2",
                    Values = new Dictionary<string, double>
                    {
                        ["الإجمالي"] = 48000,
                        ["شقة"] = 27000,
                        ["فيلا"] = 13000,
                        ["دور"] = 5200,
                        ["دوبلاكس"] = 2100,
                        ["استديو"] = 900
                    }
                },
                new RawSeriesPoint
                {
                    Period = "2025-Q3",
                    Values = new Dictionary<string, double>
                    {
                        ["الإجمالي"] = 50000,
                        ["شقة"] = 28000,
                        ["فيلا"] = 14000,
                        ["دور"] = 5500,
                        ["دوبلاكس"] = 2200,
                        ["استديو"] = 1100
                    }
                }
            },
            SalesSeries = new List<RawSeriesPoint>
            {
                new RawSeriesPoint
                {
                    Period = "2025-Q3",
                    Values = new Dictionary<string, double>
                    {
                        ["الإجمالي"] = 12000,
                        ["شقة"] = 6000,
                        ["فيلا"] = 4000,
                        ["دور"] = 1500,
                        ["دوبلاكس"] = 500,
                        ["أرض"] = 100
                    }
                }
            },
            RentIndexSeries = new List<RawSeriesPoint>
            {
                new RawSeriesPoint
                {
                    Period = "2025-03",
                    Values = new Dictionary<string, double> { ["الرقم القياسي"] = 105.2 },
                    Change = "+5.2%"
                }
            },
            AffordabilitySeries = new List<RawSeriesPoint>
            {
                new RawSeriesPoint
                {
                    Period = "2025-Q3",
                    Values = new Dictionary<string, double> { ["النسبة"] = 28.3 }
                }
            },
            RoiSeries = new List<RawSeriesPoint>
            {
                new RawSeriesPoint
                {
                    Period = "2025-Q3",
                    Values = new Dictionary<string, double> { ["العائد"] = 5.8 }
                }
            },
            IncreaseSeries = new List<RawSeriesPoint>
            {
                new RawSeriesPoint
                {
                    Period = "2025-Q3",
                    Values = new Dictionary<string, double> { ["القيمة"] = 10.5 }
                }
            },
            RentIndexByTypeSeries = new List<RawSeriesPoint>
            {
                new RawSeriesPoint { Period = "2025-Q3", Values = new Dictionary<string, double> { ["شقة"] = 1.33, ["دور"] = 1.42, ["فيلا"] = 1.43 } }
            },
            RentVsInflationSeries = new List<RawSeriesPoint>
            {
                new RawSeriesPoint { Period = "2025-03", Values = new Dictionary<string, double> { ["الإيجار الفعلي"] = 4.5, ["التضخم العام"] = 2.2 } }
            }
        };

        // Act
        var result = CityProjection.Build(overviewCity, raw);

        // Assert: header reproduces today's output
        Assert.Equal("الرياض", result.CityName);
        Assert.Equal("الضوابط مفعلة", result.OperationalStatus);
        Assert.Equal("نمو متسارع", result.Classification);
        Assert.Equal("#DC2626", result.ClassificationColor);
        // Period is derived from the latest quarterly period in the raw series (2025-Q3 here).
        Assert.Equal("الربع الثالث 2025", result.Period);
        Assert.Equal("", result.LastUpdate);

        // Executive summary: bullets + badge
        Assert.Equal(3, result.ExecutiveSummary.Bullets.Count);
        Assert.Contains("ارتفاع في الطلب على الإيجار", result.ExecutiveSummary.Bullets);
        Assert.Equal("تحت ضغط مرتفع", result.ExecutiveSummary.StatusBadge.Label);
        Assert.Equal("#DC2626", result.ExecutiveSummary.StatusBadge.Color);

        // Media monitoring: available=false
        Assert.False(result.MediaMonitoring.Available);

        // KPIs: 5 items (from CityAnalysisDeriver)
        Assert.Equal(5, result.Kpis.Count);
        Assert.Equal("الزيادة الإيجارية", result.Kpis[0].Label);
        Assert.Equal("+10.5%", result.Kpis[0].Value);
        Assert.Equal("الشواغر الإيجارية", result.Kpis[1].Label);
        // Vacancy comes from the vacancy series (none here), not the overview's 4.2%.
        Assert.Equal(("—", "—"), (result.Kpis[1].Value, result.Kpis[1].Status));

        // Market movement
        Assert.Single(result.MarketMovementTable);
        Assert.Equal("الطلب على الإيجار", result.MarketMovementTable[0].Indicator);
        Assert.Equal("مرتفع", result.MarketMovementTable[0].Level);

        // 5 tabs
        Assert.Equal(5, result.MarketAnalysisTabs.Count);
        Assert.Equal("rentals", result.MarketAnalysisTabs[0].Id);
        Assert.Equal("sales", result.MarketAnalysisTabs[1].Id);
        Assert.Equal("rent-inflation", result.MarketAnalysisTabs[2].Id);
        Assert.Equal("affordability", result.MarketAnalysisTabs[3].Id);
        Assert.Equal("investment", result.MarketAnalysisTabs[4].Id);

        // a. rentals: stacked bar, one series per unit type
        var rentals = result.MarketAnalysisTabs[0];
        Assert.Equal("bar", rentals.ChartType);
        Assert.True(rentals.Stacked);
        Assert.Contains(rentals.Series, s => s.Name == "شقة");
        Assert.NotEmpty(rentals.Categories);
        Assert.NotEmpty(rentals.TableColumns);

        // c. rent-inflation: two line series
        var ri = result.MarketAnalysisTabs[2];
        Assert.Equal("line", ri.ChartType);
        Assert.Equal(2, ri.Series.Count);
        Assert.Contains(ri.Series, s => s.Name == "الإيجار الفعلي للمسكن");
        Assert.Contains(ri.Series, s => s.Name == "التضخم العام");

        // e. affordability: threshold 30
        var aff = result.MarketAnalysisTabs[3];
        Assert.Equal("line", aff.ChartType);
        Assert.NotNull(aff.Threshold);
        Assert.Equal(30, aff.Threshold!.Value);

        // f. investment: threshold 6
        var inv = result.MarketAnalysisTabs[4];
        Assert.Equal(6, inv.Threshold!.Value);

        // Journey: current stage = "الضوابط مفعلة"
        Assert.Equal(6, result.CityJourney.Count);
        var currentStage = result.CityJourney.First(s => s.IsCurrent);
        Assert.Equal("الضوابط مفعلة", currentStage.Label);

        // Recommendations (from deriver)
        Assert.Single(result.Recommendations);
        Assert.Equal("الضوابط مفعلة", result.Recommendations[0].Recommendation);

        // WhatToMonitor (from deriver)
        Assert.Equal(4, result.WhatToMonitor.Count);
    }

    [Fact]
    public void Build_SalesTab_IncludesOtherAndBuildingTypes()
    {
        var overviewCity = new CityData { Id = "riyadh", Name = "الرياض", OperationalStatus = "الضوابط مفعلة" };
        var raw = new CityRawData
        {
            CityId = "riyadh",
            SalesSeries = new List<RawSeriesPoint>
            {
                new RawSeriesPoint
                {
                    Period = "2026-Q3",
                    Values = new Dictionary<string, double>
                    {
                        ["شقة"] = 3970, ["فيلا"] = 2480, ["دور"] = 2404, ["دوبلكس"] = 1192, ["أرض"] = 3203,
                        ["أخرى"] = 1398, ["عمارة"] = 0, ["الإجمالي"] = 14647
                    }
                }
            }
        };

        var result = CityProjection.Build(overviewCity, raw);

        var sales = result.MarketAnalysisTabs.Single(t => t.Id == "sales");
        Assert.Equal(new[] { "شقة", "فيلا", "دور", "دوبلكس", "أرض", "أخرى", "عمارة" }, sales.Series.Select(s => s.Name));
        Assert.Equal(1398, sales.Series.Single(s => s.Name == "أخرى").Data[^1]);
        Assert.Equal(new[] { "الفترة", "شقة", "فيلا", "دور", "دوبلكس", "أرض", "أخرى", "عمارة", "الإجمالي" }, sales.TableColumns);
        Assert.Equal("14,647", sales.TableRows[^1][^1]);
    }

    [Fact]
    public void Movement_rows_without_a_period_get_their_series_latest_period()
    {
        // The Q2 2026 workbooks write just "مدينة"; newer ones append the row's period ("مدينة - ‎2026-Q3").
        var overviewCity = new CityData { Id = "riyadh", Name = "الرياض", OperationalStatus = "لا إجراء" };
        var raw = new CityRawData
        {
            CityId = "riyadh",
            MovementRows =
            {
                new RawMovementRow { Indicator = "عدد الصفقات الإيجارية", Level = "مدينة", Change = "+37%" },
                new RawMovementRow { Indicator = "الإيجار الفعلي للمسكن (CPI)", Level = "مدينة", Change = "+5.9%" },
                new RawMovementRow { Indicator = "العبء السكني للدخل المحدود", Level = "مدينة", Change = "35%" },
                new RawMovementRow { Indicator = "نسبة الإيجار إلى الدخل", Level = "مدينة - \u200E2026-Q1", Change = "15%" },
            },
            RentalsSeries = { new RawSeriesPoint { Period = "2026-Q1" }, new RawSeriesPoint { Period = "2026-Q2" } },
            RentIndexSeries = { new RawSeriesPoint { Period = "2026-05" }, new RawSeriesPoint { Period = "2026-06" } },
            AffordabilitySeries = { new RawSeriesPoint { Period = "2026-Q2" } },
        };

        var levels = CityProjection.Build(overviewCity, raw).MarketMovementTable.Select(r => r.Level).ToList();

        Assert.Equal(new[]
        {
            "مدينة - \u200E2026-Q2",
            "مدينة - \u200E2026-06",
            "مدينة",                  // no burden series: no period is invented
            "مدينة - \u200E2026-Q1",  // a stated period is kept
        }, levels);
    }

    [Fact]
    public void Blank_workbook_cells_are_gaps_not_zeros()
    {
        // Older workbooks have no "أخرى"/"عمارة" sales columns, and some cities have an empty rent-CPI block.
        var overviewCity = new CityData { Id = "abha", Name = "أبها", OperationalStatus = "لا إجراء" };
        var raw = new CityRawData
        {
            CityId = "abha",
            SalesSeries = { new RawSeriesPoint { Period = "2026-Q2", Values = new() { ["شقة"] = 100, ["عمارة"] = 0, ["الإجمالي"] = 100 } } },
            RentVsInflationSeries = { new RawSeriesPoint { Period = "2026-06", Values = new() { ["التضخم العام"] = 2.2 } } },
            AffordabilitySeries =
            {
                new RawSeriesPoint { Period = "2026-Q1", Values = new() { ["الدخل"] = 1 } },
                new RawSeriesPoint { Period = "2026-Q2", Values = new() { ["النسبة"] = 11.65 } },
            },
        };

        var result = CityProjection.Build(overviewCity, raw);

        var sales = result.MarketAnalysisTabs.Single(t => t.Id == "sales");
        Assert.Null(sales.Series.Single(s => s.Name == "أخرى").Data[^1]);
        Assert.Equal(0, sales.Series.Single(s => s.Name == "عمارة").Data[^1]); // a written 0 stays 0
        Assert.Equal("—", sales.TableRows[^1][sales.TableColumns.IndexOf("أخرى")]);
        var rentInflation = result.MarketAnalysisTabs.Single(t => t.Id == "rent-inflation");
        Assert.Null(rentInflation.Series.Single(s => s.Name == "الإيجار الفعلي للمسكن").Data[^1]);
        var affordability = result.MarketAnalysisTabs.Single(t => t.Id == "affordability");
        Assert.Equal(new double?[] { null, 11.65 }, affordability.Series[0].Data);
        Assert.Equal("—", affordability.TableRows[0][1]);
        // Display rounding is half away from zero, as Excel shows it (11.65 → 11.7, not 11.6).
        Assert.Equal(11.7, result.RentalIndicatorCharts.Single(c => c.Label == "نسبة الإيجار إلى الدخل").Points[^1].Value);
    }

    [Fact]
    public void Build_RentalIncreaseSeries_ReturnsFullHistory_NotCappedAt6()
    {
        var overviewCity = new CityData { Id = "riyadh", Name = "الرياض", OperationalStatus = "الضوابط مفعلة" };
        var periods = new[] { "2024-Q2", "2024-Q3", "2024-Q4", "2025-Q1", "2025-Q2", "2025-Q3", "2025-Q4", "2026-Q1", "2026-Q2" };
        var values = new[] { 15.0, 15, 16, 17, 17, 19, 17, 14, 9 };
        var raw = new CityRawData
        {
            CityId = "riyadh",
            IncreaseSeries = periods.Select((p, i) => new RawSeriesPoint
            {
                Period = p,
                Values = new Dictionary<string, double> { ["القيمة"] = values[i] }
            }).ToList()
        };

        var result = CityProjection.Build(overviewCity, raw);

        Assert.Equal(periods, result.RentalIncreaseSeries.Select(p => p.Period));
        Assert.Equal(values, result.RentalIncreaseSeries.Select(p => p.Value));
        Assert.True(result.RentalIncreaseSeries[^1].IsLast);
        Assert.Equal(1, result.RentalIncreaseSeries.Count(p => p.IsLast));
        // The 6-bar city-analysis card is unchanged.
        Assert.Equal(6, result.RentalIndicatorCharts.Single(c => c.Label == "الزيادة الإيجارية").Points.Count);
    }

    [Fact]
    public void Build_MapsMediaMonitoring_WhenRawMediaAvailable()
    {
        var overviewCity = new CityData
        {
            Id = "riyadh",
            Name = "الرياض",
            Classification = "نمو متسارع",
            ClassificationLabel = "accelerated",
            OperationalStatus = "الضوابط مفعلة",
        };

        var raw = new CityRawData
        {
            CityId = "riyadh",
            Media = new List<RawMediaMonitoring>
            {
                new RawMediaMonitoring
                {
                    Period = "2026-06",
                    Available = true,
                    OverallSentiment = "متوازن",
                    PositivePct = 10, NeutralPct = 70, NegativePct = 20,
                    DiscussionSharePct = 50,
                    Topics = new List<string> { "موضوع يونيو" },
                    NeutralImpressions = new List<string> { "محايد يونيو" },
                },
                new RawMediaMonitoring
                {
                    Period = "2026-07",
                    Available = true,
                    OverallSentiment = "سلبي",
                    PositivePct = 4,
                    NeutralPct = 69,
                    NegativePct = 27,
                    DiscussionSharePct = 88.8,
                    Topics = new List<string> { "ارتفاع الأسعار" },
                    PositiveImpressions = new List<string> { "إيجابي" },
                    NeutralImpressions = new List<string> { "محايد" },
                    NegativeImpressions = new List<string> { "سلبي 1", "سلبي 2" },
                },
            }
        };

        var result = CityProjection.Build(overviewCity, raw);

        // Latest available month (July) drives the headline fields.
        Assert.True(result.MediaMonitoring.Available);
        Assert.Equal("2026-07", result.MediaMonitoring.Period);
        Assert.Equal("يوليو 2026", result.MediaMonitoring.MonthLabel);
        // The workbook labels July "سلبي", but 69% is neutral: the largest share decides.
        Assert.Equal("محايد", result.MediaMonitoring.OverallSentiment);
        Assert.Equal(4, result.MediaMonitoring.PositivePct);
        Assert.Equal(69, result.MediaMonitoring.NeutralPct);
        Assert.Equal(27, result.MediaMonitoring.NegativePct);
        Assert.Equal(new[] { "ارتفاع الأسعار" }, result.MediaMonitoring.TopHeadlines);
        Assert.Equal(new[] { "إيجابي" }, result.MediaMonitoring.PositiveImpressions);
        Assert.Equal(new[] { "محايد" }, result.MediaMonitoring.NeutralImpressions);
        Assert.Equal(new[] { "سلبي 1", "سلبي 2" }, result.MediaMonitoring.NegativeImpressions);

        // History carries both months chronologically.
        Assert.Equal(new[] { "2026-06", "2026-07" }, result.MediaMonitoring.History.Select(h => h.Period));
        Assert.Equal("يونيو 2026", result.MediaMonitoring.History[0].MonthLabel);
        Assert.Equal(new[] { "موضوع يونيو" }, result.MediaMonitoring.History[0].TopHeadlines);
    }

    [Fact]
    public void Build_MediaMonitoringUnavailable_WhenRawMediaNull()
    {
        var overviewCity = new CityData
        {
            Id = "abha",
            Name = "أبها",
            Classification = "نمو مستدام",
            ClassificationLabel = "sustainable",
            OperationalStatus = "لا إجراء",
        };

        var result = CityProjection.Build(overviewCity, new CityRawData { CityId = "abha", Media = null });

        Assert.False(result.MediaMonitoring.Available);
        Assert.Empty(result.MediaMonitoring.NegativeImpressions);
    }

    [Fact]
    public void Kpis_carry_frequency_and_the_citys_own_period_with_vacancy_on_the_release_month()
    {
        var overviewCity = new CityData { Id = "riyadh", Name = "الرياض", OperationalStatus = "لا إجراء" };
        var raw = new CityRawData
        {
            CityId = "riyadh",
            IncreaseSeries = { new() { Period = "2026-Q2" }, new() { Period = "2026-Q3" } },
            RoiSeries = { new() { Period = "2026-Q3" } },
            AffordabilitySeries = { new() { Period = "2026-Q2" } },
            BurdenSeries = { new() { Period = "2026-Q2" } },
        };
        // Vacancy: the release month is the newest across all cities (jeddah's June), not riyadh's own February.
        var series = new List<IndicatorSeriesPoint>
        {
            new() { Indicator = "vacancy", CityId = "riyadh", Year = 2026, Period = 2, Value = 7 },
            new() { Indicator = "vacancy", CityId = "jeddah", Year = 2026, Period = 6, Value = 8 },
        };

        var kpis = CityProjection.Build(overviewCity, raw, series: series).Kpis.ToDictionary(k => k.Label);

        Assert.Equal(("quarterly", "الربع الثالث 2026"), (kpis["الزيادة الإيجارية"].Frequency, kpis["الزيادة الإيجارية"].PeriodLabel));
        Assert.Equal(("monthly", "يونيو 2026"), (kpis["الشواغر الإيجارية"].Frequency, kpis["الشواغر الإيجارية"].PeriodLabel));
        // Riyadh has no June value: its February 7% is not shown under June.
        Assert.Equal("—", kpis["الشواغر الإيجارية"].Value);
        Assert.Equal(("quarterly", "الربع الثالث 2026"), (kpis["العائد الإيجاري"].Frequency, kpis["العائد الإيجاري"].PeriodLabel));
        Assert.Equal(("quarterly", "الربع الثاني 2026"), (kpis["نسبة الإيجار إلى الدخل"].Frequency, kpis["نسبة الإيجار إلى الدخل"].PeriodLabel));
        Assert.Equal(("quarterly", "الربع الثاني 2026"), (kpis["العبء السكني للدخل المحدود"].Frequency, kpis["العبء السكني للدخل المحدود"].PeriodLabel));
    }

    [Fact]
    public void Every_city_kpi_label_maps_to_a_panel_frequency()
    {
        var overviewCity = new CityData { Id = "abha", Name = "أبها", OperationalStatus = "لا إجراء" };

        var kpis = CityProjection.Build(overviewCity, new CityRawData { CityId = "abha" }).Kpis;

        // A renamed label in CityAnalysisDeriver would silently lose its badge; this catches it.
        Assert.All(kpis, k => Assert.NotEqual("", k.Frequency));
    }

    [Fact]
    public void Kpis_without_data_keep_the_frequency_but_no_period()
    {
        var overviewCity = new CityData { Id = "abha", Name = "أبها", OperationalStatus = "لا إجراء" };

        var kpis = CityProjection.Build(overviewCity, new CityRawData { CityId = "abha" }).Kpis.ToDictionary(k => k.Label);

        Assert.Equal(("quarterly", ""), (kpis["العائد الإيجاري"].Frequency, kpis["العائد الإيجاري"].PeriodLabel));
        Assert.Equal(("monthly", ""), (kpis["الشواغر الإيجارية"].Frequency, kpis["الشواغر الإيجارية"].PeriodLabel));
    }
}
