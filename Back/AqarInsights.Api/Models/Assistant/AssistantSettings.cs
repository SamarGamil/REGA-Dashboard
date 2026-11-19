namespace AqarInsights.Api.Models.Assistant;

/// <summary>Config for the "Talk to Data" LLM engine, bound from the "Assistant" section.
/// Provider-agnostic: any OpenAI-compatible chat endpoint (Groq, Azure OpenAI, Ollama, …).</summary>
public sealed class AssistantSettings
{
    public const string SectionName = "Assistant";

    public string BaseUrl { get; init; } = "";
    public string ApiKey { get; init; } = "";
    public string Model { get; init; } = "";
    public double Temperature { get; init; } = 0.2;
    public int MaxTokens { get; init; } = 900;

    /// <summary>Asks Qwen-style providers not to emit reasoning ("thinking") tokens, which would otherwise
    /// consume the MaxTokens budget before any answer content. Sent as chat_template_kwargs.enable_thinking=false.</summary>
    public bool DisableThinking { get; init; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl)
        && !string.IsNullOrWhiteSpace(Model)
        && (!string.IsNullOrWhiteSpace(ApiKey) || IsLocalEndpoint);

    public bool IsLocalEndpoint =>
        BaseUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase)
        || BaseUrl.Contains("127.0.0.1", StringComparison.Ordinal);
}
