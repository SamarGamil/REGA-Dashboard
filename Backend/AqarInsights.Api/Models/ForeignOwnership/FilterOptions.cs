using System.Text.Json.Serialization;

namespace AqarInsights.Api.Models.ForeignOwnership;

public class FilterOptions
{
    [JsonPropertyName("geography")]
    public List<GeographyNode> Geography { get; set; } = new();

    [JsonPropertyName("nationalities")]
    public List<string> Nationalities { get; set; } = new();

    [JsonPropertyName("propertyTypes")]
    public List<string> PropertyTypes { get; set; } = new();

    [JsonPropertyName("transactionTypes")]
    public List<string> TransactionTypes { get; set; } = new();

    [JsonPropertyName("ownerTypes")]
    public List<string> OwnerTypes { get; set; } = new();

    [JsonPropertyName("minDate")]
    public string? MinDate { get; set; }

    [JsonPropertyName("maxDate")]
    public string? MaxDate { get; set; }
}

public class GeographyNode
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("children")]
    public List<GeographyNode> Children { get; set; } = new();
}
