using System.Text.Json;
using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Raw;

namespace AqarInsights.Api.Services.RentalControls.Projections;

public static class CityProjection
{
    public static CityAnalysisData Build(CityData overviewCity, CityRawData raw, KpiThresholdsOptions? kpiThresholds = null, RentalPolicyOptions? policy = null, IReadOnlyList<IndicatorSeriesPoint>? series = null)
    {
        var rentalPolicy = policy ?? new RentalPolicyOptions();
        var indicatorSeries = series ?? Array.Empty<IndicatorSeriesPoint>();
        var kpiCity = WithSeriesVacancy(overviewCity, indicatorSeries);
        var kpis = CityAnalysisDeriver.Kpis(kpiCity, kpiThresholds);
        if (kpiCity.VacancyRate == Dash)
            kpis.Single(k => k.Label == VacancyKpi).Status = Dash;
        PanelPeriods.Apply(kpis, indicatorSeries, raw);
        return new CityAnalysisData
        {
            CityName = overviewCity.Name,
            OperationalStatus = overviewCity.OperationalStatus,
            Classification = overviewCity.Classification,
            ClassificationColor = RentalControlsMappings.ClassificationColor(overviewCity.ClassificationLabel),
            Period = ReportingPeriod.Latest(new[] { raw }),
            LastUpdate = "",
            ExecutiveSummary = new ExecutiveSummary
            {
                Bullets = raw.Bullets,
                StatusBadge = CityAnalysisDeriver.Badge(overviewCity)
            },
            MediaMonitoring = BuildMediaMonitoring(raw),
            Kpis = kpis,
            RentalIndicatorCharts = BuildIndicatorCharts(overviewCity, raw),
            RentalIncreaseSeries = BuildSeriesPoints(raw.IncreaseSeries, "القيمة"),
            RentalIncreaseMonthly = indicatorSeries
                .Where(p => p.Indicator == "rentalChange" && p.CityId == overviewCity.Id && p.Period is >= 1 and <= 12)
                .OrderBy(p => p.Year).ThenBy(p => p.Period)
                .Select(p => new MonthlyPoint { Period = $"{p.Year:D4}-{p.Period:D2}", Value = Round(p.Value, 1) }).ToList(),
            ActualRentMonthly = MonthlyPoints(raw.RentVsInflationSeries, "الإيجار الفعلي"),
            MarketMovementTable = raw.MovementRows.Select(r => new MarketMovementRow
            {
                Indicator = r.Indicator,
                Level = MovementLevel(r, raw),
                Change = r.Change,
                Assessment = r.Assessment
            }).ToList(),
            MarketAnalysisTabs = BuildTabs(raw, rentalPolicy),
            Recommendations = CityAnalysisDeriver.Recommendations(overviewCity),
            WhatToMonitor = CityAnalysisDeriver.WhatToMonitor(kpiCity, kpiThresholds),
            CityJourney = CityAnalysisDeriver.Journey(overviewCity.OperationalStatus)
        };
    }

    private const string Dash = "—";

    // Newer workbooks append each movement row's period to its level ("مدينة - ‎2026-Q3"); older ones (the Q2 2026
    // release) write just "مدينة". Such a row gets the latest period of the series it summarises — the period the
    // newer workbooks state (checked on every live row) — and keeps its plain level when that series is missing.
    private static readonly System.Text.RegularExpressions.Regex LevelPeriod = new(@"[0-9]{4}-(?:Q[1-4]|[0-9]{2})\s*$");
    private static readonly System.Text.RegularExpressions.Regex PeriodId = new(@"^[0-9]{4}-(?:Q[1-4]|[0-9]{2})$");

    private static string MovementLevel(RawMovementRow row, CityRawData raw)
    {
        if (LevelPeriod.IsMatch(row.Level)) return row.Level;
        var latest = MovementSeries(row.Indicator, raw)?
            .Select(p => p.Period).Where(p => PeriodId.IsMatch(p)).Max(StringComparer.Ordinal);
        return latest is null ? row.Level : $"{row.Level} - \u200E{latest}";
    }

