using AqarInsights.Api.Models.Assistant;
using AqarInsights.Api.Services.Assistant;
using AqarInsights.Api.Controllers;
using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Services.RentalControls;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace AqarInsights.Api.Tests.Assistant;

public class AssistantControllerTests
{
    private sealed class FakeAssistant(bool enabled) : IAssistantService
    {
        public AssistantStatus Status => new(enabled, "groq", "llama-3.3-70b-versatile");
        public async IAsyncEnumerable<string> StreamAsync(AssistantAskRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            yield return "ok";
            await Task.CompletedTask;
        }
    }

    private static AssistantController Build(IAssistantService svc, out MemoryStream bodyStream)
    {
        bodyStream = new MemoryStream();
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = bodyStream;
        return new AssistantController(svc, NullLogger<AssistantController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = ctx },
        };
    }

    [Fact]
    public void Status_returns_ok()
    {
        var ctrl = Build(new FakeAssistant(enabled: true), out _);
        var result = ctrl.Status();
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task Ask_returns_400_for_empty_question()
    {
        var ctrl = Build(new FakeAssistant(enabled: true), out _);
        await ctrl.Ask(new AssistantAskRequest { Question = "  " }, CancellationToken.None);
        Assert.Equal(StatusCodes.Status400BadRequest, ctrl.Response.StatusCode);
    }

    [Fact]
    public async Task Ask_returns_503_when_engine_disabled()
    {
        var ctrl = Build(new FakeAssistant(enabled: false), out _);
        await ctrl.Ask(new AssistantAskRequest { Question = "أهلا" }, CancellationToken.None);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, ctrl.Response.StatusCode);
    }
}
