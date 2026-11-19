using ClosedXML.Excel;
using AqarInsights.Api.Models.RentalControls.Raw;
using AqarInsights.Api.Utilities;

namespace AqarInsights.Api.Services.RentalControls.Reading;

public static class OverviewReader
{
    public static List<CityRecord> Read(IXLWorksheet sheet)
    {
        var headerRow = FindHeaderRow(sheet);
        var reader = SheetReader.ForHeader(sheet, headerRow);
        
        var cityCol = reader.Col("المدينة");
        // Q3 2026 renamed headers: الكيجر → المعدل السنوي المركب…, الزيادة → التغير الإيجاري, إيجار للدخل → نسبة الإيجار إلى الدخل.
        var cagrCol = reader.TryCol("الكيجر") ?? reader.Col("المركب");
        var pathCol = reader.Col("المسار");
        var vacancyCol = reader.Col("الشواغر");
        var returnCol = reader.Col("العائد");
        var increaseCol = reader.TryCol("الزيادة") ?? reader.Col("التغير");
        // Both needles: the Q3 CAGR header also contains "الإيجار".
        var rentCol = reader.Col("إيجار", "دخل");
        var burdenCol = reader.Col("العبء");
        var statusCol = reader.TryCol("الحالة");
        var assessmentCol = reader.Col("التقييم");
        
        var cities = new List<CityRecord>();
        
        for (var row = headerRow + 1; ; row++)
        {
            var name = sheet.Cell(row, cityCol).GetString().Trim();
            if (string.IsNullOrWhiteSpace(name)) break;
            
            var assessment = sheet.Cell(row, assessmentCol).GetString();
            var lines = assessment.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim('\u200f', '\u200e', ' '))
                .Where(l => l.Length > 0)
                .ToArray();
            var line1 = lines.Length > 0 ? lines[0] : "";
            var line2 = lines.Length > 1 ? string.Join(' ', lines.Skip(1)) : "";
            
            var returnValue = ParseNullableDouble(sheet.Cell(row, returnCol));
            
            cities.Add(new CityRecord
            {
                Id = RentalControlsMappings.CityId(name) ?? "",
                Name = name,
                Cagr = sheet.Cell(row, cagrCol).GetDouble(),
                Path = sheet.Cell(row, pathCol).GetString().Trim(),
                Vacancy = sheet.Cell(row, vacancyCol).GetDouble(),
                InvestmentReturn = returnValue,
                RentalIncrease = sheet.Cell(row, increaseCol).GetDouble(),
                RentToIncome = sheet.Cell(row, rentCol).GetDouble(),
                Burden = sheet.Cell(row, burdenCol).GetDouble(),
                Status = statusCol is { } sc ? sheet.Cell(row, sc).GetString().Trim() : "",
                AssessmentLine1 = line1,
                AssessmentLine2 = line2,
            });
        }
        
        return cities;
    }
    
    private static int FindHeaderRow(IXLWorksheet sheet)
    {
        var needle = ArabicNormalizer.Normalize("المدينة");
        foreach (var row in sheet.RowsUsed())
        {
            foreach (var cell in row.CellsUsed())
            {
                var text = ArabicNormalizer.Normalize(cell.GetString());
                if (text.Contains(needle))
                    return row.RowNumber();
            }
        }
        throw new RentalControlsReadException("Header row containing 'المدينة' not found");
    }
    
    private static double? ParseNullableDouble(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;
        var text = cell.GetString().Trim();
        if (text == "—" || text.Length == 0) return null;
        if (cell.DataType == ClosedXML.Excel.XLDataType.Number)
            return cell.GetDouble();
        return null;
    }
}
