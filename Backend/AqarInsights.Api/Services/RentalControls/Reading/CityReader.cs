using ClosedXML.Excel;
using AqarInsights.Api.Models.RentalControls.Raw;
using AqarInsights.Api.Utilities;

namespace AqarInsights.Api.Services.RentalControls.Reading;

public static class CityReader
{
    public static CityRawData Read(string cityId, IXLWorkbook wb)
    {
        var sheet1 = wb.Worksheet(1);
        var (bullets, movement) = ParseSheet1(sheet1);

        return new CityRawData
        {
            CityId = cityId,
            Bullets = bullets,
            MovementRows = movement.Select(m => new RawMovementRow
            {
                Indicator = m.Indicator,
                Level = m.Level,
                Change = m.Change,
                Assessment = m.Assessment
            }).ToList(),
            RentalsSeries = ExtractMultiValueSeries(wb, "الصفقات الإيجارية", "الفترة", new[] { "شقة", "دور", "فيلا", "دوبلاكس", "استديو", "الإجمالي" }),
            SalesSeries = ExtractMultiValueSeries(wb, "الصفقات البيعية", "الفترة", new[] { "شقة", "فيلا", "دور", "دوبلكس", "أرض", "الإجمالي" }, new[] { "أخرى", "عمارة" }),
            RentIndexSeries = ExtractIndexSeries(wb, "مؤشرات", "CPI", "الشهر", "الرقم القياسي", "التغير السنوي"),
            RentIndexByTypeSeries = ExtractMultiValueSeries(wb, "الرقم القياسي للإيجار", "الفترة", new[] { "شقة", "دور", "فيلا" }),
            AffordabilitySeries = ExtractSingleValueSeries(wb, "القدرة الإيجارية", "الفترة", "النسبة"),
            RoiSeries = ExtractSingleValueSeries(wb, "العائد", "الفترة", "العائد"),
            IncreaseSeries = ExtractSingleValueSeries(wb, "الزيادة", "الفترة", "القيمة"),
            RentVsInflationSeries = ExtractRentVsInflation(wb),
            // The affordability sheet stacks a second sub-table (العبء السكني) below the rent-to-income one.
            BurdenSeries = ExtractSubTableByValueHeader(wb, "القدرة الإيجارية", "الفترة", "العبء"),
        };
    }

    private static (List<string> Bullets, List<(string Indicator, string Level, string Change, string Assessment)> Movement) ParseSheet1(IXLWorksheet sheet)
    {
        var movement = new List<(string, string, string, string)>();
        var bullets = new List<string>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;

        // Market-movement table (reused from CitySheet1Parser).
        // Match the market sub-table title EXACTLY ("لوحة مؤشرات السوق العقاري") — not the
        // sheet title ("لوحة مؤشرات السوق العقاري - مدينة X"), which appears first and would
        // otherwise capture the rental-controls assessment panel instead.
        for (var r = 1; r <= lastRow; r++)
        {
            if (sheet.Cell(r, 2).GetString().Trim() != "لوحة مؤشرات السوق العقاري") continue;
            var dataStart = r + 2; // title row, header row, then data
            for (var d = dataStart; d <= lastRow; d++)
            {
                var indicator = sheet.Cell(d, 2).GetString().Trim();
                if (string.IsNullOrWhiteSpace(indicator)) break;
                movement.Add((
                    indicator,
                    sheet.Cell(d, 3).GetString().Trim(),
                    sheet.Cell(d, 4).GetString().Trim(),
                    sheet.Cell(d, 5).GetString().Trim()
                ));
            }
            break;
        }

        // Executive-summary conclusions (reused from CitySheet1Parser)
        for (var r = 1; r <= lastRow; r++)
        {
            if (!sheet.Cell(r, 2).GetString().Contains("أبرز الاستنتاجات")) continue;
            var cell = sheet.Cell(r + 1, 2).GetString();
            bullets = cell.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
            break;
        }

        return (bullets, movement);
    }

