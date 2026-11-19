namespace AqarInsights.Api.Services;

/// <summary>Formats calendar quarters into the Arabic label used across the dashboards (e.g. "الربع الثاني 2026").</summary>
public static class QuarterLabel
{
    private static readonly string[] Names = { "الأول", "الثاني", "الثالث", "الرابع" };

    private static readonly string[] MonthNames =
    {
        "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
        "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"
    };

    public static string Format(int year, int quarter) => $"الربع {Names[Math.Clamp(quarter, 1, 4) - 1]} {year}";

    public static string FromDate(DateTime date) => Format(date.Year, (date.Month - 1) / 3 + 1);

    // Latest data month + year, e.g. "يونيو 2026".
    public static string MonthLabel(DateTime date) => $"{MonthNames[date.Month - 1]} {date.Year}";
}
