using System.Globalization;
using System.Text.RegularExpressions;
using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Projections;
using AqarInsights.Api.Services.RentalControls.Snapshots;

namespace AqarInsights.Api.Services.RentalControls.Reports;

public static class CityReportModelBuilder
{
    private static string MarketStateColor(string operationalStatus) => operationalStatus switch
    {
        "الضوابط مفعلة" => "#C0392B",
        "يتطلب دراسة تفصيلية" => "#E08A3C",
        "يتطلب دراسة استباقية" => "#123A52",
        _ => "#1CA59A",
    };

    // Status word → (text color, tint bg): platform classification/CPI assessment words and policy statuses.
    private static (string Color, string Bg) StatusColors(string status) => status switch
    {
        "سلبي" or "نمو متسارع" or "متجاوز" => ("#DC2626", "#FDECEC"),
        "ارتفاع" or "نمو مرتفع" or "متابعة" or "دون الحد" => ("#C67A1E", "#FBF1E2"),
        "إيجابي" or "نمو مستدام" or "ضمن الحد" => ("#2E9B5B", "#E8F4EC"),
        _ => ("#6B7280", "#EEF2F4"),
    };

    // The colour a city report gives an indicator's figure (KPI card / supply strip / table): red for «متجاوز» and a rising
    // actual rent, amber for «متابعة»/«دون الحد», green for «ضمن الحد» and a falling actual rent. The national report
    // colours its cells the same way.
    public static string ValueColorOf(CityReportIndicatorFact f, bool isActualRent) => ValueColor(f, isActualRent);

    private static string ValueColor(CityReportIndicatorFact f, bool isActualRent) => ActualRentColors(f, isActualRent).Color;

    // The actual rent is red when the platform rates it "ارتفاع" and green when "تراجع" (no pressure signal); other
    // indicators keep the status palette.
    private static (string Color, string Bg) ActualRentColors(CityReportIndicatorFact f, bool isActualRent) => (isActualRent, f.Status) switch
    {
        (true, "ارتفاع") => ("#DC2626", "#FDECEC"),
        (true, "تراجع" or "انخفاض") => ("#2E9B5B", "#E8F4EC"),
        _ => StatusColors(f.Status),
    };

    // Used when the city narrative has no media bullets; follows the data's sentiment.
    private static string DefaultMediaBullet(string sentiment) => sentiment switch
    {
        "سلبي" => "يغلب الطابع السلبي على المحتوى المتداول، ويستحق المتابعة.",
        "متباين" => "الانطباعات في المحتوى المتداول متباينة بين الإيجابي والسلبي.",
        "إيجابي" => "يغلب الطابع الإيجابي على المحتوى المتداول.",
        "متوازن" => "الانطباع العام للرصد الإعلامي متوازن.",
        _ => "يغلب الطابع المحايد على المحتوى المتداول.",
    };

    // Media sentiment word → text color (neutral/balanced keep the report's teal).
    private static string SentimentColor(string sentiment) => sentiment switch
    {
        "سلبي" => "#DC2626",
        "متباين" => "#C67A1E",
        "إيجابي" => "#2E9B5B",
        _ => "#1CA59A",
    };

    // A monthly indicator with no month of its own (e.g. no actual-rent series) shows the report's period word.
    private static string PeriodOf(CityReportIndicatorFact f, string fallbackPeriod)
        => string.IsNullOrEmpty(f.Period) && f.Frequency == ReportFrequency.Monthly ? fallbackPeriod : f.Period;

    // Status words name the reference in full ("دون الحد المرجعي للمراقبة"); colours follow the platform status word.
    private static CityReportKpiCardView Card(CityReportIndicatorFact f, bool isActualRent, string fallbackPeriod, string? label = null) => new()
    {
        Label = label ?? f.DisplayLabel,
        Frequency = f.Frequency,
        Period = PeriodOf(f, fallbackPeriod),
        ValueText = f.Value,
        Note = ReportThresholds.StatusText(f.Status),
        Reference = f.Reference == "—" ? "" : $"الحد المرجعي للمراقبة: {f.Reference}",
        ColorHex = ValueColor(f, isActualRent),
    };

