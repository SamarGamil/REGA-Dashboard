using System.Text.Json;
using AqarInsights.Api.Models;

namespace AqarInsights.Api.Services;

/// <summary>
/// Loads dashboard login accounts from a JSON file (an array of {username, passwordHash})
/// once at construction. Username matching is exact (case-sensitive).
/// </summary>
public sealed class JsonUserStore : IUserStore
{
    private readonly Dictionary<string, UserAccount> _byUsername;

    public JsonUserStore(string filePath)
    {
        _byUsername = Load(filePath);
    }

    public UserAccount? Find(string username)
        => username is not null && _byUsername.TryGetValue(username, out var user) ? user : null;

    private static Dictionary<string, UserAccount> Load(string filePath)
    {
        if (!File.Exists(filePath))
            throw new InvalidOperationException($"User accounts file not found: {filePath}");

        var json = File.ReadAllText(filePath);
        var accounts = JsonSerializer.Deserialize<List<UserAccount>>(json, JsonDefaults.Read) ?? [];

        var byUsername = new Dictionary<string, UserAccount>(StringComparer.Ordinal);
        foreach (var account in accounts)
        {
            if (string.IsNullOrWhiteSpace(account.Username) || string.IsNullOrWhiteSpace(account.PasswordHash))
                continue;
            byUsername[account.Username] = account;
        }

        if (byUsername.Count == 0)
            throw new InvalidOperationException($"No valid user accounts loaded from {filePath}");

        return byUsername;
    }
}
