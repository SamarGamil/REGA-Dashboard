using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AqarInsights.Api.Models.ForeignOwnership;
using AqarInsights.Api.Services;
using AqarInsights.Api.Services.ForeignOwnership;

namespace AqarInsights.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/filters")]
[Produces("application/json")]
public sealed class FiltersController(IFilterOptionsService filterOptions) : ControllerBase
{
    private readonly IFilterOptionsService _filterOptions = filterOptions;

    [HttpGet]
    [ProducesResponseType<FilterOptions>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public IActionResult GetFilterOptions()
    {
        var options = _filterOptions.GetOptions();
        if (options is null)
            return Problem(detail: "Filter options not loaded", statusCode: StatusCodes.Status404NotFound);
        return Ok(options);
    }
}