    // The table row's status cell: the figure's colour (the actual rent as on its card), with its tint.
    private static CityReportIndicatorRowView Row(CityReportIndicatorFact f, string fallbackPeriod, bool isActualRent = false)
    {
        var (color, bg) = ActualRentColors(f, isActualRent);
        return new CityReportIndicatorRowView
        {
            Indicator = f.DisplayLabel,
            Frequency = f.Frequency,
            Period = PeriodOf(f, fallbackPeriod),
            Value = f.Value,
            Reference = Symbolic(f.Reference),
            Status = ReportThresholds.StatusText(f.Status),
            StatusColorHex = color,
            StatusBgHex = bg,
        };
    }

    private const string ActivationMonth = "2025-09";
    private const string ChartsFrom = "2025-01";

    // The two charts of page two (January 2025 on): the rental increase is a quarterly indicator, so it is plotted per quarter;
    // the actual rent CPI is monthly. The controls' activation (September 2025) is marked for cities where they are enabled.
    // A series the data lacks leaves its chart empty.
    private static List<CityReportChartView> Charts(CityAnalysisData city, CityReportFacts facts)
    {
        var enabled = facts.OperationalStatus == "الضوابط مفعلة";
        var from = YearMonth.TryParse(ChartsFrom, out var f) ? f : new YearMonth(2025, 1);
        var activation = YearMonth.TryParse(ActivationMonth, out var a) ? a : new YearMonth(2025, 9);

        var quarters = city.RentalIncreaseSeries
            .Select(p => (Ok: YearQuarter.TryParse(p.Period, out var q), Quarter: q, p.Value))
            .Where(p => p.Ok && p.Quarter.Year >= from.Year)
            .OrderBy(p => p.Quarter.Year).ThenBy(p => p.Quarter.Quarter).ToList();
        var activationQuarter = new YearQuarter(activation.Year, (activation.Month - 1) / 3 + 1);
        var increaseMarker = enabled ? quarters.FindIndex(p => p.Quarter == activationQuarter) : -1;
        var increase = new CityReportChartView
        {
            Title = $"الزيادة الإيجارية في {city.CityName}",
            Color = "#C9A227",
            Categories = quarters.Select(p => p.Quarter.Label).ToList(),
            Values = quarters.Select(p => (double?)Math.Round(p.Value, 1)).ToList(),
            MarkerIndex = increaseMarker,
            MarkerLabel = increaseMarker >= 0 ? "قرار تطبيق الضوابط الإيجارية" : "",
        };

        var rent = city.ActualRentMonthly.Where(p => string.CompareOrdinal(p.Period, ChartsFrom) >= 0)
            .OrderBy(p => p.Period, StringComparer.Ordinal).ToList();
        var rentMarker = enabled ? rent.FindIndex(p => p.Period == ActivationMonth) : -1;
        var actualRent = new CityReportChartView
        {
            Title = $"الإيجار الفعلي للمسكن CPI في {city.CityName}",
            Color = "#123A52",
            Categories = rent.Select(p => YearMonth.TryParse(p.Period, out var m) ? m.Label : p.Period).ToList(),
            Values = rent.Select(p => (double?)p.Value).ToList(),
            MarkerIndex = rentMarker,
            MarkerLabel = rentMarker >= 0 ? "قرار تطبيق الضوابط الإيجارية" : "",
        };
        return quarters.Count == 0 && rent.Count == 0 ? new() : new() { increase, actualRent };
    }

