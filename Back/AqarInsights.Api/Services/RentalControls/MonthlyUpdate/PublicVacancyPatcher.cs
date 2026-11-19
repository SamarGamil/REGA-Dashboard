using ClosedXML.Excel;
using AqarInsights.Api.Services.RentalControls.Reading;

namespace AqarInsights.Api.Services.RentalControls.MonthlyUpdate;

// Upserts the general-vacancy file (English city names, rate as a fraction; cities without a published rate have an
// empty cell) into the vacancy workbook's الشواغر العامة sheet (whole percent, one decimal). Older months are kept.
public static class PublicVacancyPatcher
{
    private static readonly Dictionary<string, string> CityByEnglish = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Riyadh"] = "riyadh", ["Jeddah"] = "jeddah", ["Makkah"] = "makkah", ["Madinah"] = "madinah",
        ["Dammam"] = "dammam", ["Buraydah"] = "buraidah", ["Tabuk"] = "tabuk", ["Abha"] = "abha",
        ["Hail"] = "hail", ["Najran"] = "najran", ["Jazan"] = "jazan", ["Araar"] = "arar",
        ["Skaka"] = "sakaka", ["Baha"] = "bahah",
    };

    public static void Apply(IXLWorkbook vacancyWorkbook, IXLWorkbook update, MonthlyUpdateReport report)
    {
        var target = SheetReader.Sheet(vacancyWorkbook, "الشواغر العامة");
        var t = SheetReader.ForHeader(target, 1);
        int yearCol = t.Col("السنة"), monthCol = t.Col("الشهر"), cityCol = t.Col("المدينة"), valueCol = t.Col("معدل الشواغر العامة");

        var rows = new Dictionary<(string CityId, int Year, int Month), IXLRow>();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in target.RowsUsed().Skip(1))
        {
            var name = row.Cell(cityCol).GetString().Trim();
            if (RentalControlsMappings.CityId(name) is not { } id
                || !row.Cell(yearCol).TryGetValue<int>(out var y) || !row.Cell(monthCol).TryGetValue<int>(out var m)) continue;
            rows[(id, y, m)] = row;
            names[id] = name;
        }

        var source = update.Worksheet(1);
        var s = SheetReader.ForHeader(source, 1);
        int sYear = s.Col("year"), sMonth = s.Col("month"), sCity = s.Col("City"), sRate = s.Col("Vacancy Rate");
        var next = (target.LastRowUsed()?.RowNumber() ?? 1) + 1;
        foreach (var row in source.RowsUsed().Skip(1))
        {
            if (row.Cell(sRate).IsEmpty() || !row.Cell(sRate).TryGetValue<double>(out var rate)) continue;
            var english = row.Cell(sCity).GetString().Trim();
            if (!CityByEnglish.TryGetValue(english, out var cityId) || !names.TryGetValue(cityId, out var arabic))
            {
                report.Warnings.Add($"public vacancy: city «{english}» is unknown or has no history in the workbook — skipped");
                continue;
            }
            var (year, month, pct) = (row.Cell(sYear).GetValue<int>(), row.Cell(sMonth).GetValue<int>(), Math.Round(rate * 100, 1));
            if (rows.TryGetValue((cityId, year, month), out var existing))
            {
                var old = Math.Round(existing.Cell(valueCol).GetValue<double>(), 1);
                if (old == pct) continue;
                existing.Cell(valueCol).Value = pct;
                report.Changes.Add($"public vacancy {cityId} {year:D4}-{month:D2}: {old} → {pct}");
                continue;
            }
            target.Cell(next, yearCol).Value = year;
            target.Cell(next, monthCol).Value = month;
            target.Cell(next, cityCol).Value = arabic;
            target.Cell(next, valueCol).Value = pct;
            rows[(cityId, year, month)] = target.Row(next++);
            report.Changes.Add($"public vacancy {cityId} {year:D4}-{month:D2} = {pct}");
        }
    }
}
