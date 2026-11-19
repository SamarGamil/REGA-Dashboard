using ClosedXML.Excel;
using AqarInsights.Api.Services.RentalControls.Reading;
using AqarInsights.Api.Services.RentalControls.Snapshots;

namespace AqarInsights.Api.Services.RentalControls.MonthlyUpdate;

// Appends a city's monthly rental-vacancy rates (city workbook sheet الشواغر الإيجارية, whole %) to the vacancy
// workbook's الشواغر الايجارية sheet, which VacancyReader reads. Months already there are never overwritten.
public static class RentalVacancyPatcher
{
    public static IReadOnlyList<(YearMonth Month, double RatePct)> ReadCityMonthly(IXLWorkbook cityUpdate)
    {
        var sheet = SheetReader.Sheet(cityUpdate, "الشواغر");
        var header = sheet.RowsUsed().First(r => r.Cell(2).GetString().Trim() == "الشهر").RowNumber();
        var result = new List<(YearMonth, double)>();
        for (var r = header + 1; YearMonth.TryParse(sheet.Cell(r, 2).GetString().Trim(), out var month); r++)
            if (sheet.Cell(r, 3).TryGetValue<double>(out var pct)) result.Add((month, pct));
        return result;
    }

    public static void Append(IXLWorkbook vacancyWorkbook, string cityId,
        IReadOnlyList<(YearMonth Month, double RatePct)> monthly, MonthlyUpdateReport report)
    {
        var sheet = SheetReader.Sheet(vacancyWorkbook, "الشواغر الايجارية");
        var cols = SheetReader.ForHeader(sheet, 1);
        int periodCol = cols.Col("الفترة"), cityCol = cols.Col("المدينة"), rateCol = cols.Col("معدل الشواغر");
        var occupiedCol = cols.TryCol("معدل المأهول");

        var existing = new Dictionary<string, List<double>>(StringComparer.Ordinal);
        string? name = null;
        foreach (var row in sheet.RowsUsed().Skip(1))
        {
            var cityName = row.Cell(cityCol).GetString().Trim();
            if (RentalControlsMappings.CityId(cityName) != cityId) continue;
            name = cityName;
            var period = row.Cell(periodCol).GetString().Trim();
            if (!existing.TryGetValue(period, out var rates)) existing[period] = rates = new();
            if (row.Cell(rateCol).TryGetValue<double>(out var rate)) rates.Add(rate);
        }
        if (name is null)
        {
            report.Warnings.Add($"rental vacancy {cityId}: city not found in الشواغر الايجارية — skipped");
            return;
        }

        var next = (sheet.LastRowUsed()?.RowNumber() ?? 1) + 1;
        foreach (var (month, pct) in monthly)
        {
            if (existing.TryGetValue(month.Id, out var rates))
            {
                if (rates.Any(r => Math.Round(r * 100, 0) != Math.Round(pct, 0)))
                    report.Warnings.Add($"rental vacancy {cityId} {month.Id}: workbook {string.Join("/", rates.Select(r => Math.Round(r * 100, 0)))}% vs update {pct}% — kept");
                continue;
            }
            sheet.Cell(next, periodCol).Value = month.Id;
            sheet.Cell(next, cityCol).Value = name;
            sheet.Cell(next, rateCol).Value = pct / 100;
            if (occupiedCol is int oc) sheet.Cell(next, oc).Value = 1 - pct / 100;
            next++;
            report.Changes.Add($"rental vacancy {cityId} {month.Id} = {pct}%");
        }
    }
}
