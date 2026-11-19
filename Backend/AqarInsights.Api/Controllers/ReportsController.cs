using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Models.RentalControls.Snapshots;
using AqarInsights.Api.Services.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Snapshots;

namespace AqarInsights.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/reports")]
public sealed class ReportsController(
    INationalReportService reports,
    ICityReportService cityReports,
    IReportHistoryStore history,
    IRentalControlsPeriodService periods) : ControllerBase
{
    private readonly INationalReportService _reports = reports;
    private readonly ICityReportService _cityReports = cityReports;
    private readonly IReportHistoryStore _history = history;
    private readonly IRentalControlsPeriodService _periods = periods;

    // Months and quarters with stored data, newest first.
    [HttpGet("periods")]
    [ProducesResponseType<ReportingPeriodsResponse>(StatusCodes.Status200OK)]
    public IActionResult GetPeriods() => Ok(_periods.GetPeriods());

    // period: yyyy-MM (monthly) or yyyy-Qn (quarterly); omitted = current quarter.
    [HttpPost("national")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNational([FromQuery] string format = "pdf", [FromQuery] bool record = true, [FromQuery] string? period = null)
    {
        if (ParseFormat(format) is not { } parsed)
            return Problem(detail: $"Unsupported format '{format}'. Use 'pdf' or 'word'.", statusCode: StatusCodes.Status400BadRequest);

        var data = await _periods.ResolveAsync(period, HttpContext?.RequestAborted ?? default);
        if (data is null) return UnknownPeriod(period);

        var file = _reports.GenerateNational(data, parsed);

        // Re-downloads (record=false) reuse an existing history entry, so don't log a new one.
        if (record)
        {
            await _history.AppendAsync(new ReportHistoryRecord
            {
                Id = Guid.NewGuid().ToString(),
                Name = "تقرير الضوابط الإيجارية الوطني",
                Type = "التقرير الوطني",
                Scope = "جميع المدن",
                Period = data.Label,
                PeriodId = NullIfEmpty(data.Id),
                Format = parsed == ReportFormat.Word ? "word" : "pdf",
                Status = "ready",
                FileName = file.FileName,
                RequestedBy = User?.Identity?.Name,
                RequestedAt = DateTimeOffset.UtcNow,
            });
        }

        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpPost("city/{cityId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCity(string cityId, [FromQuery] string format = "pdf", [FromQuery] bool record = true, [FromQuery] string? period = null)
    {
        if (ParseFormat(format) is not { } parsed)
            return Problem(detail: $"Unsupported format '{format}'. Use 'pdf' or 'word'.", statusCode: StatusCodes.Status400BadRequest);

        var data = await _periods.ResolveAsync(period, HttpContext?.RequestAborted ?? default);
        if (data is null) return UnknownPeriod(period);

        var file = _cityReports.GenerateCity(data, cityId, parsed);
        if (file is null) return Problem(detail: $"No report data for city '{cityId}'.", statusCode: StatusCodes.Status404NotFound);

        if (record)
        {
            var cityName = data.Aggregation.GetCity(cityId)?.CityName ?? cityId;
            await _history.AppendAsync(new ReportHistoryRecord
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"تقرير الضوابط الإيجارية – {cityName}",
                Type = "تقرير مدينة",
                Scope = cityName,
                Period = data.Label,
                PeriodId = NullIfEmpty(data.Id),
                Format = parsed == ReportFormat.Word ? "word" : "pdf",
                Status = "ready",
                FileName = file.FileName,
                RequestedBy = User?.Identity?.Name,
                RequestedAt = DateTimeOffset.UtcNow,
            });
        }

        return File(file.Content, file.ContentType, file.FileName);
    }

    // Entries recorded before period selection have no PeriodId; it is recovered from their period label (e.g.
    // "الربع الثاني 2026" → 2026-Q2) so a re-download regenerates the report's own period, not the current one.
    [HttpGet("history")]
    [ProducesResponseType<IEnumerable<ReportHistoryRecord>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory()
    {
        var items = await _history.GetAllAsync();
        var available = _periods.GetPeriods();
        var idByLabel = available.Monthly.Concat(available.Quarterly)
            .GroupBy(p => p.Label.Trim()).ToDictionary(g => g.Key, g => g.First().Id);
        return Ok(items
            .Select(r => r.PeriodId is null && idByLabel.TryGetValue(r.Period.Trim(), out var id) ? WithPeriodId(r, id) : r)
            .OrderByDescending(r => r.RequestedAt));
    }

    private static ReportHistoryRecord WithPeriodId(ReportHistoryRecord r, string periodId) => new()
    {
        Id = r.Id, Name = r.Name, Type = r.Type, Scope = r.Scope, Period = r.Period, PeriodId = periodId,
        Format = r.Format, Status = r.Status, FileName = r.FileName, RequestedBy = r.RequestedBy, RequestedAt = r.RequestedAt,
    };

    private ObjectResult UnknownPeriod(string? period)
        => Problem(detail: $"No rental-controls data for period '{period}'.", statusCode: StatusCodes.Status404NotFound);

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static ReportFormat? ParseFormat(string? format) => format?.ToLowerInvariant() switch
    {
        "pdf" => ReportFormat.Pdf,
        "word" or "docx" => ReportFormat.Word,
        _ => null,
    };
}
