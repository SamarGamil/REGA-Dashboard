using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Raw;
using AqarInsights.Api.Services.RentalControls;
using AqarInsights.Api.Services.RentalControls.Store;

namespace AqarInsights.Api.Tests.RentalControls;

public class RentalControlsAggregationServiceTests
{
    [Fact]
    public void Recompute_WithSingleCity_ProducesCorrectAggregations()
    {
        // Arrange
        var fakeStore = new FakeRentalControlsStore();
        var service = new RentalControlsAggregationService(fakeStore);

        // Act
        service.Recompute();

        // Assert
        var overview = service.GetOverview();
        Assert.NotNull(overview);
        Assert.Single(overview.Cities);
        Assert.Equal("riyadh", overview.Cities[0].Id);

        var indicators = service.GetIndicators();
        Assert.NotNull(indicators);
        Assert.Equal(6, indicators.CurrentPeriodIndicators.Count);

        var cityIndex = service.GetCityIndex();
        Assert.NotNull(cityIndex);
        Assert.Single(cityIndex);
        Assert.Equal("riyadh", cityIndex[0].CityId);
        Assert.Equal("الرياض", cityIndex[0].CityName);
        Assert.NotEmpty(cityIndex[0].OperationalStatus);
        Assert.NotEmpty(cityIndex[0].Classification);
        Assert.NotEmpty(cityIndex[0].ClassificationColor);

        var riyadhCity = service.GetCity("riyadh");
        Assert.NotNull(riyadhCity);
        Assert.Equal("الرياض", riyadhCity.CityName);

        var missingCity = service.GetCity("xyz");
        Assert.Null(missingCity);
    }

    private class FakeRentalControlsStore : IRentalControlsStore
    {
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public IReadOnlyList<CityRecord> GetCities()
        {
            return new List<CityRecord>
            {
                new()
                {
                    Id = "riyadh",
                    Name = "الرياض",
                    Path = "الرياض|path=1|الكل",
                    Cagr = 5.5,
                    Vacancy = 3.2,
                    InvestmentReturn = 7.8,
                    RentalIncrease = 4.5,
                    RentToIncome = 25.0,
                    Burden = 28.0,
                    AssessmentLine1 = "مفعلة",
                    AssessmentLine2 = "تحتاج للمراقبة"
                }
            };
        }

        public IReadOnlyList<IndicatorSeriesPoint> GetIndicatorSeries()
        {
            return new List<IndicatorSeriesPoint>
            {
                new() { CityId = "riyadh", Year = 2025, Period = 2, Indicator = "rentalChange", Value = 4.5 },
                new() { CityId = "riyadh", Year = 2026, Period = 2, Indicator = "rentalChange", Value = 5.0 }
            };
        }

        public IReadOnlyList<RawSupplyDemandGapCity> GetSupplyDemandGap() => new List<RawSupplyDemandGapCity>();

        public CityRawData? GetCityRaw(string cityId)
        {
            if (cityId == "riyadh")
            {
                return new CityRawData
                {
                    CityId = "riyadh",
                    Bullets = new List<string> { "نمو مستدام", "استقرار في الأسعار" },
                    MovementRows = new List<RawMovementRow>
                    {
                        new() { Indicator = "الزيادة الإيجارية", Level = "4.5%", Change = "+0.5%", Assessment = "ضمن الحد" }
                    },
                    RentalsSeries = new List<RawSeriesPoint>
                    {
                        new() { Period = "Q1 2026", Values = new Dictionary<string, double> { { "total", 1000 }, { "apartment", 500 } } }
                    },
                    SalesSeries = new List<RawSeriesPoint>(),
                    RentIndexSeries = new List<RawSeriesPoint>(),
                    AffordabilitySeries = new List<RawSeriesPoint>(),
                    RoiSeries = new List<RawSeriesPoint>(),
                    IncreaseSeries = new List<RawSeriesPoint>()
                };
            }
            return null;
        }
    }
}
