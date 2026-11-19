using System.Text.Json;
using ClosedXML.Excel;

// Generates the media-monitoring source workbook (الرصد الإعلامي.xlsx) from the
// PPT-extracted JSON (mm-data.json) plus the per-city chart/table metadata below.

var toolDir = AppContext.BaseDirectory;
// Walk up to the tools/mm-extract folder (where mm-data.json lives).
var scriptDir = Path.GetFullPath(Path.Combine(toolDir, "..", "..", "..", ".."));
var jsonPath = Path.Combine(scriptDir, "tools", "mm-extract", "mm-data.json");
if (!File.Exists(jsonPath))
{
    // Fallback: allow running with an explicit path argument.
    jsonPath = args.Length > 0 ? args[0] : jsonPath;
}

var outPath = args.Length > 1
    ? args[1]
    : Path.Combine(scriptDir, "AqarInsights.Api", "Data", "rental-controls-source", "الرصد الإعلامي.xlsx");

Console.WriteLine($"Reading  {jsonPath}");
Console.WriteLine($"Writing  {outPath}");

var json = File.ReadAllText(jsonPath);
var content = JsonSerializer.Deserialize<Dictionary<string, CityContent>>(json, new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
}) ?? throw new InvalidOperationException("Failed to parse mm-data.json");

// Per-city metadata extracted from the PPT charts (sentiment %s), slide-26 comparison
// table (overall sentiment label), and detail-page discussion share.
var meta = new (string Id, bool Available, string Sentiment, int Pos, int Neu, int Neg, double Share)[]
{
    ("riyadh",  true, "سلبي",   4,  69, 27, 88.8),
    ("jeddah",  true, "متوازن", 24, 67, 9,  5.3),
    ("makkah",  true, "سلبي",   24, 67, 9,  1.4),
    ("dammam",  true, "متوازن", 17, 66, 17, 1.4),
    ("hail",    true, "متوازن", 40, 40, 20, 1.2),
    ("najran",  true, "محايد",  0,  100, 0, 0.7),
    ("madinah", true, "متوازن", 0,  67, 33, 0.7),
    ("tabuk",   true, "محايد",  0,  100, 0, 0.2),
    ("sakaka",  true, "محايد",  0,  100, 0, 0.2),
};

using var wb = new XLWorkbook();

// Sheet 1: المدن — per-city summary row.
var citiesSheet = wb.Worksheets.Add("المدن");
citiesSheet.RightToLeft = true;
var headers = new[] { "cityId", "available", "overallSentiment", "positivePct", "neutralPct", "negativePct", "discussionSharePct" };
for (var c = 0; c < headers.Length; c++)
    citiesSheet.Cell(1, c + 1).Value = headers[c];
var row = 2;
foreach (var m in meta)
{
    citiesSheet.Cell(row, 1).Value = m.Id;
    citiesSheet.Cell(row, 2).Value = m.Available;
    citiesSheet.Cell(row, 3).Value = m.Sentiment;
    citiesSheet.Cell(row, 4).Value = m.Pos;
    citiesSheet.Cell(row, 5).Value = m.Neu;
    citiesSheet.Cell(row, 6).Value = m.Neg;
    citiesSheet.Cell(row, 7).Value = m.Share;
    row++;
}
citiesSheet.Row(1).Style.Font.Bold = true;
citiesSheet.Columns().AdjustToContents();

// Sheet 2: الموضوعات — one row per top topic.
var topicsSheet = wb.Worksheets.Add("الموضوعات");
topicsSheet.RightToLeft = true;
topicsSheet.Cell(1, 1).Value = "cityId";
topicsSheet.Cell(1, 2).Value = "topic";
row = 2;
foreach (var m in meta)
{
    if (!content.TryGetValue(m.Id, out var cc)) continue;
    foreach (var t in cc.Topics ?? Array.Empty<string>())
    {
        topicsSheet.Cell(row, 1).Value = m.Id;
        topicsSheet.Cell(row, 2).Value = t;
        row++;
    }
}
topicsSheet.Row(1).Style.Font.Bold = true;
topicsSheet.Columns().AdjustToContents();

// Sheet 3: الانطباعات — one row per impression bullet, typed positive/neutral/negative.
var impSheet = wb.Worksheets.Add("الانطباعات");
impSheet.RightToLeft = true;
impSheet.Cell(1, 1).Value = "cityId";
impSheet.Cell(1, 2).Value = "type";
impSheet.Cell(1, 3).Value = "text";
row = 2;
foreach (var m in meta)
{
    if (!content.TryGetValue(m.Id, out var cc)) continue;
    row = WriteImpressions(impSheet, row, m.Id, "positive", cc.Positive);
    row = WriteImpressions(impSheet, row, m.Id, "neutral", cc.Neutral);
    row = WriteImpressions(impSheet, row, m.Id, "negative", cc.Negative);
}
impSheet.Row(1).Style.Font.Bold = true;
impSheet.Column(3).Width = 100;
impSheet.Column(1).AdjustToContents();
impSheet.Column(2).AdjustToContents();

Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
wb.SaveAs(outPath);
Console.WriteLine("Done.");

static int WriteImpressions(IXLWorksheet sheet, int row, string cityId, string type, string[]? items)
{
    foreach (var text in items ?? Array.Empty<string>())
    {
        sheet.Cell(row, 1).Value = cityId;
        sheet.Cell(row, 2).Value = type;
        sheet.Cell(row, 3).Value = text;
        row++;
    }
    return row;
}

sealed class CityContent
{
    public string[]? Topics { get; set; }
    public string[]? Neutral { get; set; }
    public string[]? Positive { get; set; }
    public string[]? Negative { get; set; }
}
