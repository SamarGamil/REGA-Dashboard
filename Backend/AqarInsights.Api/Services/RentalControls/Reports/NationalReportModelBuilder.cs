using System.Globalization;
using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Snapshots;

namespace AqarInsights.Api.Services.RentalControls.Reports;

public static class NationalReportModelBuilder
{
    private record TrackInfo(int Severity, string TableLabel, string SummaryBucket, string ColorHex);

    private const string Green = "#2E9B5B";
    private const string Amber = "#C67A1E";
    private const string Red = "#DC2626";
    private const string Gray = "#6B7280";

    // Compass grouping of the 14 regional capitals, per the approved report.
    private static readonly Dictionary<string, string> RegionOf = new(StringComparer.Ordinal)
    {
        ["الرياض"] = "الوسطى",
        ["بريدة"] = "الوسطى",
        ["مكة المكرمة"] = "الغربية",
        ["جدة"] = "الغربية",
        ["المدينة المنورة"] = "الغربية",
        ["الدمام"] = "الشرقية",
        ["أبها"] = "الجنوبية",
        ["جازان"] = "الجنوبية",
        ["نجران"] = "الجنوبية",
        ["الباحة"] = "الجنوبية",
        ["تبوك"] = "الشمالية",
        ["حائل"] = "الشمالية",
        ["عرعر"] = "الشمالية",
        ["سكاكا"] = "الشمالية",
    };

    private static TrackInfo Track(string classification) => classification switch
    {
        "نمو متسارع" => new(0, "تفاعلي متسارع", "المتسارع", "#DC2626"),
        "نمو مرتفع" => new(1, "تفاعلي مرتفع", "المرتفع", "#EA580C"),
        "نمو مستدام" => new(2, "استباقي", "الاستباقي", "#0D9488"),
        _ => new(3, string.IsNullOrWhiteSpace(classification) ? "غير مصنّف" : classification, "الاستباقي", "#6B7280"),
    };

    // The colour of a cell: the one the city's own report gives the figure (its platform status); grey («غير متاح») when
    // the figure is missing or the city's facts are not available.
    private static string CellColor(double? value, CityReportIndicatorFact? fact, bool isActualRent = false) =>
        value is null || fact is null || fact.Value == "—" ? Gray : CityReportModelBuilder.ValueColorOf(fact, isActualRent);

    // Arrow against the previous period's displayed value: "↑"/"↓", a long dash when unchanged (the short «–» read as a
    // minus sign beside the figure), none when either value is missing.
    public const string Unchanged = "—";
    private static string Trend(double? now, MarketAnalysisData? previous, string city, Func<CityMarketMetric, double?> pick)
    {
        var before = previous?.Cities.FirstOrDefault(c => c.Name == city) is { } p ? pick(p) : null;
        if (now is null || before is null) return "";
        return now > before ? "↑" : now < before ? "↓" : Unchanged;
    }

