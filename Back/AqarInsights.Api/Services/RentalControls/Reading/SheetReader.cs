using ClosedXML.Excel;
using AqarInsights.Api.Utilities;

namespace AqarInsights.Api.Services.RentalControls.Reading;

public sealed class RentalControlsReadException : Exception
{
    public RentalControlsReadException(string message) : base(message) { }
}

public sealed class SheetReader
{
    private readonly Dictionary<string, int> _headers;   // normalized header text -> column index
    private SheetReader(Dictionary<string, int> headers) => _headers = headers;

    public static IXLWorksheet Sheet(IXLWorkbook wb, params string[] nameContains)
    {
        var needles = nameContains.Select(ArabicNormalizer.Normalize).ToArray();
        var ws = wb.Worksheets.FirstOrDefault(w =>
        {
            var n = ArabicNormalizer.Normalize(w.Name);
            return needles.All(x => n.Contains(x));
        });
        return ws ?? throw new RentalControlsReadException($"Worksheet matching [{string.Join(", ", nameContains)}] not found");
    }

    public static SheetReader ForHeader(IXLWorksheet sheet, int headerRow)
    {
        var map = new Dictionary<string, int>();
        foreach (var c in sheet.Row(headerRow).CellsUsed())
        {
            var key = ArabicNormalizer.Normalize(c.GetString());
            if (key.Length > 0 && !map.ContainsKey(key)) map[key] = c.Address.ColumnNumber;
        }
        return new SheetReader(map);
    }

    public int Col(params string[] headerContains)
        => TryCol(headerContains) ?? throw new RentalControlsReadException($"Header matching [{string.Join(", ", headerContains)}] not found");

    public int? TryCol(params string[] headerContains)
    {
        var needles = headerContains.Select(ArabicNormalizer.Normalize).ToArray();
        foreach (var (key, col) in _headers)
            if (needles.All(key.Contains)) return col;
        return null;
    }
}
