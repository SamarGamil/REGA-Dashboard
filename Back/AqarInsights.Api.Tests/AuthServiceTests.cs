using AqarInsights.Api.Models;
using AqarInsights.Api.Services;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;

namespace AqarInsights.Api.Tests;

public class AuthServiceTests
{
    private const string TestUsername = "admin";
    private const string TestPassword = "Admin@123";
    private static readonly string TestPasswordHash = BCrypt.Net.BCrypt.HashPassword(TestPassword, 11);
    private const string TestJwtSecret = "AqarInsights-Test-Secret-Key-Must-Be-At-Least-32-Chars!";
    private const int TestJwtExpiryHours = 24;

    private sealed class FakeUserStore(params UserAccount[] users) : IUserStore
    {
        private readonly Dictionary<string, UserAccount> _byUsername =
            users.ToDictionary(u => u.Username, StringComparer.Ordinal);

        public UserAccount? Find(string username)
            => _byUsername.TryGetValue(username, out var user) ? user : null;
    }

    private static AuthService CreateService()
    {
        var settings = new AuthSettings
        {
            JwtSecret = TestJwtSecret,
            JwtExpiryHours = TestJwtExpiryHours
        };
        var users = new FakeUserStore(new UserAccount { Username = TestUsername, PasswordHash = TestPasswordHash });
        return new AuthService(Options.Create(settings), users);
    }

    [Fact]
    public void ValidateCredentials_ValidUsernameAndPassword_ReturnsTrue()
    {
        var service = CreateService();

        var result = service.ValidateCredentials(TestUsername, TestPassword);

        Assert.True(result);
    }

    [Fact]
    public void ValidateCredentials_InvalidPassword_ReturnsFalse()
    {
        var service = CreateService();

        var result = service.ValidateCredentials(TestUsername, "WrongPassword");

        Assert.False(result);
    }

    [Fact]
    public void ValidateCredentials_InvalidUsername_ReturnsFalse()
    {
        var service = CreateService();

        var result = service.ValidateCredentials("wronguser", TestPassword);

        Assert.False(result);
    }

    [Fact]
    public void ValidateCredentials_CaseInsensitiveUsername_ReturnsFalse()
    {
        var service = CreateService();

        // Username comparison should be exact (case-sensitive)
        var result = service.ValidateCredentials("Admin", TestPassword);

        Assert.False(result);
    }

    [Fact]
    public void GenerateToken_ReturnsNonEmptyToken()
    {
        var service = CreateService();

        var response = service.GenerateToken(TestUsername);

        Assert.NotNull(response);
        Assert.NotEmpty(response.Token);
    }

    [Fact]
    public void GenerateToken_ReturnsExpiryInFuture()
    {
        var service = CreateService();

        var response = service.GenerateToken(TestUsername);

        Assert.True(response.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public void GenerateToken_ExpiryMatchesConfiguredHours()
    {
        var service = CreateService();
        var before = DateTimeOffset.UtcNow.AddHours(TestJwtExpiryHours);

        var response = service.GenerateToken(TestUsername);

        // ExpiresAt should be within 5 seconds of expected (account for test execution time)
        var diff = (response.ExpiresAt - before).TotalSeconds;
        Assert.InRange(diff, -5, 5);
    }

    [Fact]
    public void GenerateToken_TokenIsValidJwtFormat()
    {
        var service = CreateService();

        var response = service.GenerateToken(TestUsername);

        // JWT has 3 parts separated by dots
        var parts = response.Token.Split('.');
        Assert.Equal(3, parts.Length);
    }

    [Fact]
    public void GenerateToken_IncludesUniqueJti()
    {
        var service = CreateService();

        var first = new JwtSecurityTokenHandler().ReadJwtToken(service.GenerateToken(TestUsername).Token);
        var second = new JwtSecurityTokenHandler().ReadJwtToken(service.GenerateToken(TestUsername).Token);

        Assert.False(string.IsNullOrEmpty(first.Id));
        Assert.NotEqual(first.Id, second.Id);
    }
}
