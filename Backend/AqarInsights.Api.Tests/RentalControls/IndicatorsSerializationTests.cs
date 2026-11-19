using System.Text.Encodings.Web;
using System.Text.Json;
using AqarInsights.Api.Models.RentalControls;

namespace AqarInsights.Api.Tests.RentalControls;

public class IndicatorsSerializationTests
{
    [Fact]
    public void Serializes_camelCase_and_nested_shapes()
    {
        var data = new IndicatorsData
        {
            CurrentPeriodIndicators = { new IndicatorPanel { Id = "housing-burden", Title = "العبء", Cities = { new CityIndicatorValue { CityId = "riyadh", Value = 35, DisplayValue = "35%", Status = "متجاوز", SortOrder = 1 } } } },
        };
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        Assert.Contains("\"currentPeriodIndicators\"", json);
        Assert.Contains("\"displayValue\":\"35%\"", json);
        Assert.Contains("العبء", json);
    }
}
