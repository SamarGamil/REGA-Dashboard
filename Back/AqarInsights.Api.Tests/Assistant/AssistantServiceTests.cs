using System.Net;
using System.Text;
using AqarInsights.Api.Models.Assistant;
using AqarInsights.Api.Services.Assistant;
using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Services.RentalControls;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AqarInsights.Api.Tests.Assistant;

public class AssistantServiceTests
{
    private static FakeRentalControlsAgg Agg() => new()
    {
        Market = new MarketAnalysisData
        {
            LastQuarter = "2026-Q2",
            Cities = { new CityMarketMetric { Id = "riyadh", Name = "الرياض" } },
        },
    };

    private sealed class StubHandler(string body, string mediaType = "text/event-stream") : HttpMessageHandler
    {
        public string? LastRequestBody;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, mediaType),
            };
        }
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler);
    }

    private static AssistantSettings Configured(bool disableThinking = false) => new()
    {
        BaseUrl = "https://api.groq.com/openai/v1",
        ApiKey = "test-key",
        Model = "llama-3.3-70b-versatile",
        DisableThinking = disableThinking,
    };

    [Fact]
    public void Status_disabled_when_not_configured()
    {
        var svc = new AssistantService(new StubFactory(new StubHandler("")), Options.Create(new AssistantSettings()),
            new AssistantContextBuilder(Agg(), new FakeForeignAgg()), NullLogger<AssistantService>.Instance);
        Assert.False(svc.Status.Enabled);
    }

    [Fact]
    public async Task StreamAsync_yields_deltas_from_sse()
    {
        const string sse =
            "data: {\"choices\":[{\"delta\":{\"content\":\"مرحبا\"}}]}\n\n" +
            "data: {\"choices\":[{\"delta\":{\"content\":\" بك\"}}]}\n\n" +
            "data: [DONE]\n\n";
        var svc = new AssistantService(new StubFactory(new StubHandler(sse)), Options.Create(Configured()),
            new AssistantContextBuilder(Agg(), new FakeForeignAgg()), NullLogger<AssistantService>.Instance);

        var parts = new List<string>();
        await foreach (var d in svc.StreamAsync(new AssistantAskRequest { Question = "أهلا" }, CancellationToken.None))
            parts.Add(d);

        Assert.Equal(new[] { "مرحبا", " بك" }, parts);
    }

    [Fact]
    public async Task StreamAsync_yields_message_content_when_provider_ignores_streaming()
    {
        // A gateway that ignores "stream": true answers with one JSON body instead of SSE.
        const string json = "{\"choices\":[{\"message\":{\"content\":\"مرحبا بك\"}}]}";
        var svc = new AssistantService(new StubFactory(new StubHandler(json, "application/json")), Options.Create(Configured()),
            new AssistantContextBuilder(Agg(), new FakeForeignAgg()), NullLogger<AssistantService>.Instance);

        var parts = new List<string>();
        await foreach (var d in svc.StreamAsync(new AssistantAskRequest { Question = "أهلا" }, CancellationToken.None))
            parts.Add(d);

        Assert.Equal(new[] { "مرحبا بك" }, parts);
    }

    [Fact]
    public async Task StreamAsync_sends_thinking_switch_only_when_disabled()
    {
        const string sse = "data: {\"choices\":[{\"delta\":{\"content\":\"ok\"}}]}\n\ndata: [DONE]\n\n";

        var on = new StubHandler(sse);
        var svcOn = new AssistantService(new StubFactory(on), Options.Create(Configured(disableThinking: true)),
            new AssistantContextBuilder(Agg(), new FakeForeignAgg()), NullLogger<AssistantService>.Instance);
        await foreach (var _ in svcOn.StreamAsync(new AssistantAskRequest { Question = "أهلا" }, CancellationToken.None)) { }
        Assert.Contains("\"chat_template_kwargs\":{\"enable_thinking\":false}", on.LastRequestBody);

        var off = new StubHandler(sse);
        var svcOff = new AssistantService(new StubFactory(off), Options.Create(Configured()),
            new AssistantContextBuilder(Agg(), new FakeForeignAgg()), NullLogger<AssistantService>.Instance);
        await foreach (var _ in svcOff.StreamAsync(new AssistantAskRequest { Question = "أهلا" }, CancellationToken.None)) { }
        Assert.DoesNotContain("chat_template_kwargs", off.LastRequestBody);
    }

    [Fact]
    public async Task StreamAsync_throws_when_not_configured()
    {
        var svc = new AssistantService(new StubFactory(new StubHandler("")), Options.Create(new AssistantSettings()),
            new AssistantContextBuilder(Agg(), new FakeForeignAgg()), NullLogger<AssistantService>.Instance);

        await Assert.ThrowsAsync<AssistantUnavailableException>(async () =>
        {
            await foreach (var _ in svc.StreamAsync(new AssistantAskRequest { Question = "x" }, CancellationToken.None)) { }
        });
    }
}