    // Optional headers (columns added in later workbook versions) are skipped when absent.
    private static List<RawSeriesPoint> ExtractMultiValueSeries(IXLWorkbook wb, string sheetName, string periodHeader, string[] valueHeaders, string[]? optionalHeaders = null)
    {
        try
        {
            var sheet = SheetReader.Sheet(wb, sheetName);
            var reader = SheetReader.ForHeader(sheet, 4); // Header row is 4 in real city workbooks
            var periodCol = reader.Col(periodHeader);
            var valueCols = valueHeaders.Select(h => (Header: h, Col: reader.Col(h)))
                .Concat((optionalHeaders ?? Array.Empty<string>())
                    .Select(h => (Header: h, Col: reader.TryCol(h) ?? 0))
                    .Where(x => x.Col > 0))
                .ToArray();

            var points = new List<RawSeriesPoint>();
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
            
            for (var r = 5; r <= lastRow; r++) // Data starts at row 5
            {
                var period = sheet.Cell(r, periodCol).GetString().Trim();
                if (string.IsNullOrWhiteSpace(period)) continue;

                var values = new Dictionary<string, double>();
                foreach (var (header, col) in valueCols)
                {
                    if (sheet.Cell(r, col).TryGetValue<double>(out var value))
                        values[header] = Math.Round(value, 2);
                }

                if (values.Count > 0)
                    points.Add(new RawSeriesPoint { Period = period, Values = values });
            }

            return points;
        }
        catch (RentalControlsReadException)
        {
            return new List<RawSeriesPoint>();
        }
    }

    private static List<RawSeriesPoint> ExtractSingleValueSeries(IXLWorkbook wb, string sheetName, string periodHeader, string valueHeader)
    {
        try
        {
            var sheet = SheetReader.Sheet(wb, sheetName);
            var reader = SheetReader.ForHeader(sheet, 4);
            var periodCol = reader.Col(periodHeader);
            var valueCol = reader.Col(valueHeader);

            var points = new List<RawSeriesPoint>();
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
            
            for (var r = 5; r <= lastRow; r++)
            {
                var period = sheet.Cell(r, periodCol).GetString().Trim();
                if (string.IsNullOrWhiteSpace(period)) continue;

                if (sheet.Cell(r, valueCol).TryGetValue<double>(out var value))
                {
                    points.Add(new RawSeriesPoint 
                    { 
                        Period = period, 
                        Values = new Dictionary<string, double> { [valueHeader] = Math.Round(value, 2) } 
                    });
                }
            }

            return points;
        }
        catch (RentalControlsReadException)
        {
            return new List<RawSeriesPoint>();
        }
    }

    // Reads a sub-table identified by its value header (e.g. العبء) anywhere on the sheet,
    // for sheets that stack several sub-tables sharing the same columns.
    private static List<RawSeriesPoint> ExtractSubTableByValueHeader(IXLWorkbook wb, string sheetName, string periodHeader, string valueHeader)
    {
        try
        {
            var sheet = SheetReader.Sheet(wb, sheetName);
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
            var periodNeedle = ArabicNormalizer.Normalize(periodHeader);
            var valueNeedle = ArabicNormalizer.Normalize(valueHeader);

            int headerRow = 0, periodCol = 0, valueCol = 0;
            for (var r = 1; r <= lastRow && headerRow == 0; r++)
            {
                int pc = 0, vc = 0;
                foreach (var cell in sheet.Row(r).CellsUsed())
                {
                    var t = ArabicNormalizer.Normalize(cell.GetString());
                    if (t == periodNeedle) pc = cell.Address.ColumnNumber;
                    else if (t == valueNeedle) vc = cell.Address.ColumnNumber;
                }
                if (pc > 0 && vc > 0) { headerRow = r; periodCol = pc; valueCol = vc; }
            }
            if (headerRow == 0) return new List<RawSeriesPoint>();

            var points = new List<RawSeriesPoint>();
            for (var r = headerRow + 1; r <= lastRow; r++)
            {
                var period = sheet.Cell(r, periodCol).GetString().Trim();
                if (string.IsNullOrWhiteSpace(period)) break; // sub-table ends at first blank row
                if (sheet.Cell(r, valueCol).TryGetValue<double>(out var value))
                    points.Add(new RawSeriesPoint
                    {
                        Period = period,
                        Values = new Dictionary<string, double> { [valueHeader] = Math.Round(value, 2) }
                    });
            }
            return points;
        }
        catch (RentalControlsReadException)
        {
            return new List<RawSeriesPoint>();
        }
    }