    // The axis with its full affordability name and a badge coloured by the platform status of the indicator behind it.
    // A reference in the axis status takes the threshold of the axis's indicator.
    private static CityReportAxis Axis(CityReportAxis a, CityReportFacts facts, IReadOnlyList<ReportThresholds.Indicator> thresholds)
    {
        var name = a.Axis == "القدرة على تحمل السكن" ? "القدرة على تحمل السكن لذوي الدخل المحدود" : a.Axis;
        var axisIndicator = name switch
        {
            "ضغط السوق" => facts.Cagr,
            "المعروض" => facts.Vacancy,
            "القدرة على تحمل السكن لذوي الدخل المحدود" => facts.HousingBurden,
            "الاستثمار" => facts.InvestmentReturn,
            _ => null,
        };
        var fallback = axisIndicator is null ? null
            : ReferenceEquals(axisIndicator, facts.Cagr) ? ReportThresholds.Band(axisIndicator.Reference) : ReportThresholds.Threshold(axisIndicator.Reference);
        var status = ReportThresholds.Apply(ReportTerminology.Apply(a.Status), thresholds, fallback);
        // The market-pressure row reads the city page's «حالة السوق» badge wording (normalised to the report's
        // reference naming), so the word always agrees with the classification colour behind it (the curated axis
        // text can drift from the period's classification).
        if (name == "ضغط السوق")
            status = ReportThresholds.Apply(CityAnalysisDeriver.Badge(facts.ClassificationLabel).Label, thresholds, fallback);
        if (name == "الاستثمار" && facts.InvestmentReturn.Status == "دون الحد")
        {
            var threshold = ReportThresholds.Threshold(facts.InvestmentReturn.Reference);
            status = threshold == "" ? $"العائد الإيجاري دون {ReportThresholds.Label}" : $"العائد الإيجاري دون {ReportThresholds.Label} ({threshold})";
        }
        var platform = name switch
        {
            "ضغط السوق" => facts.Classification,
            "الأسعار" => facts.ActualRentCpi.Status,
            "المعروض" => facts.Vacancy.Status,
            "القدرة على تحمل السكن لذوي الدخل المحدود" => facts.HousingBurden.Status,
            "الاستثمار" => facts.InvestmentReturn.Status,
            _ => "",
        };
        var (color, bg) = name == "الأسعار" ? ActualRentColors(facts.ActualRentCpi, isActualRent: true) : StatusColors(platform);
        return new CityReportAxis { Axis = name, Status = status, ColorHex = color, BgHex = bg };
    }

    // "أكبر من 6%" → "> 6%"; "9.5% فأكثر" → "≥ 9.5%" (ranges and dashes are kept).
    private static string Symbolic(string reference)
    {
        var r = reference.Trim();
        foreach (var (word, symbol) in new[] { ("أكبر من", ">"), ("أكثر من", ">"), ("أقل من", "<") })
            if (r.StartsWith(word + " ", StringComparison.Ordinal)) return $"{symbol} {r[(word.Length + 1)..]}";
        foreach (var (word, symbol) in new[] { ("فأكثر", "≥"), ("فأقل", "≤"), ("أو أقل", "≤"), ("أو أكثر", "≥") })
            if (r.EndsWith(" " + word, StringComparison.Ordinal)) return $"{symbol} {r[..^(word.Length + 1)]}";
        return r;
    }

    private static CityReportSupplyStatView Stat(CityReportIndicatorFact f, bool isActualRent, string fallbackPeriod) => new()
    {
        Label = f.DisplayLabel,
        Value = f.Value,
        Note = ReportThresholds.StatusText(f.Status),
        Reference = f.Reference == "—" ? "" : $"الحد المرجعي للمراقبة: {f.Reference}",
        Frequency = f.Frequency,
        Period = PeriodOf(f, fallbackPeriod),
        ColorHex = ValueColor(f, isActualRent),
    };

