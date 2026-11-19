using ClosedXML.Excel;
using AqarInsights.Api.Models.RentalControls.Raw;

namespace AqarInsights.Api.Services.RentalControls.Reading;

// Reads the media-monitoring workbook (الرصد الإعلامي.xlsx) into per-city monthly blocks.
// Three sheets: المدن (one row per city per period), الموضوعات (topics), الانطباعات (impression bullets).
// Each sheet carries an optional `period` (YYYY-MM) column; when absent, all rows are treated as a
// single unnamed month (back-compat with the pre-history single-snapshot workbook).
public static class MediaMonitoringReader
{
    public static Dictionary<string, List<RawMediaMonitoring>> Read(IXLWorkbook wb)
    {
        // cityId -> (period -> media)
        var byCity = new Dictionary<string, Dictionary<string, RawMediaMonitoring>>(StringComparer.OrdinalIgnoreCase);

        // --- Sheet المدن: per-city per-period summary ---
        var citiesSheet = SheetReader.Sheet(wb, "المدن");
        var ch = SheetReader.ForHeader(citiesSheet, 1);
        var periodCol = ch.TryCol("period");
        var cityIdCol = ch.Col("cityId");
        var availableCol = ch.Col("available");
        var sentimentCol = ch.Col("overallSentiment");
        var posCol = ch.Col("positivePct");
        var neuCol = ch.Col("neutralPct");
        var negCol = ch.Col("negativePct");
        var shareCol = ch.TryCol("discussionSharePct");

        var lastRow = citiesSheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= lastRow; r++)
        {
            var cityId = citiesSheet.Cell(r, cityIdCol).GetString().Trim();
            if (cityId.Length == 0) continue;
            var period = periodCol is int pc ? citiesSheet.Cell(r, pc).GetString().Trim() : "";

            var media = new RawMediaMonitoring
            {
                Period = period,
                Available = ReadBool(citiesSheet.Cell(r, availableCol)),
                OverallSentiment = citiesSheet.Cell(r, sentimentCol).GetString().Trim(),
                PositivePct = ReadInt(citiesSheet.Cell(r, posCol)),
                NeutralPct = ReadInt(citiesSheet.Cell(r, neuCol)),
                NegativePct = ReadInt(citiesSheet.Cell(r, negCol)),
                DiscussionSharePct = shareCol is int sc ? ReadDouble(citiesSheet.Cell(r, sc)) : 0,
            };
            if (!byCity.TryGetValue(cityId, out var periods))
                byCity[cityId] = periods = new Dictionary<string, RawMediaMonitoring>(StringComparer.Ordinal);
            periods[period] = media;
        }

        // --- Sheet الموضوعات: top topics ---
        Readlist(wb, "الموضوعات", "topic", byCity, (media, text) => media.Topics.Add(text));

        // --- Sheet الانطباعات: typed impression bullets ---
        var impSheet = SheetReader.Sheet(wb, "الانطباعات");
        var ih = SheetReader.ForHeader(impSheet, 1);
        var impPeriodCol = ih.TryCol("period");
        var impCityCol = ih.Col("cityId");
        var typeCol = ih.Col("type");
        var textCol = ih.Col("text");
        var impLast = impSheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= impLast; r++)
        {
            var cityId = impSheet.Cell(r, impCityCol).GetString().Trim();
            var text = impSheet.Cell(r, textCol).GetString().Trim();
            if (cityId.Length == 0 || text.Length == 0) continue;
            var period = impPeriodCol is int ipc ? impSheet.Cell(r, ipc).GetString().Trim() : "";
            if (!TryGetPeriod(byCity, cityId, period, out var media)) continue;

            switch (impSheet.Cell(r, typeCol).GetString().Trim().ToLowerInvariant())
            {
                case "positive": media.PositiveImpressions.Add(text); break;
                case "neutral": media.NeutralImpressions.Add(text); break;
                case "negative": media.NegativeImpressions.Add(text); break;
            }
        }

        return byCity.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Values.OrderBy(m => m.Period, StringComparer.Ordinal).ToList(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryGetPeriod(
        Dictionary<string, Dictionary<string, RawMediaMonitoring>> byCity,
        string cityId, string period, out RawMediaMonitoring media)
    {
        media = null!;
        if (!byCity.TryGetValue(cityId, out var periods)) return false;
        if (periods.TryGetValue(period, out var found)) { media = found; return true; }
        // Fallback: a single-month city whose list sheets omit the period column.
        if (period.Length == 0 && periods.Count == 1) { media = periods.Values.First(); return true; }
        return false;
    }

    private static void Readlist(
        IXLWorkbook wb, string sheetName, string valueHeader,
        Dictionary<string, Dictionary<string, RawMediaMonitoring>> byCity,
        Action<RawMediaMonitoring, string> add)
    {
        var sheet = SheetReader.Sheet(wb, sheetName);
        var h = SheetReader.ForHeader(sheet, 1);
        var periodCol = h.TryCol("period");
        var cityCol = h.Col("cityId");
        var valCol = h.Col(valueHeader);
        var last = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= last; r++)
        {
            var cityId = sheet.Cell(r, cityCol).GetString().Trim();
            var text = sheet.Cell(r, valCol).GetString().Trim();
            if (cityId.Length == 0 || text.Length == 0) continue;
            var period = periodCol is int pc ? sheet.Cell(r, pc).GetString().Trim() : "";
            if (TryGetPeriod(byCity, cityId, period, out var media)) add(media, text);
        }
    }

    private static bool ReadBool(IXLCell cell)
    {
        if (cell.DataType == XLDataType.Boolean) return cell.GetBoolean();
        var s = cell.GetString().Trim();
        return s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1"
            || s.Equals("نعم", StringComparison.OrdinalIgnoreCase);
    }

    private static int ReadInt(IXLCell cell)
        => cell.TryGetValue<double>(out var d) ? (int)Math.Round(d) : 0;

    private static double ReadDouble(IXLCell cell)
        => cell.TryGetValue<double>(out var d) ? d : 0;
}