    public static NationalReportModel Build(MarketAnalysisData data, DateOnly generatedOn, NationalReportOptions options, string? periodLabel = null, bool isMonthly = false,
        NationalReportContext? context = null)
    {
        // A month's data is its quarter's cut at the month: "الإيجار الفعلي" (the rent CPI) is already that month's.
        var indicatorPeriods = context?.IndicatorPeriods;
        string PeriodOf(string key) => indicatorPeriods is not null && indicatorPeriods.TryGetValue(key, out var p) ? p : "";
        var cities = data.Cities;
        var label = string.IsNullOrWhiteSpace(periodLabel) ? data.LastQuarter : periodLabel;
        var model = new NationalReportModel
        {
            HeaderTitle = "التقرير الوطني لحالة المدن للضوابط الإيجارية",
            AssessmentLine = $"تقييم حالة المدن استنادًا إلى مؤشرات الضوابط الإيجارية – {label}",
            Title = "مؤشرات الضوابط الإيجارية",
            PeriodLabel = label,
            PeriodLine = ReportPeriodText.Line(label, isMonthly),
            GeneratedOnLabel = generatedOn.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
            IndicatorPeriods = indicatorPeriods is null ? new() : new(indicatorPeriods),
        };

        var topRent = cities.Where(c => c.ActualRentalGrowth != null).OrderByDescending(c => c.ActualRentalGrowth).FirstOrDefault();
        var lowYield = cities.Where(c => c.InvestmentReturn != null)
            .OrderBy(c => c.InvestmentReturn).ThenBy(c => c.InvestmentReturnExact ?? c.InvestmentReturn).FirstOrDefault();
        var topBurden = cities.Where(c => c.HousingBurden != null).OrderByDescending(c => c.HousingBurden).FirstOrDefault();
        var topCagr = cities.Where(c => c.Cagr != null).OrderByDescending(c => c.Cagr).FirstOrDefault();

        if (topRent != null)
            model.KpiCards.Add(new ReportKpiCard { Label = "أعلى إيجار فعلي للمسكن CPI", Frequency = ReportFrequency.Monthly, Period = PeriodOf("actual-rent"), ValueText = RentText(topRent), CityName = topRent.Name, IsUp = true, ColorHex = topRent.ActualRentalGrowth > 0 ? Red : Gray });
        if (lowYield != null)
            model.KpiCards.Add(new ReportKpiCard { Label = "أدنى عائد إيجاري", Frequency = ReportFrequency.Quarterly, Period = PeriodOf("investment-return"), ValueText = YieldText(lowYield), CityName = lowYield.Name, IsUp = false, ColorHex = "#2563EB" });
        if (topBurden != null)
            model.KpiCards.Add(new ReportKpiCard { Label = "أعلى عبء سكني لذوي الدخل المحدود", Frequency = ReportFrequency.Quarterly, Period = PeriodOf("housing-burden"), ValueText = PctInt(topBurden.HousingBurden!.Value), CityName = topBurden.Name, IsUp = true, ColorHex = "#EA580C" });
        if (topCagr != null)
            model.KpiCards.Add(new ReportKpiCard { Label = "أعلى معدل نمو إيجاري مركب", ValueText = CagrText(topCagr), CityName = topCagr.Name, IsUp = true, ColorHex = "#B91C1C" });

        int n = cities.Count;
        int a = cities.Count(c => Track(c.Classification).SummaryBucket == "المتسارع");
        int b = cities.Count(c => Track(c.Classification).SummaryBucket == "المرتفع");
        int reg = cities.Count(c => Track(c.Classification).SummaryBucket == "الاستباقي");
        model.SummaryLines.Add($"من أصل {n} مدينة: {Count(a)} في المسار المتسارع، و{Count(b)} في المسار المرتفع، و{Count(reg)} في المسار الاستباقي.");

        int x = cities.Count(c => c.HousingBurden != null && c.HousingBurden > options.BurdenReferenceThreshold);
        int y = cities.Count(c => c.InvestmentReturn != null && c.InvestmentReturn < options.YieldReferenceThreshold);
        model.SummaryLines.Add($"الضغط الأبرز وطنيًا يتركز في القدرة على تحمل السكن، إذ يتجاوز العبء السكني لذوي الدخل المحدود الحد المرجعي للمراقبة ({Pct(options.BurdenReferenceThreshold)}) في {CountIn(x)}، بينما يقل العائد الإيجاري عن الحد المرجعي للمراقبة ({Pct(options.YieldReferenceThreshold)}) في {CountIn(y)}.");
        model.SortNote = "المدن مرتبة تنازليًا حسب معدل النمو الإيجاري المركب.";

        var ordered = cities
            .OrderByDescending(c => c.Cagr ?? double.MinValue)
            .ThenBy(c => c.Name, StringComparer.Ordinal)
            .ToList();
        int rank = 1;
        foreach (var c in ordered)
        {
            var t = Track(c.Classification);
            var facts = context?.CityFacts?.GetValueOrDefault(c.Name);
            model.Rows.Add(new ReportCityRow
            {
                Rank = rank++,
                CityName = c.Name,
                Region = RegionOf.GetValueOrDefault(c.Name, "—"),
                TrackLabel = t.TableLabel,
                TrackColorHex = t.ColorHex,
                BurdenText = c.HousingBurden != null ? PctInt(c.HousingBurden.Value) : "—",
                BurdenColorHex = CellColor(c.HousingBurden, facts?.HousingBurden),
                RentalIncreaseText = c.RentalIncrease != null ? c.RentalIncreaseText ?? Signed(c.RentalIncrease.Value) : "—",
                RentalIncreaseColorHex = CellColor(c.RentalIncrease, facts?.RentalIncrease),
                RentToIncomeText = c.RentToIncome != null ? c.RentToIncomeText ?? PctInt(c.RentToIncome.Value) : "—",
                RentToIncomeColorHex = CellColor(c.RentToIncome, facts?.RentToIncome),
                BurdenTrend = Trend(c.HousingBurden, context?.PreviousQuarter, c.Name, m => m.HousingBurden),
                ActualRentTrend = Trend(c.ActualRentalGrowth, context?.PreviousMonth, c.Name, m => m.ActualRentalGrowth),
                YieldText = c.InvestmentReturn != null ? YieldText(c) : "—",
                YieldColorHex = CellColor(c.InvestmentReturn, facts?.InvestmentReturn),
                ActualRentText = c.ActualRentalGrowth != null ? RentText(c) : "—",
                RentColorHex = CellColor(c.ActualRentalGrowth, facts?.ActualRentCpi, isActualRent: true),
            });
        }

        // The actual-rent point counts the table's own ↓ arrows (the displayed values against the previous month) and
        // names Riyadh when it fell (the client's wording), else the steepest fall, with the value the table prints.
        var fell = ordered
            .Select(c => (c.Name, Row: model.Rows.Single(r => r.CityName == c.Name),
                Before: context?.PreviousMonth?.Cities.FirstOrDefault(p => p.Name == c.Name)?.ActualRentalGrowth, Now: c.ActualRentalGrowth))
            .Where(c => c.Row.ActualRentTrend == "↓")
            .ToList();
        if (fell.Count > 0)
        {
            var named = fell.FirstOrDefault(c => c.Name == "الرياض");
            if (named.Name is null) named = fell.OrderBy(c => c.Now - c.Before).ThenBy(c => c.Name, StringComparer.Ordinal).First();
            model.SummaryLines.Add($"تباطؤ الإيجار الفعلي للمسكن CPI في {CountIn(fell.Count)}، وانخفض في {named.Name} إلى {named.Row.ActualRentText}.");
        }

        BuildInterventionGroups(model, cities);
        model.Impact = (context?.ImpactCities ?? []).Select(c => new ImpactCityView { CityName = c.City, Points = ImpactPoints(c) }).ToList();

        // The same section title as the city reports (requirement 2.7).
        model.MonitoringTitle = "ما يجب مراقبته كمتابعة دورية";
        model.MonitoringBullets = new List<string>
        {
            $"مسار العبء السكني لذوي الدخل المحدود في المدن التي يتجاوز فيها الحد المرجعي للمراقبة ({Pct(options.BurdenReferenceThreshold)}).",
            "المدن التي يواصل فيها الإيجار الفعلي للمسكن CPI الارتفاع رغم هدوء معدل النمو الإيجاري المركب لأسعار الإيجار.",
            "تحليل انخفاض العائد الإيجاري بالربط بين حركة الإيجار وأسعار البيع.",
            "تطور الشواغر الإيجارية واتجاه المعروض وطنيًا.",
        };

        return model;
    }

