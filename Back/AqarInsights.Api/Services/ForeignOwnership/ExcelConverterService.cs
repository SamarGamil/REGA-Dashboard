using ClosedXML.Excel;
using AqarInsights.Api.Models;
using AqarInsights.Api.Models.ForeignOwnership;
using AqarInsights.Api.Utilities;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AqarInsights.Api.Services.ForeignOwnership;

public class ExcelConverterService : IExcelConverterService
{
    private readonly string _inputDir;
    private readonly string _outputDir;
    private readonly ILogger<ExcelConverterService> _logger;
    private readonly Dictionary<string, string> _cityToRegionMap;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExcelConverterService(string inputDir, string outputDir, string filterOptionsFile, ILogger<ExcelConverterService> logger)
    {
        _inputDir = inputDir;
        _outputDir = outputDir;
        _logger = logger;
        _cityToRegionMap = LoadCityToRegionMap(filterOptionsFile);
    }

    public string InputDir => _inputDir;

    private Dictionary<string, string> LoadCityToRegionMap(string filterOptionsFile)
    {
        var map = new Dictionary<string, string>();
        if (!File.Exists(filterOptionsFile))
        {
            _logger.LogWarning("Filter options file not found: {File}", filterOptionsFile);
            return map;
        }

        var json = File.ReadAllText(filterOptionsFile);
        using var doc = JsonDocument.Parse(json);
        var geography = doc.RootElement.GetProperty("geography");

        foreach (var region in geography.EnumerateArray())
        {
            var regionName = region.GetProperty("name").GetString() ?? "";
            foreach (var city in region.GetProperty("children").EnumerateArray())
            {
                var cityName = city.GetProperty("name").GetString() ?? "";
                if (!string.IsNullOrWhiteSpace(cityName))
                    map.TryAdd(ArabicNormalizer.Normalize(cityName), ArabicNormalizer.Normalize(regionName));
            }
        }

        _logger.LogInformation("Loaded {Count} city-to-region mappings", map.Count);
        return map;
    }

    private string? FindLatestExcelFile()
    {
        if (!Directory.Exists(_inputDir)) return null;
        return Directory.GetFiles(_inputDir, "*.xlsx")
            .Where(f => !Path.GetFileName(f).StartsWith("~$")) // skip Excel lock/owner files
            .OrderByDescending(File.GetLastWriteTime)
            .FirstOrDefault();
    }

    public List<(string SheetName, List<string> Columns)> InspectFile()
    {
        var result = new List<(string, List<string>)>();
        var filePath = FindLatestExcelFile();
        if (filePath == null)
        {
            _logger.LogWarning("No Excel files found in {Dir}", _inputDir);
            return result;
        }

        _logger.LogInformation("Inspecting: {File}", filePath);
        using var workbook = new XLWorkbook(filePath);

        foreach (var ws in workbook.Worksheets)
        {
            var columns = new List<string>();
            var firstRow = ws.FirstRowUsed();
            if (firstRow == null) continue;

            foreach (var cell in firstRow.CellsUsed())
            {
                columns.Add(cell.GetString().Trim());
            }

            _logger.LogInformation("Sheet '{Sheet}': {Cols} columns, ~{Rows} rows used",
                ws.Name, columns.Count, ws.RowsUsed().Count() - 1);

            var rows = ws.RowsUsed().Take(6).ToList();
            for (int i = 0; i < rows.Count; i++)
            {
                var cells = rows[i].CellsUsed().Select(c => $"[{c.Address.ColumnLetter}]{c.GetString().Trim()}").ToList();
                _logger.LogInformation("  Row {Row}: {Cells}", rows[i].RowNumber(), string.Join(" | ", cells));
            }

            result.Add((ws.Name, columns));
        }

        return result;
    }

