using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AqarInsights.Api.Models.Assistant;
using Microsoft.Extensions.Options;

namespace AqarInsights.Api.Services.Assistant;

/// <summary>Provider-agnostic client for any OpenAI-compatible /chat/completions endpoint with
/// SSE streaming. Answers are grounded via <see cref="AssistantContextBuilder"/>.</summary>
public sealed class AssistantService(
    IHttpClientFactory httpClientFactory,
    IOptions<AssistantSettings> options,
    AssistantContextBuilder context,
    ILogger<AssistantService> logger) : IAssistantService
{
    private readonly AssistantSettings _settings = options.Value;

    public AssistantStatus Status => new(
        Enabled: _settings.IsConfigured && context.HasData,
        Provider: DescribeProvider(_settings.BaseUrl),
        Model: _settings.Model);

    public async IAsyncEnumerable<string> StreamAsync(
        AssistantAskRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (!_settings.IsConfigured)
            throw new AssistantUnavailableException("The assistant engine is not configured.");
        if (!context.HasData)
            throw new AssistantUnavailableException("No dashboard dataset is loaded.");

        var systemPrompt = await context.BuildSystemPromptAsync(request.Lang, request.Product, request.Period, ct);
        var body = new ChatRequest
        {
            Model = _settings.Model,
            Temperature = _settings.Temperature,
            MaxTokens = _settings.MaxTokens,
            Stream = true,
            Messages = BuildMessages(request, systemPrompt),
            ChatTemplateKwargs = _settings.DisableThinking ? new() { ["enable_thinking"] = false } : null,
        };

        var http = httpClientFactory.CreateClient("assistant");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, CombineUrl(_settings.BaseUrl, "chat/completions"))
        {
            Content = new StringContent(JsonSerializer.Serialize(body, SerializerOptions), Encoding.UTF8, "application/json"),
        };
        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

        using var response = await http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await SafeReadAsync(response, ct);
            logger.LogError("Assistant provider returned {Status}: {Error}", (int)response.StatusCode, error);
            throw new AssistantUnavailableException($"Assistant provider error ({(int)response.StatusCode}).");
        }

        // Some OpenAI-compatible gateways ignore "stream" and return one JSON body instead of SSE.
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!string.Equals(mediaType, "text/event-stream", StringComparison.OrdinalIgnoreCase))
        {
            var text = ExtractDelta(await response.Content.ReadAsStringAsync(ct));
            if (string.IsNullOrEmpty(text))
                throw new AssistantUnavailableException("Assistant provider returned an unrecognized response.");
            yield return text;
            yield break;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        var sawContent = false;
        string? lastPayload = null;
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;

            var data = line["data:".Length..].Trim();
            if (data == "[DONE]") break;
            if (!sawContent) lastPayload = data;

            var delta = ExtractDelta(data);
            if (!string.IsNullOrEmpty(delta))
            {
                sawContent = true;
                yield return delta;
            }
        }
        // A stream with no recognizable content (e.g. a thinking model that spent the whole max_tokens budget on
        // reasoning_content, or an unexpected chunk shape) would otherwise fail silently; log the last chunk seen.
        if (!sawContent)
            logger.LogWarning("Assistant stream ended with no content; last payload: {Payload}",
                lastPayload is { Length: > 500 } ? lastPayload[..500] : lastPayload ?? "<none>");
    }

    private List<ChatMessage> BuildMessages(AssistantAskRequest request, string systemPrompt)
    {
        var messages = new List<ChatMessage> { new("system", systemPrompt) };
        foreach (var turn in request.History.TakeLast(6))
        {
            var role = turn.Role is "user" or "assistant" ? turn.Role : "user";
            var content = turn.Content.Length > 4000 ? turn.Content[..4000] : turn.Content;
            messages.Add(new ChatMessage(role, content));
        }
        messages.Add(new ChatMessage("user", context.BuildGroundedUserMessage(request.Question)));
        return messages;
    }

    private static string? ExtractDelta(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var choices = doc.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() == 0) return null;
            var choice = choices[0];
            if (choice.TryGetProperty("delta", out var delta)
                && delta.TryGetProperty("content", out var content)
                && content.ValueKind == JsonValueKind.String)
                return content.GetString();
            if (choice.TryGetProperty("message", out var message)
                && message.TryGetProperty("content", out var msgContent)
                && msgContent.ValueKind == JsonValueKind.String)
                return msgContent.GetString();
        }
        catch (JsonException) { }
        return null;
    }

    private static async Task<string> SafeReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try { return await response.Content.ReadAsStringAsync(ct); }
        catch { return "<unreadable>"; }
    }

    private static string CombineUrl(string baseUrl, string path) => $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";

    private static string DescribeProvider(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return "none";
        if (baseUrl.Contains("groq", StringComparison.OrdinalIgnoreCase)) return "groq";
        if (baseUrl.Contains("openai.azure", StringComparison.OrdinalIgnoreCase)) return "azure-openai";
        if (baseUrl.Contains("openai", StringComparison.OrdinalIgnoreCase)) return "openai";
        if (baseUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            || baseUrl.Contains("127.0.0.1", StringComparison.Ordinal)) return "local";
        return "custom";
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatRequest
    {
        [JsonPropertyName("model")] public required string Model { get; init; }
        [JsonPropertyName("messages")] public required List<ChatMessage> Messages { get; init; }
        [JsonPropertyName("temperature")] public double Temperature { get; init; }
        [JsonPropertyName("max_tokens")] public int MaxTokens { get; init; }
        [JsonPropertyName("stream")] public bool Stream { get; init; }
        /// <summary>Qwen-style thinking switch (ignored by providers that don't know it); null = not sent.</summary>
        [JsonPropertyName("chat_template_kwargs")] public Dictionary<string, object>? ChatTemplateKwargs { get; init; }
    }
}