    public static CityReportModel Build(CityAnalysisData city, CityReportFacts facts, CityReportContent content, DateOnly generatedOn, string? periodLabel = null, bool isMonthly = false)
    {
        var label = string.IsNullOrWhiteSpace(periodLabel) ? city.Period : periodLabel;
        // Platform texts such as "لا إجراء - ضمن الحدود…" already open with the status. The workbook text often
        // lacks a final period.
        var firstRecommendation = string.IsNullOrEmpty(facts.Recommendation)
            ? facts.OperationalStatus
            : facts.Recommendation.StartsWith(facts.OperationalStatus, StringComparison.Ordinal)
                ? facts.Recommendation
                : $"{facts.OperationalStatus}: {facts.Recommendation}";
        // A monthly report shows its quarter's level but not the platform line's quarter-on-quarter change; a quarterly
        // report names the period the change is measured against.
        firstRecommendation = WithFinalPeriod(isMonthly
            ? WithoutQuarterChange(firstRecommendation)
            : WithChangePeriod(firstRecommendation, city.ExecutiveSummary.Bullets));
        var fallbackPeriod = ReportPeriodText.Word(label);
        // The narrative with the platform's indicator names, and every reference written as the monitoring reference
        // with its threshold; a monthly report leaves out the narrative's quarter-on-quarter comparisons.
        var thresholds = ReportThresholds.From(facts);
        string Narrative(string text) => ReportThresholds.Apply(ReportTerminology.Apply(isMonthly ? WithoutQuarterComparison(text) : text), thresholds);
        List<string> Narratives(IEnumerable<string> texts) => texts.Select(Narrative).ToList();
        var narrativeRecommendations = Narratives(content.RecommendationBullets);
        // A narrative point that already states the study basis for the status replaces the platform line.
        var studyPhrase = facts.OperationalStatus switch
        {
            "يتطلب دراسة استباقية" => "الدراسة الاستباقية",
            "يتطلب دراسة تفصيلية" => "الدراسة التفصيلية",
            _ => null,
        };
        var platformLineReplaced = studyPhrase is not null && narrativeRecommendations.Count > 0
            && narrativeRecommendations[0].StartsWith($"تركز {studyPhrase}", StringComparison.Ordinal);

        var model = new CityReportModel
        {
            HeaderTitle = $"تقرير الضوابط الإيجارية – {city.CityName}",
            HeaderSubtitle = $"مؤشرات مدينة {city.CityName} – {label}",
            CityName = city.CityName,
            PeriodLabel = label,
            PeriodLine = ReportPeriodText.Line(label, isMonthly),
            GeneratedOnLabel = generatedOn.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
            MarketState = facts.OperationalStatus,
            MarketStateColorHex = MarketStateColor(facts.OperationalStatus),
            KpiCards = new[] { facts.Cagr, facts.HousingBurden, facts.InvestmentReturn, facts.ActualRentCpi }.Select(f =>
            {
                var isActualRent = ReferenceEquals(f, facts.ActualRentCpi);
                return Card(f, isActualRent, fallbackPeriod, isActualRent ? "الإيجار الفعلي للمسكن CPI (التغير السنوي)" : null);
            }).ToList(),
            ExecutiveBullets = Narratives(content.ExecutiveBullets.Select(b =>
                CityReportMarketBuilder.IsDemandPoint(b) ? CityReportMarketBuilder.DemandSentence(city) ?? b : b)),
            IndicatorsBullets = Narratives(content.IndicatorsBullets),
            IndicatorRows = new[] { facts.Cagr, facts.RentalIncrease, facts.RentToIncome, facts.HousingBurden, facts.ActualRentCpi, facts.GeneralInflation }
                .Select(f => Row(f, fallbackPeriod, ReferenceEquals(f, facts.ActualRentCpi))).ToList(),
            MarketBullets = Narratives(content.MarketBullets),
            ProductRows = CityReportMarketBuilder.ProductRows(city, content.ProductTable),
            CityAveragesLine = Narrative(CityReportMarketBuilder.AveragesLine(city, content.CityAveragesLine)),
            SupplyInvestment = new[] { facts.Vacancy, facts.ActualRentCpi, facts.InvestmentReturn }.Select(f => Stat(f, ReferenceEquals(f, facts.ActualRentCpi), fallbackPeriod)).ToList(),
            ConclusionBullets = Narratives(content.ConclusionBullets.Take(3)),
            Axes = content.AssessmentAxes.Select(a => Axis(a, facts, thresholds)).ToList(),
            OverallAssessment = $"{facts.OperationalStatus} — {facts.Classification}",
            RecommendationBullets = platformLineReplaced
                ? narrativeRecommendations
                : Narratives(new[] { firstRecommendation }.Concat(content.RecommendationBullets)),
            WatchTitle = "ما يجب مراقبته كمتابعة دورية",
            WatchList = Narratives(content.WatchList),
            Charts = Charts(city, facts),
        };

        var media = city.MediaMonitoring;
        // A monthly report shows only its own month's media: a city whose latest media is from an earlier month
        // has none for the report month (e.g. Najran has August but no September media).
        if (media is { Available: true } && (!isMonthly || string.IsNullOrEmpty(media.MonthLabel) || media.MonthLabel == label))
        {
            model.MediaAvailable = true;
            // The overall impression is derived from the shares shown next to it (largest share wins), so it never
            // contradicts the breakdown (client review: "محايد" beside 51% سلبي, "سلبي" beside 69% محايد). Max 3 bullets.
            model.MediaSentiment = MediaSentimentClassifier.Classify(media.PositivePct, media.NeutralPct, media.NegativePct);
            model.MediaSentimentColorHex = SentimentColor(model.MediaSentiment);
            var monthPrefix = string.IsNullOrEmpty(media.MonthLabel) ? "" : $"{media.MonthLabel}: ";
            model.MediaBreakdown = $"{monthPrefix}إيجابي {media.PositivePct}% — محايد {media.NeutralPct}% — سلبي {media.NegativePct}%";
            (model.MediaMonthPrefix, model.MediaPositivePct, model.MediaNeutralPct, model.MediaNegativePct) =
                (monthPrefix, media.PositivePct, media.NeutralPct, media.NegativePct);
            model.MediaBullets = content.MediaBullets.Count > 0
                ? Narratives(content.MediaBullets.Take(3))
                : new List<string> { DefaultMediaBullet(model.MediaSentiment) };
        }
        else
        {
            model.MediaAvailable = false;
            model.MediaBullets = new List<string> { $"لا تتوفر بيانات رصد إعلامي لمدينة {city.CityName} {(isMonthly ? "لهذا الشهر" : "لهذا الربع")}." };
        }

        return model;
    }

