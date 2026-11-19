namespace AqarInsights.Api.Services.RentalControls.Reports;

internal static class PdfBidi
{
    // A figure such as "+15.2%" or "-1%" inside right-to-left PDF text keeps its sign on the left only when set as a left-to-right run.
    public static string Ltr(string? text)
        => string.IsNullOrEmpty(text) || text.Any(c => c is >= '\u0600' and <= '\u06FF') ? text ?? "" : "\u202A" + text + "\u202C";
}
