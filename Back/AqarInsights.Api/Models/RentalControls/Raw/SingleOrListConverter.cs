using System.Text.Json;
using System.Text.Json.Serialization;

namespace AqarInsights.Api.Models.RentalControls.Raw;

// Reads either a JSON array or a single object (legacy shape) into a list; always writes an array.
public sealed class SingleOrListConverter<T> : JsonConverter<List<T>>
{
    public override List<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var single = JsonSerializer.Deserialize<T>(ref reader, options);
            return single is null ? new List<T>() : new List<T> { single };
        }
        return JsonSerializer.Deserialize<List<T>>(ref reader, options);
    }

    public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, options);
}
