namespace AqarInsights.Api.Models;

/// <summary>
/// A single dashboard login account. Loaded from the users-accounts JSON file.
/// </summary>
public sealed record UserAccount
{
    public required string Username { get; init; }
    public required string PasswordHash { get; init; }
}
