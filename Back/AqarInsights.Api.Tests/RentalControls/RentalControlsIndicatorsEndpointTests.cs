using AqarInsights.Api.Controllers;
using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Services.RentalControls;
using Microsoft.AspNetCore.Mvc;

namespace AqarInsights.Api.Tests.RentalControls;

public class RentalControlsIndicatorsEndpointTests
{
    [Fact]
    public async Task Returns_ok_when_loaded()
    {
        var ctrl = new RentalControlsController(new FakeRentalControlsAgg { Indicators = new IndicatorsData() }, new NoPeriods());
        Assert.IsType<OkObjectResult>(await ctrl.GetIndicators());
    }

    [Fact]
    public async Task Returns_ok_always()
        => Assert.IsType<OkObjectResult>(await new RentalControlsController(new FakeRentalControlsAgg(), new NoPeriods()).GetIndicators());
}
