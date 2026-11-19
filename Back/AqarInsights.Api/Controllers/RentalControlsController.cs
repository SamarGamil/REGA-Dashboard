using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AqarInsights.Api.Models;
using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Services.RentalControls;
using AqarInsights.Api.Services.RentalControls.Projections;
using AqarInsights.Api.Services.RentalControls.Snapshots;

namespace AqarInsights.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/rental-controls")]
[Produces("application/json")]
public sealed class RentalControlsController(IRentalControlsAggregationService agg, IRentalControlsPeriodService periods) : ControllerBase
{
    private readonly IRentalControlsAggregationService _agg = agg;
    private readonly IRentalControlsPeriodService _periods = periods;

    // period (optional, every endpoint): yyyy-MM or yyyy-Qn as listed by GET /api/reports/periods; omitted = the live
    // quarter (same as ?period=<live quarter>). A month is its quarter's data with that month's monthly values.
    [HttpGet("overview")]
    [ProducesResponseType<OverviewData>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOverview([FromQuery] string? period = null)
        => await ViewAsync(period) is { } view ? Ok(view.Aggregation.GetOverview()) : UnknownPeriod(period);

    [HttpGet("indicators")]
    [ProducesResponseType<IndicatorsData>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetIndicators([FromQuery] string? period = null)
        => await ViewAsync(period) is { } view
            ? Ok(PeriodValueMask.Apply(view.Aggregation.GetIndicators(), view.Scope))
            : UnknownPeriod(period);

    [HttpGet("cities")]
    [ProducesResponseType<IReadOnlyList<CityListItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCities([FromQuery] string? period = null)
        => await ViewAsync(period) is { } view ? Ok(view.Aggregation.GetCityIndex()) : UnknownPeriod(period);

    [HttpGet("cities/{cityId}")]
    [ProducesResponseType<CityAnalysisData>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCity(string cityId, [FromQuery] string? period = null)
    {
        if (await ViewAsync(period) is not { } view) return UnknownPeriod(period);
        var city = view.Aggregation.GetCity(cityId);
        if (city is null) return Problem(detail: $"City '{cityId}' not found", statusCode: StatusCodes.Status404NotFound);
        return Ok(PeriodValueMask.Apply(city, view.Scope));
    }

    [HttpGet("market-analysis")]
    [ProducesResponseType<MarketAnalysisData>(StatusCodes.Status200OK)]
    public IActionResult GetMarketAnalysis() => Ok(_agg.GetMarketAnalysis());

    [HttpGet("reporting-period")]
    [ProducesResponseType<ReportingPeriodResponse>(StatusCodes.Status200OK)]
    public IActionResult GetReportingPeriod() => Ok(new ReportingPeriodResponse(_agg.GetMarketAnalysis().LastQuarter, LastUpdated: ""));

    private sealed record PeriodView(IRentalControlsAggregationService Aggregation, PeriodScope Scope);

    // No period: the live quarter's view (a period service without data serves the injected aggregation as is).
    private async Task<PeriodView?> ViewAsync(string? period)
    {
        var data = await _periods.ResolveAsync(period, HttpContext?.RequestAborted ?? default);
        if (data is not null) return new PeriodView(data.Aggregation, data.Scope);
        return string.IsNullOrWhiteSpace(period) ? new PeriodView(_agg, PeriodScope.None) : null;
    }

    private ObjectResult UnknownPeriod(string? period)
        => Problem(detail: $"No rental-controls data for period '{period}'.", statusCode: StatusCodes.Status404NotFound);
}
