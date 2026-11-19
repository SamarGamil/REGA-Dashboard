using AqarInsights.Api.Models.Assistant;

namespace AqarInsights.Api.Services.Assistant;

/// <summary>The "Talk to Data" engine contract. Streams the answer token-by-token.</summary>
public interface IAssistantService
{
    AssistantStatus Status { get; }
    IAsyncEnumerable<string> StreamAsync(AssistantAskRequest request, CancellationToken ct);
}

/// <summary>Raised when the engine cannot serve a request (not configured / no data).</summary>
public sealed class AssistantUnavailableException(string reason) : Exception(reason);
