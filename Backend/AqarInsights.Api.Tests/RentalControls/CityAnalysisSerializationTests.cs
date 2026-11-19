using System.Text.Encodings.Web;
using System.Text.Json;
using AqarInsights.Api.Models.RentalControls;

namespace AqarInsights.Api.Tests.RentalControls;

public class CityAnalysisSerializationTests
{
    [Fact]
    public void Serializes_camelCase_arabic_and_tabs()
    {
        var data = new CityAnalysisData
        {
            CityName = "الرياض",
            MarketAnalysisTabs = { new MarketAnalysisTab {
                Id = "rentals", Label = "الإيجارات", ChartType = "bar", Stacked = true,
                Series = { new TabSeries { Name = "شقة", Data = { 1, 2, 3 } } },
                Categories = { "الربع الأول 2025" },
                TableColumns = { "الفترة", "شقة" },
                TableRows = { new List<string> { "الربع الأول 2025", "1" } },
            } },
        };
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        Assert.Contains("\"marketAnalysisTabs\"", json);
        Assert.Contains("الرياض", json);
        Assert.Contains("\"chartType\":\"bar\"", json);
        Assert.Contains("\"series\":[", json);
    }
}
