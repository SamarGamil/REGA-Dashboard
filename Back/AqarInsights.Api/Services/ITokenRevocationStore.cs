namespace AqarInsights.Api.Services;

/// <summary>
/// Tracks JWT ids (jti) that have been revoked via logout so their tokens can no longer be replayed.
/// </summary>
public interface ITokenRevocationStore
{
    /// <summary>Marks a token id as revoked until its natural expiry.</summary>
    void Revoke(string jti, DateTimeOffset expiresAt);

    /// <summary>Returns true if the token id has been revoked and its token must be rejected.</summary>
    bool IsRevoked(string jti);
}
