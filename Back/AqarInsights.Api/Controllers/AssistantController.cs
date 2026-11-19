using System.Text;
using System.Text.Json;
using AqarInsights.Api.Models.Assistant;
using AqarInsights.Api.Services.Assistant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqarInsights.Api.Controllers;

/// <summary>"تحدث مع البيانات" / Talk to Data endpoints. The answer streams as Server-Sent Events.
/// When the engine is not configured the client receives 503 and shows an unavailable state.</summary>
[Authorize]
[ApiController]
[Route("api/assistant")]
public sealed class AssistantController(
    IAssistantService assistant,
    ILogger<AssistantController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions EventJson = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [HttpGet("status")]
    [ProducesResponseType<AssistantStatus>(StatusCodes.Status200OK)]
    public ActionResult<AssistantStatus> Status() => Ok(assistant.Status);

    [HttpPost("ask")]
    public async Task Ask([FromBody] AssistantAskRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await WriteJsonAsync(new { error = "Question is required." }, ct);
            return;
        }

        if (!assistant.Status.Enabled)
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await WriteJsonAsync(new { error = "assistant_unavailable" }, ct);
            return;
        }

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        try
        {
            await foreach (var delta in assistant.StreamAsync(request, ct))
                await WriteEventAsync(new { delta }, ct);
            await WriteEventAsync(new { done = true }, ct);
        }
        catch (OperationCanceledException) { }
        catch (AssistantUnavailableException ex)
        {
            logger.LogWarning(ex, "Assistant became unavailable mid-request.");
            await WriteEventAsync(new { error = "assistant_unavailable" }, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Assistant streaming failed.");
            await WriteEventAsync(new { error = "assistant_error" }, ct);
        }
    }

    private async Task WriteEventAsync(object payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload, EventJson);
        await Response.WriteAsync($"data: {json}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }

    private async Task WriteJsonAsync(object payload, CancellationToken ct)
    {
        Response.ContentType = "application/json";
        await Response.WriteAsync(JsonSerializer.Serialize(payload, EventJson), Encoding.UTF8, ct);
    }
}