    private const string Dash = "—";

    // "بتراجع 0.8 نقطة" without the period it is measured against.
    private static readonly Regex PointChange = new(
        @"(?<![\p{L}])(?<verb>بتراجع|بارتفاع|بانخفاض|بزيادة)\s+(?<n>\d+(?:\.\d+)?)\s+(?:نقطة|نقاط|نقطتين)(?:\s+مئوية)?(?!\s+(?:مئوية\s+)?عن(?![\p{L}]))",
        RegexOptions.CultureInvariant);

    // Client review: a point change in the platform's assessment line names the period it is measured against, taken
    // from the city page's own statement of the same change ("… بتراجع 0.8 نقطة عن الربع السابق").
    private static string WithChangePeriod(string text, IReadOnlyList<string> platformBullets) => PointChange.Replace(text, m =>
    {
        var same = new Regex($@"{m.Groups["verb"].Value}\s+{Regex.Escape(m.Groups["n"].Value)}\s+(?:نقطة|نقاط|نقطتين)(?:\s+مئوية)?\s+(?<when>عن\s+.+?)\s*(?:\.(?!\d)|[،؛\n]|$)");
        var when = platformBullets.Select(b => same.Match(b)).FirstOrDefault(x => x.Success)?.Groups["when"].Value.Trim();
        return string.IsNullOrEmpty(when) ? m.Value : $"{m.Value} {when}";
    });

    // "بتراجع 0.8 نقطة [مئوية] [عن الربع السابق]" / "دون تغير عن الربع السابق": the change between quarters.
    private static readonly Regex QuarterChange = new(
        @"\s*(?<![\p{L}])(?:(?:بتراجع|بارتفاع|بانخفاض|بزيادة)\s+\d+(?:\.\d+)?\s+(?:نقطة|نقاط|نقطتين)(?:\s+مئوية)?|دون\s+تغير)(?:\s+عن\s+الربع\s+[^\s،.]+(?:\s+\d{4})?)?",
        RegexOptions.CultureInvariant);

    private static string WithoutQuarterChange(string text) => QuarterChange.Replace(text, "");

