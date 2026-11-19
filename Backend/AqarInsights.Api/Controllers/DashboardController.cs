using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AqarInsights.Api.Models;
using AqarInsights.Api.Models.ForeignOwnership;
using AqarInsights.Api.Services;
using AqarInsights.Api.Services.ForeignOwnership;

namespace AqarInsights.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/dashboard")]
[Produces("application/json")]
public sealed class DashboardController(IAggregationService aggregation) : ControllerBase
{
    private readonly IAggregationService _aggregation = aggregation;

    [HttpGet("summary")]
    [ProducesResponseType<SummaryResponse>(StatusCodes.Status200OK)]
    public IActionResult GetSummary([FromQuery] DashboardFilterParams filters) => Ok(_aggregation.GetSummary(filters));

    [HttpGet("regions")]
    [ProducesResponseType<RegionsResponse>(StatusCodes.Status200OK)]
    public IActionResult GetRegions([FromQuery] DashboardFilterParams filters) => Ok(_aggregation.GetRegions(filters));

    [HttpGet("nationalities")]
    [ProducesResponseType<NationalitiesResponse>(StatusCodes.Status200OK)]
    public IActionResult GetNationalities([FromQuery] DashboardFilterParams filters) => Ok(_aggregation.GetNationalities(filters));

    [HttpGet("properties")]
    [ProducesResponseType<PropertiesResponse>(StatusCodes.Status200OK)]
    public IActionResult GetProperties([FromQuery] DashboardFilterParams filters) => Ok(_aggregation.GetProperties(filters));

    [HttpGet("areas")]
    [ProducesResponseType<AreasResponse>(StatusCodes.Status200OK)]
    public IActionResult GetAreas([FromQuery] DashboardFilterParams filters) => Ok(_aggregation.GetAreas(filters));

    [HttpGet("financial")]
    [ProducesResponseType<FinancialResponse>(StatusCodes.Status200OK)]
    public IActionResult GetFinancial([FromQuery] DashboardFilterParams filters) => Ok(_aggregation.GetFinancial(filters));

    [HttpGet("strategic")]
    [ProducesResponseType<StrategicResponse>(StatusCodes.Status200OK)]
    public IActionResult GetStrategic([FromQuery] DashboardFilterParams filters) => Ok(_aggregation.GetStrategic(filters));

    [HttpGet("reporting-period")]
    [ProducesResponseType<ReportingPeriodResponse>(StatusCodes.Status200OK)]
    public IActionResult GetReportingPeriod() => Ok(new ReportingPeriodResponse(_aggregation.GetReportingPeriod(), _aggregation.GetReportingMonth()));
}
