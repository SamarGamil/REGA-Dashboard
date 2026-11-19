using System.Globalization;
using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Utilities;

namespace AqarInsights.Api.Services.RentalControls;

public static class RentalControlsMappings
{
    private static readonly Dictionary<string, string> CityIds = new()
    {
        [ArabicNormalizer.Normalize("الرياض")] = "riyadh",
        [ArabicNormalizer.Normalize("جدة")] = "jeddah",
        [ArabicNormalizer.Normalize("مكة المكرمة")] = "makkah",
        [ArabicNormalizer.Normalize("المدينة المنورة")] = "madinah",
        [ArabicNormalizer.Normalize("الدمام")] = "dammam",
        [ArabicNormalizer.Normalize("بريدة")] = "buraidah",
        [ArabicNormalizer.Normalize("تبوك")] = "tabuk",
        [ArabicNormalizer.Normalize("أبها")] = "abha",
        [ArabicNormalizer.Normalize("حائل")] = "hail",
        [ArabicNormalizer.Normalize("نجران")] = "najran",
        [ArabicNormalizer.Normalize("جازان")] = "jazan",
        [ArabicNormalizer.Normalize("عرعر")] = "arar",
        [ArabicNormalizer.Normalize("سكاكا")] = "sakaka",
        [ArabicNormalizer.Normalize("الباحة")] = "bahah",
    };

    public static string? CityId(string arabicName)
        => CityIds.TryGetValue(ArabicNormalizer.Normalize(arabicName), out var id) ? id : null;

    public static (string classification, string label) Classification(string masar) => masar.Trim() switch
    {
        "متسارع" => ("نمو متسارع", "accelerated"),
        "مرتفع" => ("نمو مرتفع", "elevated"),
        _ => ("نمو مستدام", "sustainable"),
    };

    public static string OperationalStatus(string assessmentLine1)
    {
        if (assessmentLine1.Contains("مفعلة")) return "الضوابط مفعلة";
        if (assessmentLine1.Contains("تفصيلية")) return "يتطلب دراسة تفصيلية";
        if (assessmentLine1.Contains("استباقية")) return "يتطلب دراسة استباقية";
        return "لا إجراء";
    }

    public static string Percent(object? cellValue, bool plusSign = false, int decimals = 0)
    {
        if (cellValue is null) return "—";
        var s = cellValue.ToString()?.Trim() ?? "";
        if (s.Length == 0 || s == "—") return "—";
        if (!double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) return "—";
        var pct = Math.Round(d * 100, decimals);
        if (pct == 0) pct = 0; // drop negative zero so it prints "0%", not "-0%"
        var text = pct.ToString(decimals > 0 ? $"F{decimals}" : "F0", CultureInfo.InvariantCulture) + "%";
        return plusSign && pct > 0 ? "+" + text : text;
    }

    public static IReadOnlyList<ClassificationLegendItem> Legend { get; } = new List<ClassificationLegendItem>
    {
        new() { Key = "sustainable", Label = "نمو مستدام", Range = "≤6%", ColorClass = "bg-emerald-100/70" },
        new() { Key = "elevated", Label = "نمو مرتفع", Range = "7%-9%", ColorClass = "bg-amber-100/70" },
        new() { Key = "accelerated", Label = "نمو متسارع", Range = "≥10%", ColorClass = "bg-rose-100/70" },
    };

    private static readonly Dictionary<string, string> FileTokens = new()
    {
        ["riyadh"] = "رياض", ["jeddah"] = "جدة", ["makkah"] = "مكة", ["madinah"] = "المدينة",
        ["dammam"] = "الدمام", ["buraidah"] = "بريدة", ["tabuk"] = "تبوك", ["abha"] = "أبها",
        ["hail"] = "حائل", ["najran"] = "نجران", ["jazan"] = "جازان", ["arar"] = "عرعر",
        ["sakaka"] = "سكاكا", ["bahah"] = "الباحة",
    };

    public static string? CityFileToken(string cityId) => FileTokens.TryGetValue(cityId, out var t) ? t : null;

    public static string ClassificationColor(string label) => label switch
    {
        "accelerated" => "#DC2626",
        "elevated" => "#D97706",
        _ => "#0F766E",
    };
}