    public async Task<ConversionReport> ConvertLatestAsync(CancellationToken cancellationToken = default)
    {
        var report = new ConversionReport();
        var filePath = FindLatestExcelFile();

        if (filePath == null)
        {
            report.Errors.Add($"No Excel files found in {_inputDir}");
            return report;
        }

        _logger.LogInformation("Converting: {File}", filePath);

        try
        {
            using var workbook = new XLWorkbook(filePath);
            var reportDate = ReportDateFromFileName(filePath);
            var records = ParseAllRows(workbook, report, reportDate);

            report.TotalRows = report.ValidRows + report.SkippedRows;
            _logger.LogInformation("Parsed {Valid} valid rows, skipped {Skipped}", report.ValidRows, report.SkippedRows);

            if (records.Count == 0)
            {
                report.Errors.Add("No valid records found");
                return report;
            }

            Directory.CreateDirectory(_outputDir);

            var transactionsPath = Path.Combine(_outputDir, "transactions.json");
            var transactionsJson = JsonSerializer.Serialize(records, _jsonOptions);
            await File.WriteAllTextAsync(transactionsPath, transactionsJson, cancellationToken);
            report.FilesGenerated.Add("transactions.json");

            report.Success = true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException)
        {
            report.Errors.Add(ex.Message);
            _logger.LogError(ex, "Conversion failed");
        }

        return report;
    }

    private List<TransactionRecord> ParseAllRows(XLWorkbook workbook, ConversionReport report, DateTime? reportDate)
    {
        var records = new List<TransactionRecord>();
        var unmappedCities = new HashSet<string>();
        var dataSheets = new[] { "الشراء", "البيع", "الهبة" };

        foreach (var ws in workbook.Worksheets)
        {
            if (!dataSheets.Contains(ws.Name)) continue;

            var headerRow = FindHeaderRow(ws);
            if (headerRow == null)
            {
                report.Warnings.Add(new ConversionWarning { Sheet = ws.Name, Issue = "لم يتم العثور على صف العناوين" });
                continue;
            }

            var colMap = BuildColumnMap(headerRow);
            _logger.LogInformation("Sheet '{Sheet}': mapped columns: {Keys}",
                ws.Name, string.Join(", ", colMap.Keys));

            // Default transaction type from sheet name
            var sheetTransactionType = NormalizeTransactionType(ws.Name);

            var firstDataRow = headerRow.RowNumber() + 1;
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? firstDataRow - 1;
            for (int rowNum = firstDataRow; rowNum <= lastRow; rowNum++)
            {
                var row = ws.Row(rowNum);
                if (!row.CellsUsed().Any()) continue;

                try
                {
                    var city = ArabicNormalizer.Normalize(GetString(row, colMap, "city"));
                    var region = _cityToRegionMap.TryGetValue(city, out var mappedRegion) ? mappedRegion : "";
                    var valueStr = colMap.TryGetValue("value", out var valueCol) ? row.Cell(valueCol).GetString().Trim() : "";
                    var transactionType = GetString(row, colMap, "transactionType");

                    // Default from sheet name if column is empty
                    if (string.IsNullOrWhiteSpace(transactionType))
                        transactionType = sheetTransactionType;
                    else
                        transactionType = NormalizeTransactionType(transactionType);

                    // Types outside شراء/بيع/هبة (e.g. "نقل ملكية بموجب حكم قضائي") follow their sheet's type.
                    if (transactionType != TransactionTypes.Purchase && transactionType != TransactionTypes.Sale && transactionType != TransactionTypes.Gift)
                        transactionType = sheetTransactionType;

                    decimal value = 0;
                    if (!string.IsNullOrWhiteSpace(valueStr) && !valueStr.Contains("لا") && !valueStr.Contains("لايوجد") && valueCol > 0)
                    {
                        var cell = row.Cell(valueCol);
                        if (cell.DataType == XLDataType.Number)
                            value = (decimal)cell.GetDouble();
                        else
                            decimal.TryParse(valueStr.Replace(",", ""), out value);
                    }

                    if (string.IsNullOrWhiteSpace(region) && string.IsNullOrWhiteSpace(city))
                    {
                        report.SkippedRows++;
                        report.SkippedDetails.Add(new SkippedRowDetail
                        {
                            Sheet = ws.Name,
                            Row = rowNum,
                            Reason = "لا يوجد منطقة أو مدينة",
                            RawData = GetRowSummary(row, colMap)
                        });
                        continue;
                    }

                    // A located transaction with a missing/zero price is still a real transaction
                    // (the source workbook counts it); keep it with value 0 rather than dropping it.

                    if (string.IsNullOrWhiteSpace(region))
                    {
                        region = city;
                        if (unmappedCities.Add(city))
                            report.Warnings.Add(new ConversionWarning
                            {
                                Sheet = ws.Name,
                                Row = rowNum,
                                Issue = $"المدينة «{city}» غير مرتبطة بمنطقة في ملف الجغرافيا"
                            });
                    }

                    var propertyType = PropertyCategories.FromRaw(GetString(row, colMap, "propertyType"));

                    var nationality = NationalityNormalizer.Canonicalize(GetString(row, colMap, "nationality"));
                    if (nationality.Length > 0 && !ArabicLetter.IsMatch(nationality))
                    {
                        report.Warnings.Add(new ConversionWarning
                        {
                            Sheet = ws.Name,
                            Row = rowNum,
                            Issue = $"جنسية غير معتمدة «{nationality}» ← {UnspecifiedNationality} (رقم العقار {GetString(row, colMap, "propertyNumber")})"
                        });
                        nationality = UnspecifiedNationality;
                    }

                    var rec = new TransactionRecord
                    {
                        Date = GetDate(row, colMap, reportDate),
                        Month = "",
                        TransactionType = transactionType,
                        Region = region,
                        City = city,
                        District = ArabicNormalizer.Normalize(GetString(row, colMap, "district")),
                        Nationality = nationality,
                        PropertyType = propertyType,
                        Area = GetDecimal(row, colMap, "area"),
                        Value = value,
                        OwnerType = NormalizeOwnerType(GetString(row, colMap, "ownerType")),
                        TransactionShare = GetShare(row, colMap)
                    };

                    if (rec.Date.HasValue)
                        rec.Month = rec.Date.Value.ToString("yyyy-MM");

                    records.Add(rec);
                    report.ValidRows++;
                }
                catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
                {
                    report.SkippedRows++;
                    report.SkippedDetails.Add(new SkippedRowDetail
                    {
                        Sheet = ws.Name,
                        Row = rowNum,
                        Reason = $"خطأ في المعالجة: {ex.Message}"
                    });
                    _logger.LogWarning("Row {Row} parse error in {Sheet}: {Msg}", rowNum, ws.Name, ex.Message);
                }
            }
        }

        return records;
    }

