namespace AqarInsights.Api.Models;

/// <summary>
/// Configuration for JWT settings and the location of the login accounts file.
/// Bound from the "AuthSettings" section in appsettings.json. Individual user
/// credentials live in the users-accounts JSON file (see <see cref="UsersFile"/>).
/// </summary>
public sealed class AuthSettings
{
    public required string JwtSecret { get; init; }
    public int JwtExpiryHours { get; init; } = 24;
    public string UsersFile { get; init; } = "Data/users-accounts.json";
}
