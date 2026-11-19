using AqarInsights.Api.Controllers;
using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Services.RentalControls;
using Microsoft.AspNetCore.Mvc;

namespace AqarInsights.Api.Tests.RentalControls;

public class RentalControlsControllerTests
{
    [Fact]
    public async Task Overview_returns_ok_when_loaded()
    {
        var agg = new FakeRentalControlsAgg { Overview = new OverviewData { Cities = { new CityData { Id = "riyadh" } } } };
        var result = await new RentalControlsController(agg, new NoPeriods()).GetOverview();
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<OverviewData>(ok.Value);
    }

    [Fact]
    public async Task Overview_always_returns_ok()
    {
        var result = await new RentalControlsController(new FakeRentalControlsAgg(), new NoPeriods()).GetOverview();
        Assert.IsType<OkObjectResult>(result);
    }
}
