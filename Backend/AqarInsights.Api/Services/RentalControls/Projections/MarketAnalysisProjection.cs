using System.Globalization;
using System.Text.RegularExpressions;
using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Raw;

namespace AqarInsights.Api.Services.RentalControls.Projections;

public static class MarketAnalysisProjection
{
    public static MarketAnalysisData Build(OverviewData overview, IReadOnlyDictionary<string, CityRawData> cityRaw, MarketAnalysisOptions? options = null)
    {
        var cities = overview.Cities.Select(c =>
        {
            cityRaw.TryGetValue(c.Id, out var raw);
            return new CityMarketMetric
            {
                Id = c.Id,
                Name = c.Name,
                OperationalStatus = c.OperationalStatus,
                Classification = c.Classification,
                Cagr = ParseNum(c.Cagr),
                RentalIncrease = ParseNum(c.ActualRentalGrowth), // overview field holds الزيادة الإيجارية
                HousingBurden = ParseNum(c.HousingBurden),
                InvestmentReturn = ParseNum(c.InvestmentReturn),
                InvestmentReturnExact = raw?.RoiSeries.LastOrDefault(p => p.Values.ContainsKey("العائد"))?.Values["العائد"],
                RentToIncome = ParseNum(c.RentToIncome),
                VacancyRate = ParseNum(c.VacancyRate),
                RentalTransactionCount = LatestTotal(raw?.RentalsSeries) is { } rt ? Math.Round(rt / 1000.0, 1) : null,
                SalesTransactionCount = LatestTotal(raw?.SalesSeries) is { } st ? Math.Round(st / 1000.0, 1) : null,
                AvgRentalTransactionValue = ParseMovement(raw, "متوسط قيمة الصفقة الإيجارية (ريال/وحدة)"),
                AvgSalesTransactionValue = ParseMovement(raw, "متوسط قيمة الصفقة البيعية (ريال)"),
                ActualRentalGrowth = ParseMovement(raw, "الإيجار الفعلي للمسكن (CPI)"),
                ActualRentalGrowthText = MovementText(raw, "الإيجار الفعلي للمسكن (CPI)"),
                InvestmentReturnText = ArabicNumber.FirstNumber(c.InvestmentReturn) is null ? null : c.InvestmentReturn,
                CagrText = ArabicNumber.FirstNumber(c.Cagr) is null ? null : c.Cagr,
                RentalIncreaseText = ArabicNumber.FirstNumber(c.ActualRentalGrowth) is null ? null : c.ActualRentalGrowth,
                RentToIncomeText = ArabicNumber.FirstNumber(c.RentToIncome) is null ? null : c.RentToIncome,
                GeneralInflationCpi = ParseMovement(raw, "التضخم العام (CPI)"),
                ActualRentCpi = LatestIndex(raw?.RentIndexSeries),
                ApartmentRentIndex = LatestTypeIndex(raw?.RentIndexByTypeSeries, "شقة"),
                FloorRentIndex = LatestTypeIndex(raw?.RentIndexByTypeSeries, "دور"),
                VillaRentIndex = LatestTypeIndex(raw?.RentIndexByTypeSeries, "فيلا"),
            };
        }).ToList();

        var indicators = Indicators(options);
        return new MarketAnalysisData
        {
            LastQuarter = ReportingPeriod.Latest(cityRaw.Values),
            Cities = cities,
            Indicators = indicators,
            Recommendations = Recommendations(cities, indicators),
        };
    }

    // Parse leading signed decimal from a formatted string ("9.7%"→9.7, "+9%"→9, "—"→null).
    private static double? ParseNum(string? s) => ArabicNumber.FirstNumber(s);

    // First numeric group of a movement Change cell ("33,157 (-4%)"→33157, "+5.9%"→5.9).
    private static double? ParseMovement(CityRawData? raw, string indicator)
    {
        var row = raw?.MovementRows.FirstOrDefault(r => r.Indicator.Trim() == indicator);
        return ParseNum(row?.Change);
    }

    // The movement cell as published ("+4.0%"), bidi marks removed; null when it holds no number.
    private static string? MovementText(CityRawData? raw, string indicator)
    {
        var change = raw?.MovementRows.FirstOrDefault(r => r.Indicator.Trim() == indicator)?.Change;
        if (ParseNum(change) is null) return null;
        return change!.Replace("\u200e", "").Replace("\u200f", "").Replace("−", "-").Trim();
    }

    private static double? LatestTotal(List<RawSeriesPoint>? series)
    {
        var last = series?.LastOrDefault(p => p.Values.ContainsKey("الإجمالي"));
        return last?.Values["الإجمالي"];
    }

    private static double? LatestIndex(List<RawSeriesPoint>? series)
    {
        var last = series?.LastOrDefault(p => p.Values.ContainsKey("الرقم القياسي"));
        return last is null ? null : Math.Round(last.Values["الرقم القياسي"], 1);
    }

    // Per-type Fisher index (~1.0 base) → conventional index points (×100).
    private static double? LatestTypeIndex(List<RawSeriesPoint>? series, string type)
    {
        var last = series?.LastOrDefault(p => p.Values.ContainsKey(type));
        return last is null ? null : Math.Round(last.Values[type] * 100, 1);
    }