    private static List<RawSeriesPoint>? MovementSeries(string indicator, CityRawData raw) => indicator switch
    {
        "عدد الصفقات الإيجارية" or "متوسط قيمة الصفقة الإيجارية (ريال/وحدة)" => raw.RentalsSeries,
        "عدد الصفقات البيعية" or "متوسط قيمة الصفقة البيعية (ريال)" => raw.SalesSeries,
        "نسبة الإيجار إلى الدخل" => raw.AffordabilitySeries,
        "العبء السكني للدخل المحدود" => raw.BurdenSeries,
        _ when indicator.StartsWith("الرقم القياسي لإيجار", StringComparison.Ordinal) => raw.RentIndexByTypeSeries,
        _ when indicator.Contains("(CPI)", StringComparison.Ordinal) || indicator.Contains("(شهري)", StringComparison.Ordinal) => raw.RentIndexSeries,
        _ => null,
    };
    private const string VacancyKpi = "الشواغر الإيجارية";

    // Half away from zero on the decimal value, as Excel shows it (11.65 → 11.7; Math.Round on the double gives 11.6).
    private static double Round(double value, int digits)
        => (double)Math.Round((decimal)value, digits, MidpointRounding.AwayFromZero);

    // The vacancy card and its watch-list row take value and month from the vacancy series (the overview workbook's
    // vacancy column has no month); "—" when the city has no value for the series' latest month.
    private static CityData WithSeriesVacancy(CityData city, IReadOnlyList<IndicatorSeriesPoint> series)
    {
        var copy = JsonSerializer.Deserialize<CityData>(JsonSerializer.SerializeToUtf8Bytes(city))!;
        copy.VacancyRate = MonthlySeries.Latest(series, "vacancy").ByCity.TryGetValue(city.Id, out var v)
            ? IndicatorsProjection.Percent(v)
            : Dash;
        return copy;
    }

    private static MediaMonitoring BuildMediaMonitoring(CityRawData raw)
    {
        var available = (raw.Media ?? new List<RawMediaMonitoring>())
            .Where(m => m.Available)
            .OrderBy(m => m.Period, StringComparer.Ordinal)
            .ToList();
        if (available.Count == 0)
            return new MediaMonitoring { Available = false };

        var latest = available[^1];
        return new MediaMonitoring
        {
            Available = true,
            Period = latest.Period,
            MonthLabel = FormatPeriod(latest.Period),
            OverallSentiment = MediaSentimentClassifier.Classify(latest.PositivePct, latest.NeutralPct, latest.NegativePct),
            PositivePct = latest.PositivePct,
            NeutralPct = latest.NeutralPct,
            NegativePct = latest.NegativePct,
            TopHeadlines = latest.Topics,
            PositiveImpressions = latest.PositiveImpressions,
            NeutralImpressions = latest.NeutralImpressions,
            NegativeImpressions = latest.NegativeImpressions,
            History = available.Select(ToPoint).ToList(),
        };
    }

    private static MediaSentimentPoint ToPoint(RawMediaMonitoring m) => new()
    {
        Period = m.Period,
        MonthLabel = FormatPeriod(m.Period),
        OverallSentiment = MediaSentimentClassifier.Classify(m.PositivePct, m.NeutralPct, m.NegativePct),
        PositivePct = m.PositivePct,
        NeutralPct = m.NeutralPct,
        NegativePct = m.NegativePct,
        DiscussionSharePct = m.DiscussionSharePct,
        TopHeadlines = m.Topics,
        PositiveImpressions = m.PositiveImpressions,
        NeutralImpressions = m.NeutralImpressions,
        NegativeImpressions = m.NegativeImpressions,
    };

