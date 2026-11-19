using System.Globalization;
using System.Text.RegularExpressions;

namespace AqarInsights.Api.Services.RentalControls;

// Shared numeric parsing for Arabic-formatted display strings (handles thousands commas,
// leading '+', and the Unicode minus U+2212 '−'). Centralises what used to be 4 copies.
public static class ArabicNumber
{
    // First signed decimal in the string ("33,157 (-4%)"→33157, "−4%"→-4, "—"/"-"/null→null).
    public static double? FirstNumber(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var normalized = s.Replace(",", "").Replace("−", "-");
        var m = Regex.Match(normalized, @"-?\d+(\.\d+)?");
        return m.Success ? double.Parse(m.Value, CultureInfo.InvariantCulture) : null;
    }

    // Whole-value integer percent ("6%"→6, "−4%"→-4); returns fallback (default -1) when not a plain integer.
    public static int PercentInt(string? s, int fallback = -1)
    {
        var cleaned = (s ?? "").Replace("%", "").Replace("+", "").Replace("−", "-").Trim();
        return int.TryParse(cleaned, out var n) ? n : fallback;
    }
}
