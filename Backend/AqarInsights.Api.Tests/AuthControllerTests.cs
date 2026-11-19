using AqarInsights.Api.Controllers;
using AqarInsights.Api.Models;
using AqarInsights.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AqarInsights.Api.Tests;

public class AuthControllerTests
{
    private static readonly LoginResponse FakeResponse = new("fake.jwt.token", DateTimeOffset.UtcNow.AddHours(24));

    private sealed class FakeAuthService : IAuthService
    {
        public bool ShouldValidate { get; set; }

        public bool ValidateCredentials(string username, string password) => ShouldValidate;

        public LoginResponse GenerateToken(string username) => FakeResponse;
    }

    private sealed class FakeRevocationStore : ITokenRevocationStore
    {
        public readonly HashSet<string> Revoked = [];
        public void Revoke(string jti, DateTimeOffset expiresAt) => Revoked.Add(jti);
        public bool IsRevoked(string jti) => Revoked.Contains(jti);
    }

    [Fact]
    public void Login_ValidCredentials_ReturnsOkWithToken()
    {
        var fakeService = new FakeAuthService { ShouldValidate = true };
        var controller = new AuthController(fakeService, new FakeRevocationStore());
        var request = new LoginRequest { Username = "admin", Password = "Admin@123" };

        var result = controller.Login(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<LoginResponse>(okResult.Value);
        Assert.Equal(FakeResponse.Token, response.Token);
    }

    [Fact]
    public void Login_InvalidCredentials_ReturnsUnauthorized()
    {
        var fakeService = new FakeAuthService { ShouldValidate = false };
        var controller = new AuthController(fakeService, new FakeRevocationStore());
        var request = new LoginRequest { Username = "admin", Password = "wrong" };

        var result = controller.Login(request);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public void Logout_RevokesCurrentTokenJti()
    {
        var store = new FakeRevocationStore();
        var controller = new AuthController(new FakeAuthService(), store);
        var expiry = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var identity = new ClaimsIdentity(
        [
            new Claim(JwtRegisteredClaimNames.Jti, "token-123"),
            new Claim(JwtRegisteredClaimNames.Exp, expiry.ToString())
        ], "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        var result = controller.Logout();

        Assert.IsType<NoContentResult>(result);
        Assert.True(store.IsRevoked("token-123"));
    }
}