    private static List<RawSeriesPoint> ExtractIndexSeries(IXLWorkbook wb, params string[] sheetNameParts)
    {
        try
        {
            var periodHeader = sheetNameParts[^3];
            var indexHeader = sheetNameParts[^2];
            var changeHeader = sheetNameParts[^1];
            var sheetParts = sheetNameParts[..^3];
            var sheet = SheetReader.Sheet(wb, sheetParts);
            var reader = SheetReader.ForHeader(sheet, 4);
            var periodCol = reader.Col(periodHeader);
            var indexCol = reader.Col(indexHeader);
            var changeCol = reader.TryCol(changeHeader); // Change might not exist

            var points = new List<RawSeriesPoint>();
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
            
            for (var r = 5; r <= lastRow; r++)
            {
                var period = sheet.Cell(r, periodCol).GetString().Trim();
                // The CPI sheet stacks a second block (التضخم العام) below the rent-index one.
                // Stop at it — both when a blank separator precedes it (normal layout) and when
                // its title appears directly (cities whose rent-index block is empty).
                if (period.Contains("التضخم")) break;
                if (string.IsNullOrWhiteSpace(period))
                {
                    if (points.Count > 0) break;
                    continue;
                }

                if (sheet.Cell(r, indexCol).TryGetValue<double>(out var indexValue))
                {
                    var change = changeCol.HasValue ? sheet.Cell(r, changeCol.Value).GetString().Trim() : null;
                    points.Add(new RawSeriesPoint 
                    { 
                        Period = period, 
                        Values = new Dictionary<string, double> { [indexHeader] = Math.Round(indexValue, 2) },
                        Change = string.IsNullOrWhiteSpace(change) || change == "-" ? null : change
                    });
                }
            }

            return points;
        }
        catch (RentalControlsReadException)
        {
            return new List<RawSeriesPoint>();
        }
    }

    // CPI sheet holds a rent block and a general-inflation block, both with "الشهر"/"التغير السنوي".
    // Two layouts occur: side-by-side (both blocks share header row 4 → two change columns) and
    // stacked (only the rent block is on row 4; the inflation block sits below under a "التضخم" title).
    private static List<RawSeriesPoint> ExtractRentVsInflation(IXLWorkbook wb)
    {
        try
        {
            var sheet = SheetReader.Sheet(wb, "مؤشرات", "CPI");
            const int headerRow = 4;
            var monthCols = new List<int>();
            var changeCols = new List<int>();
            foreach (var c in sheet.Row(headerRow).CellsUsed())
            {
                var key = c.GetString().Trim();
                if (key == "الشهر") monthCols.Add(c.Address.ColumnNumber);
                else if (key == "التغير السنوي") changeCols.Add(c.Address.ColumnNumber);
            }
            if (monthCols.Count == 0 || changeCols.Count == 0) return new List<RawSeriesPoint>();
            monthCols.Sort();
            changeCols.Sort();

            return changeCols.Count >= 2
                ? ReadRentVsInflationSideBySide(sheet, headerRow, monthCols[0], changeCols[0], changeCols[1])
                : ReadRentVsInflationStacked(sheet, headerRow, monthCols[0], changeCols[0]);
        }
        catch (RentalControlsReadException)
        {
            return new List<RawSeriesPoint>();
        }
    }

