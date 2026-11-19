using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AqarInsights.Api.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AqarInsights.Api.Services;

public sealed class AuthService : IAuthService
{
    private readonly AuthSettings _settings;
    private readonly IUserStore _users;
    private readonly TimeProvider _timeProvider;
    // Valid hash used to equalise verification time when the username doesn't match.
    private readonly string _dummyHash;

    public AuthService(IOptions<AuthSettings> settings, IUserStore users, TimeProvider? timeProvider = null)
    {
        _settings = settings.Value;
        _users = users;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _dummyHash = BCrypt.Net.BCrypt.HashPassword("not-a-real-password", 11);
    }

    public bool ValidateCredentials(string username, string password)
    {
        var user = username is null ? null : _users.Find(username);
        // Always run one BCrypt verify so response time doesn't reveal whether the username exists.
        var hash = user is not null && !string.IsNullOrEmpty(user.PasswordHash) ? user.PasswordHash : _dummyHash;
        var passwordMatches = BCrypt.Net.BCrypt.Verify(password, hash);
        return user is not null && passwordMatches;
    }

    public LoginResponse GenerateToken(string username)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.JwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var nowUtc = _timeProvider.GetUtcNow();
        var expiry = nowUtc.AddHours(_settings.JwtExpiryHours);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim(JwtRegisteredClaimNames.Iat, nowUtc.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: expiry.UtcDateTime,
            signingCredentials: credentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return new LoginResponse(tokenString, expiry);
    }
}
