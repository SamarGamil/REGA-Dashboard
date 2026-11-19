using System.Text.RegularExpressions;
using AqarInsights.Api.Services.RentalControls.Snapshots;

namespace AqarInsights.Api.Services.RentalControls.Projections;

public static class MonthlyStatements
{
    private static readonly string[] MonthNames =
        { "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو", "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر" };

    // Statements built on monthly values: the actual-rent CPI (any "فعلي" form: "الإيجار الفعلي", "فعلي +6.8%",
    // "فجوة فعلية", "متسارعة فعليًا", "الزخم الفعلي للأسعار"), general inflation (but not "جيب تضخم" — a price pocket in
    // the quarterly rent index), media impressions, or a named month. Recommended actions such as "رصد إعلامي" are not
    // monthly values and stay.
    private static readonly Regex MonthlyStatement = new(
        @"CPI|التضخم|تضخم\s*\(?[+-]?\d|وتضخم|فعلي|الانطباع|المحتوى المتداول|المزاج|شهري|"
        + string.Join("|", MonthNames));

    // A month named with its year ("سبتمبر 2026").
    private static readonly Regex NamedMonth = new($@"({string.Join("|", MonthNames)})\s+(\d{{4}})");

    public static bool IsMonthly(string text) => MonthlyStatement.IsMatch(text);

    /// <summary>True when the text names a month (with its year) later than <paramref name="month"/>.</summary>
    public static bool NamesMonthAfter(string text, YearMonth month) => NamedMonth.Matches(text).Any(m =>
        new YearMonth(int.Parse(m.Groups[2].Value), Array.IndexOf(MonthNames, m.Groups[1].Value) + 1) > month);
}