    // Side-by-side layout: rent change and inflation change share header row 4 (two change columns).
    private static List<RawSeriesPoint> ReadRentVsInflationSideBySide(
        IXLWorksheet sheet, int headerRow, int monthCol, int rentCol, int inflationCol)
    {
        var points = new List<RawSeriesPoint>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        for (var r = headerRow + 1; r <= lastRow; r++)
        {
            var period = sheet.Cell(r, monthCol).GetString().Trim();
            if (string.IsNullOrWhiteSpace(period)) continue;
            var rent = ParsePercent(sheet.Cell(r, rentCol).GetString());
            var inflation = ParsePercent(sheet.Cell(r, inflationCol).GetString());
            if (rent is null && inflation is null) continue;
            var values = new Dictionary<string, double>();
            if (rent.HasValue) values["الإيجار الفعلي"] = Math.Round(rent.Value, 2);
            if (inflation.HasValue) values["التضخم العام"] = Math.Round(inflation.Value, 2);
            points.Add(new RawSeriesPoint { Period = period, Values = values });
        }
        return points;
    }

    // Stacked layout: the rent block is on header row 4; the general-inflation block is stacked
    // below under a "التضخم" title with its own header row. Merge both blocks by month.
    private static List<RawSeriesPoint> ReadRentVsInflationStacked(
        IXLWorksheet sheet, int headerRow, int rentMonthCol, int rentChangeCol)
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;

        // Locate the general-inflation sub-table title so the rent block can stop before it.
        var inflationTitleRow = 0;
        for (var r = headerRow + 1; r <= lastRow; r++)
        {
            if (sheet.Row(r).CellsUsed().Any(c => c.GetString().Contains("التضخم")))
            {
                inflationTitleRow = r;
                break;
            }
        }

        var byMonth = new Dictionary<string, Dictionary<string, double>>();
        var order = new List<string>();

        void Merge(string month, string key, double value)
        {
            if (!byMonth.TryGetValue(month, out var v)) { v = new Dictionary<string, double>(); byMonth[month] = v; order.Add(month); }
            v[key] = Math.Round(value, 2);
        }

        // Rent block: data start up to the inflation title (or sheet end).
        var rentEnd = inflationTitleRow > 0 ? inflationTitleRow - 1 : lastRow;
        for (var r = headerRow + 1; r <= rentEnd; r++)
        {
            var month = sheet.Cell(r, rentMonthCol).GetString().Trim();
            if (string.IsNullOrWhiteSpace(month)) continue;
            var rent = ParsePercent(sheet.Cell(r, rentChangeCol).GetString());
            if (rent.HasValue) Merge(month, "الإيجار الفعلي", rent.Value);
        }

        // Inflation block: find its own header row (الشهر + التغير السنوي) below the title.
        if (inflationTitleRow > 0)
        {
            int infHeaderRow = 0, infMonthCol = 0, infChangeCol = 0;
            for (var r = inflationTitleRow; r <= lastRow && infHeaderRow == 0; r++)
            {
                int mc = 0, cc = 0;
                foreach (var c in sheet.Row(r).CellsUsed())
                {
                    var key = c.GetString().Trim();
                    if (key == "الشهر") mc = c.Address.ColumnNumber;
                    else if (key == "التغير السنوي") cc = c.Address.ColumnNumber;
                }
                if (mc > 0 && cc > 0) { infHeaderRow = r; infMonthCol = mc; infChangeCol = cc; }
            }

            if (infHeaderRow > 0)
            {
                for (var r = infHeaderRow + 1; r <= lastRow; r++)
                {
                    var month = sheet.Cell(r, infMonthCol).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(month)) continue;
                    var inflation = ParsePercent(sheet.Cell(r, infChangeCol).GetString());
                    if (inflation.HasValue) Merge(month, "التضخم العام", inflation.Value);
                }
            }
        }

        return order.Select(m => new RawSeriesPoint { Period = m, Values = byMonth[m] }).ToList();
    }

    private static double? ParsePercent(string? s) => ArabicNumber.FirstNumber(s);
}