    private static string NormalizeTransactionType(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        if (raw.Contains(TransactionTypes.Purchase)) return TransactionTypes.Purchase;
        if (raw.Contains(TransactionTypes.Sale)) return TransactionTypes.Sale;
        if (raw.Contains(TransactionTypes.Gift)) return TransactionTypes.Gift;
        if (raw.Contains("هبه")) return TransactionTypes.Gift;
        return raw;
    }

    private const string UnspecifiedNationality = "غير محدد";
    private static readonly Regex ArabicLetter = new(@"\p{IsArabic}", RegexOptions.Compiled);

    private static string NormalizeOwnerType(string raw)
    {
        if (raw.Contains("Company", StringComparison.OrdinalIgnoreCase) || raw.Contains("شركة")) return OwnerTypes.Company;
        if (raw.Contains("Individual", StringComparison.OrdinalIgnoreCase) || raw.Contains("فرد")) return OwnerTypes.Individual;
        return "";
    }

    private static decimal? GetShare(IXLRow row, Dictionary<string, int> map)
    {
        if (!map.TryGetValue("share", out var col)) return null;
        var cell = row.Cell(col);
        if (cell.IsEmpty()) return null;
        if (cell.DataType == XLDataType.Number) return (decimal)cell.GetDouble();
        return decimal.TryParse(cell.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    // Templates put title/lineage rows above the Arabic header; the header is the first row mapping city + 2 more columns.
    private static IXLRow? FindHeaderRow(IXLWorksheet ws)
    {
        for (int r = 1; r <= 10; r++)
        {
            var map = BuildColumnMap(ws.Row(r));
            if (map.ContainsKey("city") && map.Count >= 3) return ws.Row(r);
        }
        return null;
    }

    private static Dictionary<string, int> BuildColumnMap(IXLRow headerRow)
    {
        var map = new Dictionary<string, int>();
        foreach (var cell in headerRow.CellsUsed())
        {
            var header = cell.GetString().Trim();
            var col = cell.Address.ColumnNumber;

            if (MatchesAny(header, "Company vs", "نوع المالك"))
                map.TryAdd("ownerType", col);
            else if (MatchesAny(header, "Transaction Share", "نسبة الملكية"))
                map.TryAdd("share", col);
            else if (MatchesAny(header, "رقم العقار"))
                map.TryAdd("propertyNumber", col);
            else if (MatchesAny(header, "تاريخ تنفيذ", "تاريخ", "التاريخ"))
                map.TryAdd("date", col);
            else if (MatchesAny(header, "نوع العملية", "نوع التصرف", "نوع المعاملة"))
                map.TryAdd("transactionType", col);
            else if (MatchesAny(header, "مدينة العقار", "المدينة", "مدينة"))
                map.TryAdd("city", col);
            else if (MatchesAny(header, "الحي", "حي"))
                map.TryAdd("district", col);
            else if (MatchesAny(header, "جنسية", "الجنسية"))
                map.TryAdd("nationality", col);
            else if (MatchesAny(header, "نوع العقار"))
                map.TryAdd("propertyType", col);
            else if (MatchesAny(header, "المساحة", "مساحة"))
                map.TryAdd("area", col);
            else if (MatchesAny(header, "قيمة الصفقة", "القيمة", "قيمة", "السعر", "المبلغ"))
                map.TryAdd("value", col);
        }
        return map;
    }

    private static bool MatchesAny(string header, params string[] keywords)
    {
        return keywords.Any(k => header.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetString(IXLRow row, Dictionary<string, int> map, string key)
    {
        if (!map.TryGetValue(key, out var col)) return "";
        return row.Cell(col).GetString().Trim();
    }

    private static decimal GetDecimal(IXLRow row, Dictionary<string, int> map, string key)
    {
        if (!map.TryGetValue(key, out var col)) return 0;
        var cell = row.Cell(col);
        if (cell.IsEmpty()) return 0;
        if (cell.DataType == XLDataType.Number)
            return (decimal)cell.GetDouble();
        if (decimal.TryParse(cell.GetString().Replace(",", ""), out var val))
            return val;
        return 0;
    }

    // Text date cells are entered inconsistently (day-first, month-first, ISO, dotted).
    // Parse with fixed formats under InvariantCulture so results are reproducible across
    // locales; day-first is tried before month-first so ambiguous d≤12 values match Saudi intent.
    private static readonly string[] DateFormats =
    {
        "yyyy-MM-dd", "yyyy-M-d", "yyyy/M/d",
        "d/M/yyyy", "d-M-yyyy", "d.M.yyyy",
        "M/d/yyyy", "M-d-yyyy", "M.d.yyyy",
    };

    private static DateTime? GetDate(IXLRow row, Dictionary<string, int> map, DateTime? reportDate)
    {
        if (!map.TryGetValue("date", out var col)) return null;
        var cell = row.Cell(col);
        if (cell.IsEmpty()) return null;

        DateTime? date = null;
        if (cell.DataType == XLDataType.DateTime)
            date = cell.GetDateTime();
        else if (DateTime.TryParseExact(cell.GetString().Trim().Replace('\\', '/'), DateFormats,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            date = dt;

        return date.HasValue ? RejectFutureDate(date.Value, reportDate) : null;
    }

    // A dated daily report can't contain transactions after its own date; rows dated later carry a
    // data-entry/locale error, so their date is rejected (null) and they drop out of period views
    // instead of being placed in a period they can't belong to.
    private static DateTime? RejectFutureDate(DateTime date, DateTime? reportDate)
    {
        if (reportDate is not { } bound) return date;
        return date <= bound ? date : null;
    }

    // Leading yyyyMMdd token in the report file name (NSO daily-report convention); null if absent.
    private static DateTime? ReportDateFromFileName(string path)
    {
        var match = Regex.Match(Path.GetFileName(path), @"^(\d{8})");
        return match.Success && DateTime.TryParseExact(match.Value, "yyyyMMdd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;
    }

    private static string GetRowSummary(IXLRow row, Dictionary<string, int> colMap)
    {
        var parts = new List<string>();
        foreach (var (key, col) in colMap)
        {
            var val = row.Cell(col).GetString().Trim();
            if (!string.IsNullOrWhiteSpace(val))
                parts.Add($"{key}={val}");
        }
        return string.Join(" | ", parts);
    }
}
