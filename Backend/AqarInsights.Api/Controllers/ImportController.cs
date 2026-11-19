using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AqarInsights.Api.Models;
using AqarInsights.Api.Services;
using AqarInsights.Api.Services.ForeignOwnership;

namespace AqarInsights.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/import")]
[Produces("application/json")]
public sealed class ImportController(
    IExcelConverterService converter,
    IDataLoaderService dataLoader,
    IAggregationService aggregation,
    IFilterOptionsService filterOptions,
    ILogger<ImportController> logger) : ControllerBase
{
    private readonly IExcelConverterService _converter = converter;
    private readonly IDataLoaderService _dataLoader = dataLoader;
    private readonly IAggregationService _aggregation = aggregation;
    private readonly IFilterOptionsService _filterOptions = filterOptions;
    private readonly ILogger<ImportController> _logger = logger;

    [HttpPost]
    [RequestSizeLimit(30_000_000)]
    [ProducesResponseType<ConversionReport>(StatusCodes.Status200OK)]
    [ProducesResponseType<ConversionReport>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return Problem(detail: "No file provided", statusCode: StatusCodes.Status400BadRequest);

        if (file.Length > 30_000_000)
            return Problem(detail: "File exceeds the 30 MB limit", statusCode: StatusCodes.Status400BadRequest);

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return Problem(detail: "Only .xlsx files are supported", statusCode: StatusCodes.Status400BadRequest);

        // Save where the converter reads from so the upload becomes the latest file it picks.
        var dataDir = _converter.InputDir;
        Directory.CreateDirectory(dataDir);
        var filePath = Path.Combine(dataDir, Path.GetFileName(file.FileName));

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        _logger.LogInformation("File uploaded: {Path}", filePath);

        // Run conversion
        var report = await _converter.ConvertLatestAsync();

        if (!report.Success)
            return UnprocessableEntity(report);

        // Reload data and recompute
        await _dataLoader.LoadAsync();
        _aggregation.Recompute();
        await _filterOptions.LoadAsync();

        return Ok(report);
    }

    [HttpGet("inspect")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Inspect()
    {
        var result = _converter.InspectFile();
        return Ok(result.Select(r => new { sheet = r.SheetName, columns = r.Columns }));
    }
}