    private static List<MarketIndicatorDef> Indicators(MarketAnalysisOptions? options)
    {
        var defs = new List<MarketIndicatorDef>
        {
            new() { Id = "rentalTransactionCount", Label = "الصفقات الإيجارية", Unit = " ألف", IsPercentage = false, HigherIs = "positive", Threshold = 5, ThresholdLabel = "حد الصفقات 5 آلاف" },
            new() { Id = "avgRentalTransactionValue", Label = "متوسط قيمة الصفقة الإيجارية (ريال/وحدة)", Unit = " ريال", IsPercentage = false, HigherIs = "positive", Threshold = 20000, ThresholdLabel = "متوسط القيمة 20 ألف" },
            new() { Id = "salesTransactionCount", Label = "الصفقات البيعية", Unit = " ألف", IsPercentage = false, HigherIs = "positive", Threshold = 3, ThresholdLabel = "حد الصفقات 3 آلاف" },
            new() { Id = "avgSalesTransactionValue", Label = "متوسط قيمة الصفقة البيعية (ريال)", Unit = " ريال", IsPercentage = false, HigherIs = "neutral", Threshold = 800000, ThresholdLabel = "متوسط القيمة 800 ألف" },
            new() { Id = "actualRentCpi", Label = "الإيجار الفعلي للمسكن (CPI)", Unit = "", IsPercentage = false, HigherIs = "negative", Threshold = 110, ThresholdLabel = "حد CPI 110" },
            new() { Id = "generalInflationCpi", Label = "التضخم العام (CPI)", Unit = "%", IsPercentage = true, HigherIs = "negative", Threshold = 3, ThresholdLabel = "حد التضخم 3%" },
            new() { Id = "apartmentRentIndex", Label = "الرقم القياسي لإيجار الشقة", Unit = "", IsPercentage = false, HigherIs = "negative", Threshold = 115, ThresholdLabel = "حد الشقة 115" },
            new() { Id = "floorRentIndex", Label = "الرقم القياسي لإيجار الدور", Unit = "", IsPercentage = false, HigherIs = "negative", Threshold = 112, ThresholdLabel = "حد الدور 112" },
            new() { Id = "villaRentIndex", Label = "الرقم القياسي لإيجار الفيلا", Unit = "", IsPercentage = false, HigherIs = "negative", Threshold = 118, ThresholdLabel = "حد الفيلا 118" },
        };

        if (options?.Thresholds is { } overrides)
        {
            foreach (var def in defs)
            {
                if (!overrides.TryGetValue(def.Id, out var cfg)) continue;
                def.Threshold = cfg.Value;
                if (!string.IsNullOrWhiteSpace(cfg.Label)) def.ThresholdLabel = cfg.Label;
            }
        }

        return defs;
    }

    // Data-driven guidance: card templates are fixed, affected cities are derived from the matrix + thresholds.
    private static List<MarketRecommendation> Recommendations(List<CityMarketMetric> cities, List<MarketIndicatorDef> indicators)
    {
        double Th(string id) => indicators.FirstOrDefault(i => i.Id == id)?.Threshold ?? 0;
        string Names(IEnumerable<CityMarketMetric> cs) => string.Join("، ", cs.Take(3).Select(c => c.Name));

        // 1. High rental activity AND high value → intensify monitoring.
        var rtT = Th("rentalTransactionCount");
        var arvT = Th("avgRentalTransactionValue");
        var hot = cities.Where(c => (c.RentalTransactionCount ?? 0) >= rtT && (c.AvgRentalTransactionValue ?? 0) >= arvT)
            .OrderByDescending(c => c.RentalTransactionCount ?? 0).ToList();
        if (hot.Count == 0)
            hot = cities.Where(c => c.RentalTransactionCount != null).OrderByDescending(c => c.RentalTransactionCount!.Value).ToList();

        // 2. Elevated rent index vs general inflation → housing pressure.
        var pressure = cities.Where(c => (c.ActualRentCpi ?? 0) >= Th("actualRentCpi"))
            .OrderByDescending(c => (c.ActualRentCpi ?? 0) - (c.GeneralInflationCpi ?? 0)).ToList();
        if (pressure.Count == 0)
            pressure = cities.Where(c => c.ActualRentCpi != null).OrderByDescending(c => c.ActualRentCpi!.Value).ToList();

        // 3. High sale value but low volume → study the relationship.
        var stT = Th("salesTransactionCount");
        var asvT = Th("avgSalesTransactionValue");
        var diverge = cities.Where(c => (c.AvgSalesTransactionValue ?? 0) >= asvT && (c.SalesTransactionCount ?? 0) < stT)
            .OrderByDescending(c => c.AvgSalesTransactionValue ?? 0).ToList();
        if (diverge.Count == 0)
            diverge = cities.Where(c => c.AvgSalesTransactionValue != null).OrderByDescending(c => c.AvgSalesTransactionValue!.Value).ToList();

        return new List<MarketRecommendation>
        {
            new() { Priority = 1, Title = "تكثيف مراقبة المدن ذات النشاط الإيجاري المرتفع", AffectedCities = Names(hot), Reason = "ارتفاع عدد الصفقات الإيجارية مع مؤشرات أسعار مرتفعة يستدعي متابعة مستمرة.", Cta = "عرض تحليل المدن", Action = "analyze" },
            new() { Priority = 2, Title = "متابعة المدن ذات الفجوة بين الإيجار والتضخم", AffectedCities = Names(pressure), Reason = "ارتفاع الرقم القياسي للإيجار الفعلي مقارنة بالتضخم العام يشير إلى ضغط سكني.", Cta = "عرض المؤشرات", Action = "indicators" },
            new() { Priority = 3, Title = "دراسة العلاقة بين الصفقات البيعية والقيمة السوقية", AffectedCities = Names(diverge), Reason = "تباين كبير بين عدد الصفقات البيعية ومتوسط قيمة الصفقة يستدعي تحليلاً أعمق.", Cta = "مقارنة المدن", Action = "compare" },
        };
    }
}
