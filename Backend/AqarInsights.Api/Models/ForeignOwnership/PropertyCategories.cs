using System.Text.RegularExpressions;

namespace AqarInsights.Api.Models.ForeignOwnership;

// The 6 reporting categories for "نوع العقار"; every raw source type is consolidated into one of them.
public static class PropertyCategories
{
    public const string ResidentialBuilding = "مباني سكنية";
    public const string VacantLand = "أرض شاغرة";
    public const string ResidentialApartment = "شقق سكنية";
    public const string Floor = "دور";
    public const string MixedUse = "مباني متعددة الأغراض";
    public const string Other = "فئات أخرى";

    public static readonly IReadOnlyList<string> All =
        [ResidentialBuilding, VacantLand, ResidentialApartment, Floor, MixedUse, Other];

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    public static string FromRaw(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Other;
        var t = Whitespace.Replace(raw, " ").Trim();

        // Order matters: "مبنى سكني/تجاري" must hit mixed-use before the residential-building rule.
        if (t.Contains("سكني") && t.Contains("تجاري")) return MixedUse;
        if (t.Contains("شقة") || t.Contains("شقه")) return ResidentialApartment;
        if (t.Contains("أرض") || t.Contains("ارض")) return VacantLand;
        if (t == "دور" || t.StartsWith("دور ")) return Floor;
        if (t is "مبنى" or "برج" or "عمارة"
            || t.Contains("مبنى سكني") || t.Contains("مجمع سكني") || t.Contains("عمارة سكنية")
            || t.Contains("فيلا") || t.Contains("فلة") || t.Contains("فله") || t.Contains("قصر"))
            return ResidentialBuilding;
        return Other;
    }
}
