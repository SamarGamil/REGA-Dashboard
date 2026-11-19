using System.Globalization;
using System.Text.RegularExpressions;
using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Services.RentalControls.Snapshots;

namespace AqarInsights.Api.Services.RentalControls.Projections;

/// <summary>
/// The actual-rent CPI sentence rebuilt for a month whose written text was made for another month: the workbook's own
/// sentence quotes its month's figure, so the view states the selected month's figure in the same fixed form
/// ("الإيجار الفعلي (CPI) يرتفع 7.3% سنويا على مستوى المدينة (أبريل 2026).").
/// </summary>
public static class MonthlyCpiStatement
{
    private const string RentCpiRow = "الإيجار الفعلي للمسكن (CPI)";

    // The city summary's fixed CPI bullet ("الإيجار الفعلي (CPI) يرتفع 5.9% سنويا على مستوى المدينة").
    private static readonly Regex SummaryBullet = new(@"الإيجار الفعلي\s*\(CPI\)\s*(يرتفع|يتراجع|يستقر)", RegexOptions.CultureInvariant);

    // A report sentence built on the actual-rent CPI, in any wording ("الإيجار الفعلي (+10.2% سنويًا) يرتفع …",
    // "إيجار فعلي (-2.7%) …"): the same "فعلي" stem as MonthlyStatements; a general-inflation "(CPI)" sentence is not one.
    private static readonly Regex ActualRent = new("فعلي", RegexOptions.CultureInvariant);

    public static bool IsSummaryBullet(string text) => SummaryBullet.IsMatch(text);

    public static bool IsActualRentSentence(string text) => ActualRent.IsMatch(text);

    /// <summary>
    /// The rebuilt sentence for a month view whose text was written for another month; null otherwise or when the month
    /// has no actual-rent CPI figure. <paramref name="city"/> is the masked city document (its CPI row is the month's own
    /// figure or "—").
    /// </summary>
    public static string? ForView(CityAnalysisData city, PeriodScope scope)
    {
        if (scope.Month is not { } month || month == scope.TextMonth) return null;
        var row = city.MarketMovementTable.FirstOrDefault(r => r.Indicator == RentCpiRow);
        if (ArabicNumber.FirstNumber(row?.Change) is not { } value) return null;
        var rounded = Math.Round(value, 1, MidpointRounding.AwayFromZero);
        return rounded switch
        {
            > 0 => $"الإيجار الفعلي (CPI) يرتفع {Amount(rounded)}% سنويا على مستوى المدينة ({month.Label}).",
            < 0 => $"الإيجار الفعلي (CPI) يتراجع {Amount(-rounded)}% سنويا على مستوى المدينة ({month.Label}).",
            _ => $"الإيجار الفعلي (CPI) يستقر سنويا على مستوى المدينة ({month.Label}).",
        };
    }

    private static string Amount(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);
}
