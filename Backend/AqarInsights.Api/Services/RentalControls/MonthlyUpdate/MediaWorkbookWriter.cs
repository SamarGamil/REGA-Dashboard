using ClosedXML.Excel;
using AqarInsights.Api.Services.RentalControls.Snapshots;

namespace AqarInsights.Api.Services.RentalControls.MonthlyUpdate;

// Replaces one month in الرصد الإعلامي.xlsx (sheets المدن / الموضوعات / الانطباعات, read by MediaMonitoringReader).
public static class MediaWorkbookWriter
{
    public static void Write(IXLWorkbook wb, YearMonth month, IReadOnlyList<MediaCityMonth> cities, MonthlyUpdateReport report)
    {
        Replace(wb, "المدن", month, cities.Select(c => new Dictionary<string, XLCellValue>
        {
            ["cityId"] = c.CityId, ["available"] = true, ["overallSentiment"] = c.OverallSentiment,
            ["positivePct"] = c.PositivePct, ["neutralPct"] = c.NeutralPct, ["negativePct"] = c.NegativePct,
            ["discussionSharePct"] = c.SharePct,
        }));
        Replace(wb, "الموضوعات", month, cities.SelectMany(c => c.Topics.Select(t =>
            new Dictionary<string, XLCellValue> { ["cityId"] = c.CityId, ["topic"] = t })));
        Replace(wb, "الانطباعات", month, cities.SelectMany(c => c.Impressions.Select(i =>
            new Dictionary<string, XLCellValue> { ["cityId"] = c.CityId, ["type"] = i.Type, ["text"] = i.Text })));
        report.Changes.Add($"media {month.Id}: {cities.Count} cities — " + string.Join(", ", cities.Select(c =>
            $"{c.CityId} {c.PositivePct}/{c.NeutralPct}/{c.NegativePct} share {c.SharePct}% topics {c.Topics.Count} impressions {c.Impressions.Count}")));
    }

    // Deletes the month's rows, then appends the new ones under the sheet's own header order.
    private static void Replace(IXLWorkbook wb, string sheetName, YearMonth month, IEnumerable<Dictionary<string, XLCellValue>> rows)
    {
        var sheet = wb.Worksheet(sheetName);
        var header = sheet.Row(1).CellsUsed().ToDictionary(c => c.GetString().Trim(), c => c.Address.ColumnNumber, StringComparer.Ordinal);
        var periodCol = header["period"];
        foreach (var row in sheet.RowsUsed().Skip(1).Where(r => r.Cell(periodCol).GetString().Trim() == month.Id).Reverse().ToList())
            row.Delete();
        var next = (sheet.LastRowUsed()?.RowNumber() ?? 1) + 1;
        foreach (var values in rows)
        {
            sheet.Cell(next, periodCol).Value = month.Id;
            foreach (var (name, value) in values) sheet.Cell(next, header[name]).Value = value;
            next++;
        }
    }
}
