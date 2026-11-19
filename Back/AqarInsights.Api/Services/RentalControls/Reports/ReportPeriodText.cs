namespace AqarInsights.Api.Services.RentalControls.Reports;

// The explicit period statement printed in every report header and stored in the file metadata.
public static class ReportPeriodText
{
    public static string Line(string periodLabel, bool isMonthly)
        => $"فترة التقرير: {periodLabel} ({(isMonthly ? "تقرير شهري" : "تقرير ربع سنوي")})";

    // The period as shown beside a value: "سبتمبر 2026" → "سبتمبر", "الربع الثالث 2026" → "الربع الثالث".
    public static string Word(string? periodLabel)
        => System.Text.RegularExpressions.Regex.Replace(periodLabel ?? "", @"\s*\d{4}\s*$", "").Trim();
}
