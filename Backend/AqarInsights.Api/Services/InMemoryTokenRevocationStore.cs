using Microsoft.Extensions.Caching.Memory;

namespace AqarInsights.Api.Services;

/// <summary>
/// In-memory denylist of revoked JWT ids. Entries auto-expire when the token would have expired,
/// so the list never grows beyond the active-token window.
/// NOTE: state is per-process. With multiple replicas a token revoked on one pod is still accepted
/// by the others until it expires. Replace with a shared store (e.g. Redis via IDistributedCache)
/// for guaranteed cross-replica revocation.
/// </summary>
public sealed class InMemoryTokenRevocationStore(IMemoryCache cache) : ITokenRevocationStore
{
    private readonly IMemoryCache _cache = cache;

    public void Revoke(string jti, DateTimeOffset expiresAt)
    {
        if (string.IsNullOrEmpty(jti))
            return;
        // Nothing to store if the token has already expired.
        if (expiresAt <= DateTimeOffset.UtcNow)
            return;
        _cache.Set(Key(jti), true, expiresAt);
    }

    public bool IsRevoked(string jti) =>
        !string.IsNullOrEmpty(jti) && _cache.TryGetValue(Key(jti), out _);

    private static string Key(string jti) => $"revoked-jti:{jti}";
}
