using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Services.RentalControls;

namespace AqarInsights.Api.Tests.RentalControls;

public class CityAnalysisDeriverTests
{
    private static CityData Riyadh() => new()
    {
        Id = "riyadh", Name = "الرياض", OperationalStatus = "الضوابط مفعلة", ClassificationLabel = "accelerated",
        ActualRentalGrowth = "+9%", VacancyRate = "7%", InvestmentReturn = "5%", RentToIncome = "15%", HousingBurden = "35%",
        Recommendation = "الاستمرار في متابعة أثر الضوابط",
    };

    [Fact]
    public void Kpis_has_five_items()
        => Assert.Equal(5, CityAnalysisDeriver.Kpis(Riyadh()).Count);

    [Fact]
    public void Kpis_status_uses_configurable_thresholds()
    {
        var city = Riyadh(); // HousingBurden = "35%"

        // Default (BurdenMed = 35): 35 >= 35 → محايد
        var def = CityAnalysisDeriver.Kpis(city).Single(k => k.Label == "العبء السكني للدخل المحدود");
        Assert.Equal("محايد", def.Status);

        // Raise the neutral boundary above 35 → now below it → إيجابي
        var t = new KpiThresholdsOptions { BurdenMed = 36, BurdenHigh = 40 };
        var custom = CityAnalysisDeriver.Kpis(city, t).Single(k => k.Label == "العبء السكني للدخل المحدود");
        Assert.Equal("إيجابي", custom.Status);
    }

    [Fact]
    public void Journey_marks_current_stage()
    {
        var stages = CityAnalysisDeriver.Journey("الضوابط مفعلة");
        var current = stages.Single(s => s.IsCurrent);
        Assert.Equal("الضوابط مفعلة", current.Label);
        Assert.True(stages[0].IsCompleted);   // earlier stages completed
    }
}
