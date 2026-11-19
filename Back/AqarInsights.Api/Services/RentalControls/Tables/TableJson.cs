using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AqarInsights.Api.Services.RentalControls.Tables;

/// <summary>How table rows are stored: camelCase, Arabic unescaped, nulls omitted, one compact row per line.</summary>
public static class TableJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // A JSON array with one compact row per line: readable diffs, and one line = one database row.
    public static string SerializeRows<T>(IEnumerable<T> rows)
    {
        var lines = rows.Select(r => JsonSerializer.Serialize(r, Options)).ToList();
        return lines.Count == 0 ? "[]\n" : "[\n" + string.Join(",\n", lines) + "\n]\n";
    }

    public static List<T> ReadRows<T>(string json) => JsonSerializer.Deserialize<List<T>>(json, Options) ?? [];
}