    // Narrative clauses comparing a quarter with the one before: "وارتد صعودًا في الربع الثالث (34% ← 36%) بعد تحسن سابق",
    // "واستقر في الربع الثالث بعد تحسن سابق (37% ← 35%)", "رغم تراجعها من 14% في الربع السابق".
    private static readonly Regex[] QuarterComparisons =
    {
        new(@"\s+و\S+(?:\s+\S+)?\s+في\s+الربع\s+(?:الأول|الثاني|الثالث|الرابع)(?:\s+\d{4})?(?:\s+بعد\s+تحسن\s+سابق)?\s*\(\d+(?:\.\d+)?%\s*←\s*\d+(?:\.\d+)?%\)(?:\s+بعد\s+تحسن\s+سابق)?", RegexOptions.CultureInvariant),
        new(@"\s+(?:رغم|مع)\s+[^،.؛]*?(?:في|عن|مقارنة\s+ب)\s*الربع\s+(?:السابق|الماضي)", RegexOptions.CultureInvariant),
    };

    private static string WithoutQuarterComparison(string text)
        => QuarterComparisons.Aggregate(text, (t, rx) => rx.Replace(t, ""));

    private static string WithFinalPeriod(string text)
    {
        var trimmed = text.TrimEnd();
        return trimmed.Length == 0 || ".!؟?:…".Contains(trimmed[^1]) ? trimmed : trimmed + ".";
    }

    public static bool IsMonthlyStatement(string text) => MonthlyStatements.IsMonthly(text);

    /// <summary>
    /// The narrative as the period shows it (<see cref="PeriodValueMask.KeepsSentence"/>): outside the text month a
    /// month view drops the statements built on monthly values, and every view drops sentences about a later month. A
    /// section left empty shows a dash; the market section and the recommendation also carry data lines, so they just
    /// drop the statements; without the month's statements there is no media text either. The executive section's first
    /// dropped actual-rent sentence is replaced by <paramref name="monthCpi"/> (the month's own CPI figure,
    /// <see cref="MonthlyCpiStatement.ForView"/>) when there is one.
    /// </summary>
    public static CityReportContent ForPeriod(CityReportContent content, PeriodScope scope, string? monthCpi = null)
    {
        bool Keeps(string text) => PeriodValueMask.KeepsSentence(text, scope);
        List<string> Kept(IEnumerable<string> items) => items.Where(Keeps).ToList();
        List<string> OrDash(List<string> items) => items.Count > 0 ? items : new() { Dash };
        // Media narrative follows the text month: a month view needs the text written for it; a quarter view needs the
        // text written inside the quarter.
        var keepsMonthly = scope.Month is { } m
            ? m == scope.TextMonth
            : scope.Quarter is not { } q || scope.TextMonth is not { } t || t <= q.LastMonth;
        var executive = new List<string>();
        foreach (var b in content.ExecutiveBullets)
        {
            if (Keeps(b) || CityReportMarketBuilder.IsDemandPoint(b)) executive.Add(b);
            else if (monthCpi is not null && MonthlyCpiStatement.IsActualRentSentence(b))
            {
                executive.Add(monthCpi);
                monthCpi = null;
            }
        }
        return new()
        {
            CityId = content.CityId,
            ExecutiveBullets = OrDash(executive),
            IndicatorsBullets = OrDash(Kept(content.IndicatorsBullets)),
            MarketBullets = Kept(content.MarketBullets),
            ProductTable = content.ProductTable,
            CityAveragesLine = Keeps(content.CityAveragesLine ?? "") ? content.CityAveragesLine ?? "" : "",
            MediaBullets = keepsMonthly ? Kept(content.MediaBullets) : new(),
            ConclusionBullets = OrDash(Kept(content.ConclusionBullets)),
            AssessmentAxes = content.AssessmentAxes
                .Select(a => Keeps(a.Status) ? a : new CityReportAxis { Axis = a.Axis, Status = Dash }).ToList(),
            RecommendationBullets = Kept(content.RecommendationBullets),
            WatchList = OrDash(Kept(content.WatchList)),
        };
    }
}
