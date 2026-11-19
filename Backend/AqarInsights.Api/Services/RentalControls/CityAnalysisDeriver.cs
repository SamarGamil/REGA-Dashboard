using AqarInsights.Api.Models.RentalControls;

namespace AqarInsights.Api.Services.RentalControls;

public static class CityAnalysisDeriver
{
    private static readonly string[] Stages =
        { "لا إجراء", "يتطلب دراسة استباقية", "يتطلب دراسة تفصيلية", "الضوابط مفعلة", "قياس الأثر", "تقييم الإيقاف" };

    public static List<KpiItem> Kpis(CityData c, KpiThresholdsOptions? thresholds = null)
    {
        var t = thresholds ?? new KpiThresholdsOptions();
        return new()
        {
            new() { Label = "الزيادة الإيجارية", Value = c.ActualRentalGrowth, Status = GrowthStatus(c.ActualRentalGrowth, t), Trend = "stable" },
            new() { Label = "الشواغر الإيجارية", Value = c.VacancyRate, Status = VacancyStatus(c.VacancyRate, t), Trend = "stable" },
            new() { Label = "العائد الإيجاري", Value = c.InvestmentReturn, Status = YieldStatus(c.InvestmentReturn, t), Trend = "stable" },
            new() { Label = "نسبة الإيجار إلى الدخل", Value = c.RentToIncome, Status = RentToIncomeStatus(c.RentToIncome, t), Trend = "stable" },
            new() { Label = "العبء السكني للدخل المحدود", Value = c.HousingBurden, Status = BurdenStatus(c.HousingBurden, t), Trend = "stable" },
        };
    }

    // KPI status (سلبي/محايد/إيجابي) derived from the metric vs its thresholds. Direction differs per metric.
    private static string GrowthStatus(string s, KpiThresholdsOptions t) => ParsePercent(s) is not { } v ? "محايد" : v >= t.GrowthHigh ? "سلبي" : v >= t.GrowthMed ? "محايد" : "إيجابي";
    private static string VacancyStatus(string s, KpiThresholdsOptions t) => ParsePercent(s) is not { } v ? "محايد" : v < t.VacancyTight ? "سلبي" : v <= t.VacancyMed ? "محايد" : "إيجابي";
    private static string YieldStatus(string s, KpiThresholdsOptions t) => ParsePercent(s) is not { } v ? "محايد" : v < t.YieldLow ? "سلبي" : v < t.YieldMin ? "محايد" : "إيجابي";
    private static string RentToIncomeStatus(string s, KpiThresholdsOptions t) => ParsePercent(s) is not { } v ? "محايد" : v >= t.RentToIncomeHigh ? "سلبي" : v >= t.RentToIncomeMed ? "محايد" : "إيجابي";
    private static string BurdenStatus(string s, KpiThresholdsOptions t) => ParsePercent(s) is not { } v ? "محايد" : v >= t.BurdenHigh ? "سلبي" : v >= t.BurdenMed ? "محايد" : "إيجابي";

    public static List<CityJourneyStage> Journey(string status)
    {
        var idx = System.Array.IndexOf(Stages, status);
        if (idx < 0) idx = 0;
        return Stages.Select((label, i) => new CityJourneyStage
        {
            Label = label, IsCurrent = i == idx, IsCompleted = i < idx,
        }).ToList();
    }

    public static StatusBadge Badge(CityData c) => Badge(c.ClassificationLabel);

    // The city page's «حالة السوق» badge, keyed by the CAGR classification.
    public static StatusBadge Badge(string classificationLabel) => classificationLabel switch
    {
        "accelerated" => new() { Label = "تحت ضغط مرتفع", Color = "#DC2626", BgColor = "#FEF2F2" },
        "elevated" => new() { Label = "نمو مرتفع", Color = "#D97706", BgColor = "#FFFBEB" },
        _ => new() { Label = "ضمن الحدود", Color = "#0F766E", BgColor = "#ECFDF5" },
    };

    public static List<ExecRecommendation> Recommendations(CityData c) => string.IsNullOrWhiteSpace(c.Recommendation)
        ? new()
        : new() { new() { Priority = 1, PriorityLabel = RecommendationPriorityLabel(c), Recommendation = c.OperationalStatus, Description = c.Recommendation } };

    private static string RecommendationPriorityLabel(CityData c) => c.OperationalStatus switch
    {
        "الضوابط مفعلة" or "يتطلب دراسة تفصيلية" => "عالية",
        "يتطلب دراسة استباقية" => "متوسطة",
        _ => "منخفضة",
    };

    public static List<MonitorItem> WhatToMonitor(CityData c, KpiThresholdsOptions? thresholds = null)
    {
        var t = thresholds ?? new KpiThresholdsOptions();
        return new()
        {
            new() { Item = "مسار نمو الإيجارات", CurrentStatus = c.ActualRentalGrowth, Trend = "stable", Priority = GrowthPriority(c.ActualRentalGrowth, t) },
            new() { Item = "معدل الشواغر", CurrentStatus = c.VacancyRate, Trend = "stable", Priority = VacancyPriority(c.VacancyRate, t) },
            new() { Item = "العبء السكني", CurrentStatus = c.HousingBurden, Trend = "stable", Priority = BurdenPriority(c.HousingBurden, t) },
            new() { Item = "العائد الإيجاري", CurrentStatus = c.InvestmentReturn, Trend = "stable", Priority = YieldPriority(c.InvestmentReturn, t) },
        };
    }

    private static string GrowthPriority(string s, KpiThresholdsOptions t) => ParsePercent(s) is not { } v ? "—" : v >= t.GrowthHigh ? "عالي" : v >= t.GrowthMed ? "متوسط" : "منخفض";
    private static string VacancyPriority(string s, KpiThresholdsOptions t) => ParsePercent(s) is not { } v ? "—" : v < t.VacancyTight ? "عالي" : v <= t.VacancyMed ? "متوسط" : "منخفض";
    private static string BurdenPriority(string s, KpiThresholdsOptions t) => ParsePercent(s) is not { } v ? "—" : v >= t.BurdenHigh ? "عالي" : v >= t.BurdenMed ? "متوسط" : "منخفض";
    private static string YieldPriority(string s, KpiThresholdsOptions t) => ParsePercent(s) is not { } v ? "—" : v < t.YieldLow ? "عالي" : v < t.YieldMin ? "متوسط" : "منخفض";

    private static double? ParsePercent(string s) => ArabicNumber.FirstNumber(s);
}
