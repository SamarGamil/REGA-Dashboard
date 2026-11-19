using System.ComponentModel.DataAnnotations;

namespace AqarInsights.Api.Models.Assistant;

public sealed record ChatTurn(string Role, string Content);

/// <summary>Request body for POST /api/assistant/ask.</summary>
public sealed record AssistantAskRequest
{
    [Required] public string Question { get; init; } = "";
    public string Lang { get; init; } = "ar";

    /// <summary>Active product/dashboard: "rental-indicators" or "foreign-ownership". Selects the grounding data.</summary>
    public string Product { get; init; } = "rental-indicators";

    /// <summary>Rental-indicators period selected in the portal ("yyyy-MM" or "yyyy-Qn"); null/empty = the live quarter.</summary>
    public string? Period { get; init; }

    public IReadOnlyList<ChatTurn> History { get; init; } = [];
}

/// <summary>Response for GET /api/assistant/status.</summary>
public sealed record AssistantStatus(bool Enabled, string Provider, string Model);
