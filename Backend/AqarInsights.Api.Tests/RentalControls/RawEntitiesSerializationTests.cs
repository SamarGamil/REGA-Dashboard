using System.Text.Encodings.Web;
using System.Text.Json;
using AqarInsights.Api.Models.RentalControls.Raw;
using AqarInsights.Api.Services;

namespace AqarInsights.Api.Tests.RentalControls;

public class RawEntitiesSerializationTests
{
    [Fact]
    public void Serializes_camelCase_and_arabic()
    {
        var city = new CityRecord { Id = "riyadh", Name = "الرياض", Cagr = 0.097, Path = "متسارع" };
        var json = JsonSerializer.Serialize(city, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        Assert.Contains("\"path\":\"متسارع\"", json);
        Assert.Contains("الرياض", json);
    }

    [Fact]
    public void Legacy_single_media_object_reads_as_a_one_month_list()
    {
        const string json = """{"cityId":"riyadh","media":{"available":true,"overallSentiment":"سلبي","positivePct":4}}""";

        var city = JsonSerializer.Deserialize<CityRawData>(json, JsonDefaults.Read)!;

        var media = Assert.Single(city.Media!);
        Assert.Equal(("", 4), (media.Period, media.PositivePct));
        Assert.StartsWith("[", JsonSerializer.SerializeToNode(city, JsonDefaults.Read)!["media"]!.ToJsonString());
    }
}