    // The increase is read as the platform's impact panel does (the quarter before activation against the latest
    // quarter); the actual rent from the month before the controls' decision month to the latest month.
    private const string ActivationQuarter = "2025-Q3";
    private const string DecisionMonth = "2025-09";

    // The compound rental growth before the controls (the quarter ahead of activation) has no backend series yet;
    // it is the same figure the portal's impact panel shows.
    private const double CagrBeforeActivation = 15.7;

    private static List<string> ImpactPoints(ImpactCitySeries city)
    {
        var points = RentImpactPoints(city);
        if (city.Cagr is not { } cagr || !YearQuarter.TryParse(ActivationQuarter, out var activation) || points.Count < 2)
            return points;

        string Num(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);
        var drop = Math.Round((CagrBeforeActivation - cagr) * 10) / 10;
        var period = string.IsNullOrWhiteSpace(city.CagrPeriod) ? "" : $" ({city.CagrPeriod})";
        points.Insert(0, drop switch
        {
            > 0 => $"انخفض معدل النمو الإيجاري المركب بمقدار {PointsText(drop)}: من {Num(CagrBeforeActivation)}% قبل التفعيل ({activation.Previous().Label}) إلى {Num(cagr)}%{period}.",
            < 0 => $"ارتفع معدل النمو الإيجاري المركب بمقدار {PointsText(-drop)}: من {Num(CagrBeforeActivation)}% قبل التفعيل ({activation.Previous().Label}) إلى {Num(cagr)}%{period}.",
            _ => $"ثبت معدل النمو الإيجاري المركب عند {Num(cagr)}%{period}.",
        });
        return points;
    }

