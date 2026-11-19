using AqarInsights.Api.Controllers;
using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Services.RentalControls;
using Microsoft.AspNetCore.Mvc;

namespace AqarInsights.Api.Tests.RentalControls;

public class RentalControlsCitiesEndpointTests
{
    private static FakeRentalControlsAgg Agg() => new()
    {
        CityIndex = { new() { CityId = "riyadh" } },
        CityLookup = id => id == "riyadh" ? new CityAnalysisData { CityName = "الرياض" } : null,
    };

    [Fact]
    public async Task Cities_returns_list()
        => Assert.IsType<OkObjectResult>(await new RentalControlsController(Agg(), new NoPeriods()).GetCities());

    [Fact]
    public async Task City_returns_404_for_unknown()
    {
        var r = Assert.IsType<ObjectResult>(await new RentalControlsController(Agg(), new NoPeriods()).GetCity("xyz"));
        Assert.Equal(404, r.StatusCode);
    }
}
