using System.Text.RegularExpressions;

namespace AqarInsights.Api.Utilities;

// Canonicalises buyer-nationality strings so variant spellings collapse to one label.
// The source workbook records nationality as free text, so the same country/group appears
// under several spellings (e.g. مصر/مصري, كندا/كندي) plus junk placeholders (لا يوجد).
public static partial class NationalityNormalizer
{
    public const string Unspecified = "غير محدد";

    // Explicit merges the general Arabic normaliser can't infer (semantic, not orthographic).
    // Keys are already ArabicNormalizer-normalised so lookups are stable.
    private static readonly Dictionary<string, string> CanonicalMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["مصري"] = "مصر",
        ["كندي"] = "كندا",
        ["الصين الشعبية"] = "الصين",
        ["فلسطيني-وثيقةمصرية"] = "فلسطيني بوثيقة مصرية",
        ["روسيا الاتحادية"] = "روسيا",
        ["ميانمار/جوازبنجلا دش"] = "ميانمار/جواز بنجلاديش",
        ["لا يوجد"] = Unspecified,
        ["حصر ن 1"] = Unspecified,
        // RER Company rows carry the partners' category instead of a country.
        ["Mixed"] = "شركة مختلطة",
        ["Foreign"] = "شركة اجنبية",
        ["Gulf"] = "شركة خليجية",
    };

    public static string Canonicalize(string? raw)
    {
        var normalized = ArabicNormalizer.Normalize(raw);
        if (string.IsNullOrWhiteSpace(normalized)) return Unspecified;

        // All displaced-tribes spellings (قبائل نازحة/الكويت, قبايل نازحه الحليفه, مقيم/نازح…) → one label.
        if (normalized.Contains("نازح")) return "قبائل نازحة";

        // "فلسطيني بوثيقةلبناني" → "فلسطيني بوثيقة لبناني"
        normalized = MissingSpaceAfterDocument().Replace(normalized, "بوثيقة ");

        return CanonicalMap.TryGetValue(normalized, out var canonical) ? canonical : normalized;
    }

    [GeneratedRegex(@"بوثيقة(?=\S)")]
    private static partial Regex MissingSpaceAfterDocument();
}