    private static string PointsText(double n)
    {
        var tail = n == Math.Floor(n) ? (int)n % 100 : 0;
        var noun = n == 2 ? "نقطتان مئويتان" : tail is >= 3 and <= 10 ? "نقاط مئوية" : "نقطة مئوية";
        return $"{n.ToString("0.#", CultureInfo.InvariantCulture)} {noun}";
    }

    private static List<string> RentImpactPoints(ImpactCitySeries city)
    {
        const string noPeriod = "لا تتوفر فترة قياس كافية حتى الآن.";
        var byQuarter = new Dictionary<YearQuarter, double>();
        foreach (var p in city.IncreaseQuarterly)
            if (YearQuarter.TryParse(p.Period, out var q)) byQuarter[q] = p.Value;
        if (!YearQuarter.TryParse(ActivationQuarter, out var activation) || byQuarter.Count == 0
            || !byQuarter.TryGetValue(activation.Previous(), out var before)) return new() { noPeriod };
        var last = byQuarter.Keys.Max();
        if (last < activation) return new() { noPeriod };
        var after = byQuarter[last];
        var delta = Math.Round((after - before) * 10) / 10;
        string Num(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);
        string Points(double n) => PointsText(n);

        var points = new List<string>
        {
            delta switch
            {
                < 0 => $"انخفضت الزيادة الإيجارية من {Num(before)}% قبل التفعيل ({activation.Previous().Label}) إلى {Num(after)}% ({last.Label}).",
                > 0 => $"ارتفعت الزيادة الإيجارية من {Num(before)}% قبل التفعيل ({activation.Previous().Label}) إلى {Num(after)}% ({last.Label}).",
                _ => $"ثبتت الزيادة الإيجارية عند {Num(before)}% بين {activation.Previous().Label} و{last.Label}.",
            },
            delta switch
            {
                < 0 => $"لوحظ تباطؤ الزيادة الإيجارية بمقدار {Points(-delta)} خلال الفترة التالية للتفعيل.",
                > 0 => $"لوحظ ارتفاع الزيادة الإيجارية بمقدار {Points(delta)} خلال الفترة التالية للتفعيل.",
                _ => "لم تتغير الزيادة الإيجارية خلال الفترة التالية للتفعيل.",
            },
        };

        // "Both indicators": the actual rent from the month before the decision month to its latest month.
        var rent = city.ActualRentMonthly.OrderBy(p => p.Period, StringComparer.Ordinal).ToList();
        if (YearMonth.TryParse(DecisionMonth, out var decision)
            && rent.FirstOrDefault(p => p.Period == decision.Previous().Id) is { } rentBefore
            && rent.LastOrDefault() is { } rentAfter && string.CompareOrdinal(rentAfter.Period, decision.Id) >= 0
            && YearMonth.TryParse(rentAfter.Period, out var rentLast))
        {
            var rentDelta = Math.Round((rentAfter.Value - rentBefore.Value) * 10) / 10;
            string Trend(double d) => d < 0 ? "انخفاض" : d > 0 ? "ارتفاع" : "ثبات";
            var rentFrom = $"من {Math.Round(rentBefore.Value, 1).ToString("0.0", CultureInfo.InvariantCulture)}% ({decision.Previous().Label}) إلى {Math.Round(rentAfter.Value, 1).ToString("0.0", CultureInfo.InvariantCulture)}% ({rentLast.Label})";
            points.Add(Math.Sign(delta) == Math.Sign(rentDelta) && delta != 0
                ? $"كلا المؤشرين (الزيادة الإيجارية والإيجار الفعلي للمسكن CPI) في {Trend(delta)} منذ قرار التطبيق: الإيجار الفعلي للمسكن CPI {rentFrom}."
                : $"الزيادة الإيجارية في {Trend(delta)} بينما الإيجار الفعلي للمسكن CPI في {Trend(rentDelta)} منذ قرار التطبيق {rentFrom}.");
        }
        return points;
    }

