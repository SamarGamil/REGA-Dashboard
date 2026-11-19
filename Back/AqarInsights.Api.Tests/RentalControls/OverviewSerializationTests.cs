using System.Text.Encodings.Web;
using System.Text.Json;
using AqarInsights.Api.Models.RentalControls;

namespace AqarInsights.Api.Tests.RentalControls;

public class OverviewSerializationTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    };

    [Fact]
    public void Serializes_camelCase_and_preserves_arabic()
    {
        var data = new OverviewData
        {
            Cities = { new CityData { Id = "riyadh", Name = "الرياض", Cagr = "9.7%", Priority = 1 } },
        };

        var json = JsonSerializer.Serialize(data, Options);

        Assert.Contains("\"cities\"", json);
        Assert.Contains("الرياض", json);          // not \u-escaped
        Assert.Contains("\"cagr\": \"9.7%\"", json);
    }
}