    private static List<MarketAnalysisTab> BuildTabs(CityRawData raw, RentalPolicyOptions policy)
    {
        var rentBurdenLimit = policy.RentBurdenLimitPct;
        var yieldLimit = policy.YieldActivationPct;
        return new List<MarketAnalysisTab>
        {
            StackedTab("rentals", "الصفقات الإيجارية", "توزيع عدد الصفقات الإيجارية حسب نوع الوحدة لكل ربع.",
                raw.RentalsSeries, new[] { "شقة", "دور", "فيلا", "دوبلاكس", "استديو" }, 8),
            StackedTab("sales", "الصفقات البيعية", "توزيع عدد الصفقات البيعية حسب نوع العقار لكل ربع.",
                raw.SalesSeries, new[] { "شقة", "فيلا", "دور", "دوبلكس", "أرض", "أخرى", "عمارة" }, 8),
            MultiLineTab("rent-inflation", "مؤشرات الإيجار والتضخم",
                "مقارنة التغير السنوي للإيجار الفعلي للمسكن مقابل التضخم العام.",
                raw.RentVsInflationSeries,
                new[] { ("الإيجار الفعلي", "الإيجار الفعلي للمسكن"), ("التضخم العام", "التضخم العام") }, 12, "%"),
            ThresholdTab("affordability", "القدرة الإيجارية",
                $"نسبة الإيجار إلى الدخل مقابل حد المراقبة {rentBurdenLimit}%.",
                raw.AffordabilitySeries, "النسبة", 8, rentBurdenLimit, $"حد المراقبة {rentBurdenLimit}%", "%"),
            ThresholdTab("investment", "العائد من الاستثمار",
                $"العائد من الاستثمار مقابل حد التفعيل {yieldLimit}%.",
                raw.RoiSeries, "العائد", 8, yieldLimit, $"حد التفعيل {yieldLimit}%", "%"),
        };
    }

    private static List<string> LastNPeriods(List<RawSeriesPoint> series, int take)
    {
        var periods = DistinctByPeriod(series).Select(p => p.Period).ToList();
        return periods.Count <= take ? periods : periods.Skip(periods.Count - take).ToList();
    }

    private static MarketAnalysisTab StackedTab(string id, string label, string insight,
        List<RawSeriesPoint> series, string[] types, int take)
    {
        var periods = LastNPeriods(series, take);
        var byPeriod = DistinctByPeriod(series).ToDictionary(p => p.Period, p => p.Values);
        var seriesOut = types.Select(t => new TabSeries
        {
            Name = t,
            Data = periods.Select(p => byPeriod.TryGetValue(p, out var v) && v.TryGetValue(t, out var d) ? Round(d, 0) : (double?)null).ToList()
        }).ToList();

        var columns = new List<string> { "الفترة" };
        columns.AddRange(types);
        columns.Add("الإجمالي");
        var rows = periods.Select(p =>
        {
            var v = byPeriod.TryGetValue(p, out var vv) ? vv : new Dictionary<string, double>();
            var row = new List<string> { FormatPeriod(p) };
            double total = 0;
            foreach (var t in types)
            {
                if (!v.TryGetValue(t, out var x))
                {
                    row.Add("—");
                    continue;
                }
                total += x;
                row.Add(x.ToString("N0"));
            }
            row.Add(v.TryGetValue("الإجمالي", out var tot) ? tot.ToString("N0") : total.ToString("N0"));
            return row;
        }).ToList();

        return new MarketAnalysisTab
        {
            Id = id, Label = label, Insight = insight, ChartType = "bar", Stacked = true,
            Series = seriesOut, Categories = periods.Select(FormatPeriod).ToList(),
            TableColumns = columns, TableRows = rows
        };
    }

    private static MarketAnalysisTab MultiLineTab(string id, string label, string insight,
        List<RawSeriesPoint> series, (string key, string name)[] keys, int take, string suffix)
    {
        var periods = LastNPeriods(series, take);
        var byPeriod = DistinctByPeriod(series).ToDictionary(p => p.Period, p => p.Values);
        var seriesOut = keys.Select(k => new TabSeries
        {
            Name = k.name,
            Data = periods.Select(p => byPeriod.TryGetValue(p, out var v) && v.TryGetValue(k.key, out var d) ? Round(d, 2) : (double?)null).ToList()
        }).ToList();

        var columns = new List<string> { "الفترة" };
        columns.AddRange(keys.Select(k => k.name));
        var rows = periods.Select(p =>
        {
            var v = byPeriod.TryGetValue(p, out var vv) ? vv : new Dictionary<string, double>();
            var row = new List<string> { FormatPeriod(p) };
            foreach (var k in keys)
                row.Add(v.TryGetValue(k.key, out var d) ? $"{Round(d, 2)}{suffix}" : "—");
            return row;
        }).ToList();

        return new MarketAnalysisTab
        {
            Id = id, Label = label, Insight = insight, ChartType = "line", Stacked = false,
            Series = seriesOut, Categories = periods.Select(FormatPeriod).ToList(),
            TableColumns = columns, TableRows = rows
        };
    }