    private static void BuildInterventionGroups(NationalReportModel model, List<CityMarketMetric> cities)
    {
        var defs = new (string Label, string ColorHex, Func<CityMarketMetric, bool> Match)[]
        {
            ("التدخل مفعّل", "#C0392B", c => c.OperationalStatus == "الضوابط مفعلة"),
            ("يتطلب دراسة تفصيلية", "#C67A1E", c => c.OperationalStatus == "يتطلب دراسة تفصيلية"),
            ("مراقبة استباقية", "#2E9B5B", c => c.OperationalStatus == "يتطلب دراسة استباقية"),
            ("لا إجراء", "#1C8C8C", c => c.OperationalStatus == "لا إجراء"),
        };
        var rankOf = model.Rows.Select((r, i) => new { r.CityName, i }).ToDictionary(t => t.CityName, t => t.i);
        foreach (var d in defs)
        {
            var members = cities.Where(d.Match)
                .OrderBy(c => rankOf.TryGetValue(c.Name, out var idx) ? idx : int.MaxValue)
                .Select(c => c.Name).ToList();
            if (members.Count > 0)
                model.InterventionGroups.Add(new InterventionGroup { Label = d.Label, ColorHex = d.ColorHex, Cities = members });
        }
    }

    private static string Pct(double v) => v.ToString("0.#", CultureInfo.InvariantCulture) + "%";

    // The city report's strings (as published), so both reports print the same figure ("+4.0%", not "+4%").
    private static string YieldText(CityMarketMetric c) => c.InvestmentReturnText ?? Pct(c.InvestmentReturn!.Value);
    private static string RentText(CityMarketMetric c) => c.ActualRentalGrowthText ?? Signed(c.ActualRentalGrowth!.Value);
    private static string CagrText(CityMarketMetric c) => c.CagrText ?? Pct(c.Cagr!.Value);
    private static string PctInt(double v) => Math.Round(v).ToString("0", CultureInfo.InvariantCulture) + "%";
    private static string Signed(double v) => (v >= 0 ? "+" : "") + v.ToString("0.#", CultureInfo.InvariantCulture) + "%";

    // Subject position ("مدينتان في المسار…"). Nominative dual.
    private static string Count(int c) => c switch
    {
        0 => "لا توجد مدن",
        1 => "مدينة واحدة",
        2 => "مدينتان",
        >= 3 and <= 10 => $"{c} مدن",
        _ => $"{c} مدينة",
    };

    // After "في" ("في مدينتين"). Genitive dual.
    private static string CountIn(int c) => c switch
    {
        0 => "0 من المدن",
        1 => "مدينة واحدة",
        2 => "مدينتين",
        >= 3 and <= 10 => $"{c} مدن",
        _ => $"{c} مدينة",
    };
}
