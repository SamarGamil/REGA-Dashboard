using System.ComponentModel.DataAnnotations;

namespace AqarInsights.Api.Models;

/// <summary>Payload for authenticating a user.</summary>
public sealed record LoginRequest
{
    [Required]
    public required string Username { get; init; }

    [Required]
    public required string Password { get; init; }
}

/// <summary>Successful authentication response containing a JWT token.</summary>
public sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt);
