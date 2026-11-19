using AqarInsights.Api.Services;
using Microsoft.Extensions.Caching.Memory;

namespace AqarInsights.Api.Tests;

public class InMemoryTokenRevocationStoreTests
{
    private static InMemoryTokenRevocationStore CreateStore() =>
        new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public void IsRevoked_UnknownJti_ReturnsFalse()
    {
        var store = CreateStore();

        Assert.False(store.IsRevoked("never-seen"));
    }

    [Fact]
    public void Revoke_ThenIsRevoked_ReturnsTrue()
    {
        var store = CreateStore();

        store.Revoke("jti-1", DateTimeOffset.UtcNow.AddHours(1));

        Assert.True(store.IsRevoked("jti-1"));
    }

    [Fact]
    public void Revoke_AlreadyExpired_IsNotStored()
    {
        var store = CreateStore();

        store.Revoke("jti-expired", DateTimeOffset.UtcNow.AddSeconds(-1));

        Assert.False(store.IsRevoked("jti-expired"));
    }

    [Fact]
    public void Revoke_EmptyJti_IsIgnored()
    {
        var store = CreateStore();

        store.Revoke("", DateTimeOffset.UtcNow.AddHours(1));

        Assert.False(store.IsRevoked(""));
    }
}
