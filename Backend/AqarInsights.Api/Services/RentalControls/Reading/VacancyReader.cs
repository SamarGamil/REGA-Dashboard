using ClosedXML.Excel;
using AqarInsights.Api.Models.RentalControls.Raw;
using AqarInsights.Api.Utilities;

namespace AqarInsights.Api.Services.RentalControls.Reading;

public static class VacancyReader
{
    private static readonly string[] ValidYears = { "2023", "2024", "2025", "2026" };

    public static List<IndicatorSeriesPoint> Read(IXLWorkbook wb, IReadOnlyDictionary<string, string> cityIdByNormalizedName)
    {
        var points = new List<IndicatorSeriesPoint>();
        
        // 1. Rent to Income (نسبة الإيجار إلى الدخل) - filter geo=مدينة, scale×100 (Saudi-only dashboard: no nationality filter)
        ReadIndicator(wb, cityIdByNormalizedName, points, 
            sheetName: "نسبة الإيجار إلى الدخل", 
            indicator: "rentToIncome",
            valueCol: "نسبة الإيجار من الدخل",
            periodCol: "الربع",
            scale: 100,
            filters: new[] { ("المستوى الجغرافي", "مدينة") });
        
        // 2. Burden (العبء الايجاري) is sourced from the per-city workbooks (CityReader), not here.

        // 3. ROI (العائد من الاستثمار) - filter unitType=عام سكني
        ReadIndicator(wb, cityIdByNormalizedName, points,
            sheetName: "العائد من الاستثمار",
            indicator: "roi",
            valueCol: "العائد من الاستثمار",
            periodCol: "الربع",
            scale: 1,
            filters: new[] { ("نوع الوحدة", "عام سكني") },
            unitTypeTag: "عام سكني");
        
        // 4. Public Vacancy (الشواغر العامة- لم يعتمد)
        ReadIndicator(wb, cityIdByNormalizedName, points,
            sheetName: "الشواغر العامة",
            indicator: "publicVacancy",
            valueCol: "معدل الشواغر العامة",
            periodCol: "الشهر",
            scale: 1);
        
        // 5. Rental Change (التغير الإيجاري)
        ReadIndicator(wb, cityIdByNormalizedName, points,
            sheetName: "التغير الإيجاري",
            indicator: "rentalChange",
            valueCol: "التغير الإيجاري",
            periodCol: "الشهر",
            scale: 1);

        // 6. Rental Vacancy (الشواغر الايجارية) - combined YYYY-MM period, value ×100 to whole percent
        ReadRentalVacancy(wb, cityIdByNormalizedName, points);

        return points;
    }

    private static void ReadRentalVacancy(
        IXLWorkbook wb,
        IReadOnlyDictionary<string, string> cityIdByNormalizedName,
        List<IndicatorSeriesPoint> points)
    {
        // The sheet repeats some city-months (city and governorate rows); a repeat must carry the same rate.
        var seen = new Dictionary<(string CityId, int Year, int Month), double>();
        var conflicts = new List<string>();
        try
        {
            var sheet = SheetReader.Sheet(wb, "الشواغر الايجارية");
            var reader = SheetReader.ForHeader(sheet, 1);

            var period = reader.Col("الفترة");
            var city = reader.Col("المدينة");
            var value = reader.Col("معدل الشواغر");

            foreach (var row in sheet.RowsUsed().Skip(1))
            {
                var periodText = row.Cell(period).GetString().Trim();
                if (!TryParseYearMonth(periodText, out var year, out var month)) continue;
                if (!ValidYears.Contains(year.ToString())) continue;

                var cityName = row.Cell(city).GetString().Trim();
                var normalizedCity = ArabicNormalizer.Normalize(cityName);
                if (!cityIdByNormalizedName.TryGetValue(normalizedCity, out var cityId)) continue;

                if (!row.Cell(value).TryGetValue<double>(out var rawValue)) continue;
                var rate = Math.Round(rawValue * 100, 0);
                if (seen.TryGetValue((cityId, year, month), out var earlier) && earlier != rate)
                    conflicts.Add($"{cityId} {year:D4}-{month:D2}: {earlier}% / {rate}%");
                seen.TryAdd((cityId, year, month), rate);

                points.Add(new IndicatorSeriesPoint
                {
                    Indicator = "vacancy",
                    CityId = cityId,
                    Year = year,
                    Period = month,
                    Value = rate,
                });
            }
        }
        catch (RentalControlsReadException)
        {
            // Sheet not found or missing columns - skip silently
        }

        if (conflicts.Count > 0)
            throw new RentalControlsReadException(
                $"الشواغر الايجارية: the same city and month have different rates ({string.Join("; ", conflicts)}).");
    }

    private static bool TryParseYearMonth(string text, out int year, out int month)
    {
        year = 0; month = 0;
        var parts = text.Split('-');
        return parts.Length == 2
            && int.TryParse(parts[0], out year)
            && int.TryParse(parts[1], out month);
    }

    private static void ReadIndicator(
        IXLWorkbook wb,
        IReadOnlyDictionary<string, string> cityIdByNormalizedName,
        List<IndicatorSeriesPoint> points,
        string sheetName,
        string indicator,
        string valueCol,
        string periodCol,
        double scale,
        (string header, string value)[]? filters = null,
        string? nationalityTag = null,
        string? unitTypeTag = null)
    {
        try
        {
            var sheet = SheetReader.Sheet(wb, sheetName);
            var reader = SheetReader.ForHeader(sheet, 1);
            
            var yearCol = reader.Col("السنة");
            var period = reader.Col(periodCol);
            var city = reader.Col("المدينة");
            var value = reader.Col(valueCol);
            
            // Get filter columns if any
            var filterCols = filters?.Select(f => (col: reader.TryCol(f.header), value: f.value))
                .Where(x => x.col.HasValue)
                .Select(x => (col: x.col!.Value, value: x.value))
                .ToArray() ?? Array.Empty<(int col, string value)>();
            
            foreach (var row in sheet.RowsUsed().Skip(1))
            {
                // Apply filters
                var passesFilters = filterCols.All(f =>
                {
                    var cellValue = row.Cell(f.col).GetString().Trim();
                    return string.Equals(cellValue, f.value, StringComparison.OrdinalIgnoreCase);
                });
                if (!passesFilters) continue;
                
                // Extract year
                if (!row.Cell(yearCol).TryGetValue<double>(out var yearDouble)) continue;
                var year = (int)yearDouble;
                if (!ValidYears.Contains(year.ToString())) continue;
                
                // Extract city
                var cityName = row.Cell(city).GetString().Trim();
                var normalizedCity = ArabicNormalizer.Normalize(cityName);
                if (!cityIdByNormalizedName.TryGetValue(normalizedCity, out var cityId)) continue;
                
                // Extract value
                if (!row.Cell(value).TryGetValue<double>(out var rawValue)) continue;
                var finalValue = Math.Round(rawValue * scale, 1);
                
                // Extract period
                if (!row.Cell(period).TryGetValue<double>(out var periodDouble)) continue;
                var periodInt = (int)periodDouble;
                
                points.Add(new IndicatorSeriesPoint
                {
                    Indicator = indicator,
                    CityId = cityId,
                    Year = year,
                    Period = periodInt,
                    Value = finalValue,
                    Nationality = nationalityTag,
                    UnitType = unitTypeTag
                });
            }
        }
        catch (RentalControlsReadException)
        {
            // Sheet not found or missing columns - skip this indicator silently
        }
    }
}
