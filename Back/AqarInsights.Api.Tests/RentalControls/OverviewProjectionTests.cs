using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Raw;
using AqarInsights.Api.Services.RentalControls.Projections;

namespace AqarInsights.Api.Tests.RentalControls;

public class OverviewProjectionTests
{
    [Fact]
    public void Build_SingleCity_MapsAllFields()
    {
        // Arrange
        var cities = new List<CityRecord>
        {
            new()
            {
                Id = "riyadh",
                Name = "الرياض",
                Cagr = 0.0968836,
                Path = "متسارع",
                Vacancy = 0.07,
                InvestmentReturn = 0.05,
                RentalIncrease = 0.09,
                RentToIncome = 0.15,
                Burden = 0.35,
                AssessmentLine1 = "الضوابط الايجارية مفعلة",
                AssessmentLine2 = "تتطلب المراجعة الدورية للضوابط",
            },
        };

        // Act
        var result = OverviewProjection.Build(cities);

        // Assert
        Assert.Single(result.Cities);
        var city = result.Cities[0];
        Assert.Equal("riyadh", city.Id);
        Assert.Equal("الرياض", city.Name);
        Assert.Equal("9.7%", city.Cagr);
        Assert.Equal("نمو متسارع", city.Classification);
        Assert.Equal("accelerated", city.ClassificationLabel);
        Assert.Equal("الضوابط مفعلة", city.OperationalStatus);
        Assert.Equal("7%", city.VacancyRate);
        Assert.Equal("5%", city.InvestmentReturn);
        Assert.Equal("+9%", city.ActualRentalGrowth);
        Assert.Equal("15%", city.RentToIncome);
        Assert.Equal("35%", city.HousingBurden);
        Assert.True(city.ImpactData.Available);
        Assert.Equal("تتطلب المراجعة الدورية للضوابط", city.ImpactData.Description);
        Assert.Equal("تتطلب المراجعة الدورية للضوابط", city.Recommendation);
        Assert.Equal(1, city.Priority);
    }

    [Fact]
    public void Build_MultipleCities_ComputesNationalSummary()
    {
        // Arrange
        var cities = new List<CityRecord>
        {
            new() { Id = "riyadh", Name = "الرياض", Path = "متسارع", AssessmentLine1 = "الضوابط الايجارية مفعلة", AssessmentLine2 = "" },
            new() { Id = "jeddah", Name = "جدة", Path = "مرتفع", AssessmentLine1 = "يتطلب دراسة تفصيلية", AssessmentLine2 = "" },
            new() { Id = "dammam", Name = "الدمام", Path = "استباقي", AssessmentLine1 = "لا إجراء", AssessmentLine2 = "" },
        };

        // Act
        var result = OverviewProjection.Build(cities);

        // Assert
        Assert.Equal(1, result.NationalSummary.AcceleratedCities);
        Assert.Equal(1, result.NationalSummary.ElevatedCities);
        Assert.Equal(1, result.NationalSummary.SustainableCities);
        Assert.Equal(1, result.NationalSummary.RegulationActivated);
        Assert.Equal(1, result.NationalSummary.DetailedStudy);
        Assert.Equal(0, result.NationalSummary.ProactiveMonitoring);
        Assert.Equal(1, result.NationalSummary.RoutineMonitoring);
    }

    [Fact]
    public void Build_MultipleCities_ComputesOperationalStages()
    {
        // Arrange
        var cities = new List<CityRecord>
        {
            new() { Id = "riyadh", Name = "الرياض", Path = "متسارع", AssessmentLine1 = "الضوابط الايجارية مفعلة", AssessmentLine2 = "" },
            new() { Id = "jeddah", Name = "جدة", Path = "مرتفع", AssessmentLine1 = "يتطلب دراسة تفصيلية", AssessmentLine2 = "" },
        };

        // Act
        var result = OverviewProjection.Build(cities);

        // Assert
        Assert.Equal(4, result.OperationalStages.Count);
        Assert.Contains(result.OperationalStages, s => s.Key == "activated" && s.Count == 1);
        Assert.Contains(result.OperationalStages, s => s.Key == "detailed" && s.Count == 1);
        Assert.Contains(result.OperationalStages, s => s.Key == "noAction" && s.Count == 0);
        Assert.Contains(result.OperationalStages, s => s.Key == "proactive" && s.Count == 0);
    }

    [Fact]
    public void Build_MultipleCities_CreatesRecommendations()
    {
        // Arrange
        var cities = new List<CityRecord>
        {
            new() { Id = "riyadh", Name = "الرياض", Path = "متسارع", AssessmentLine1 = "مفعلة", AssessmentLine2 = "توصية الرياض" },
            new() { Id = "jeddah", Name = "جدة", Path = "مرتفع", AssessmentLine1 = "", AssessmentLine2 = "توصية جدة" },
        };

        // Act
        var result = OverviewProjection.Build(cities);

        // Assert
        Assert.Equal(2, result.Recommendations.Count);
        Assert.Contains(result.Recommendations, r => r.City == "الرياض" && r.Text == "توصية الرياض");
        Assert.Contains(result.Recommendations, r => r.City == "جدة" && r.Text == "توصية جدة");
    }

    [Fact]
    public void Build_Always_SetsClassificationLegend()
    {
        // Arrange
        var cities = new List<CityRecord> { new() { Id = "riyadh", Name = "الرياض", Path = "متسارع", AssessmentLine1 = "", AssessmentLine2 = "" } };

        // Act
        var result = OverviewProjection.Build(cities);

        // Assert
        Assert.Equal(3, result.ClassificationLegend.Count);
        Assert.Contains(result.ClassificationLegend, l => l.Key == "sustainable");
        Assert.Contains(result.ClassificationLegend, l => l.Key == "elevated");
        Assert.Contains(result.ClassificationLegend, l => l.Key == "accelerated");
    }

    [Fact]
    public void Build_NullInvestmentReturn_ReturnsEmDash()
    {
        // Arrange
        var cities = new List<CityRecord>
        {
            new() { Id = "riyadh", Name = "الرياض", Path = "متسارع", InvestmentReturn = null, AssessmentLine1 = "", AssessmentLine2 = "" },
        };

        // Act
        var result = OverviewProjection.Build(cities);

        // Assert
        Assert.Equal("—", result.Cities[0].InvestmentReturn);
    }

    // The overview's whole figure is the city's latest yield point rounded, wherever that point falls: Bahah's series
    // ends at Q4 2025 (5.54%) and the Q3 overview shows it as 6%. A point that rounds to another figure is not the
    // overview's, so the overview's whole figure stays.
    [Theory]
    [InlineData("2026-Q3", 5.54, "5.5%")]
    [InlineData("2025-Q4", 5.54, "5.5%")]
    [InlineData("2025-Q4", 6.61, "6%")]
    public void Yield_decimal_comes_from_the_city_series_latest_point_when_it_rounds_to_the_overview(string yieldPeriod, double point, string expected)
    {
        var cities = new List<CityRecord>
        {
            new() { Id = "bahah", Name = "الباحة", Path = "استباقي", InvestmentReturn = 0.06, AssessmentLine1 = "", AssessmentLine2 = "" },
        };
        var raw = new CityRawData
        {
            CityId = "bahah",
            RentalsSeries = { new() { Period = "2026-Q3" } },
            RoiSeries = { new() { Period = yieldPeriod, Values = { ["العائد"] = point } } },
        };

        Assert.Equal(expected, OverviewProjection.Build(cities, _ => raw).Cities[0].InvestmentReturn);
    }
}
