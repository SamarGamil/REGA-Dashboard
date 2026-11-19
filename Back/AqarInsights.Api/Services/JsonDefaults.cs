using System.Text.Json;

namespace AqarInsights.Api.Services;

// Shared read-side (deserialization) options for all JSON data files.
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Read = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
}