    private static MarketAnalysisTab ThresholdTab(string id, string label, string insight,
        List<RawSeriesPoint> series, string key, int take, double threshold, string thresholdLabel, string suffix)
    {
        var periods = LastNPeriods(series, take);
        var byPeriod = DistinctByPeriod(series).ToDictionary(p => p.Period, p => p.Values);
        var data = periods.Select(p => byPeriod.TryGetValue(p, out var v) && v.TryGetValue(key, out var d) ? Round(d, 2) : (double?)null).ToList();
        var rows = periods.Select((p, i) => new List<string> { FormatPeriod(p), data[i] is { } value ? $"{value}{suffix}" : "—" }).ToList();

        return new MarketAnalysisTab
        {
            Id = id, Label = label, Insight = insight, ChartType = "line", Stacked = false,
            Series = new List<TabSeries> { new TabSeries { Name = label, Data = data } },
            Categories = periods.Select(FormatPeriod).ToList(),
            Threshold = new TabThreshold { Value = threshold, Label = thresholdLabel },
            TableColumns = new List<string> { "الفترة", key },
            TableRows = rows
        };
    }

    // Real per-city quarterly series only; indicators without a series (burden, vacancy) are omitted.
    private static List<RentalIndicatorChart> BuildIndicatorCharts(CityData c, CityRawData raw)
    {
        var charts = new List<RentalIndicatorChart>
        {
            BuildIndicatorChart("الزيادة الإيجارية", c.ActualRentalGrowth, "محايد", "up", raw.IncreaseSeries, "القيمة"),
            BuildIndicatorChart("نسبة الإيجار إلى الدخل", c.RentToIncome, "إيجابي", "stable", raw.AffordabilitySeries, "النسبة"),
        };
        return charts.Where(ch => ch.Points.Count > 0).ToList();
    }

    private static RentalIndicatorChart BuildIndicatorChart(string label, string value, string status, string trend, List<RawSeriesPoint> series, string key)
    {
        var points = BuildSeriesPoints(series, key);
        var last = points.Count <= 6 ? points : points.Skip(points.Count - 6).ToList();
        return new RentalIndicatorChart
        {
            Label = label,
            Value = value,
            Status = status,
            Trend = trend,
            Points = last
        };
    }

    private static List<MonthlyPoint> MonthlyPoints(List<RawSeriesPoint> series, string key)
        => DistinctByPeriod(series)
            .Where(p => p.Values.ContainsKey(key) && System.Text.RegularExpressions.Regex.IsMatch(p.Period, @"^\d{4}-(0[1-9]|1[0-2])$"))
            .OrderBy(p => p.Period, StringComparer.Ordinal)
            .Select(p => new MonthlyPoint { Period = p.Period, Value = Round(p.Values[key], 2) }).ToList();

    private static List<RentalIndicatorPoint> BuildSeriesPoints(List<RawSeriesPoint> series, string key)
    {
        var points = DistinctByPeriod(series)
            .Where(p => p.Values.ContainsKey(key))
            .Select(p => (p.Period, Value: Round(p.Values[key], 1)))
            .ToList();
        return points.Select((p, i) => new RentalIndicatorPoint
        {
            Period = FormatPeriod(p.Period),
            Value = p.Value,
            IsLast = i == points.Count - 1
        }).ToList();
    }

    private static string FormatPeriod(string period)
    {
        var q = System.Text.RegularExpressions.Regex.Match(period, @"^(\d{4})-Q([1-4])$");
        if (q.Success) return $"{q.Groups[1].Value}-Q{q.Groups[2].Value}";
        var m = System.Text.RegularExpressions.Regex.Match(period, @"^(\d{4})-(\d{2})$");
        if (m.Success && int.TryParse(m.Groups[2].Value, out var mi) && mi is >= 1 and <= 12)
            return $"{ArabicMonths[mi - 1]} {m.Groups[1].Value}";
        return period;
    }

    private static readonly string[] ArabicMonths =
    {
        "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
        "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"
    };

    private static List<RawSeriesPoint> DistinctByPeriod(List<RawSeriesPoint> series)
    {
        var seen = new HashSet<string>();
        var result = new List<RawSeriesPoint>();
        foreach (var p in series)
        {
            if (seen.Add(p.Period))
                result.Add(p);
        }
        return result;
    }
}
