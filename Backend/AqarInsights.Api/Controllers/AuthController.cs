using AqarInsights.Api.Models;
using AqarInsights.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AqarInsights.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(IAuthService authService, ITokenRevocationStore revocationStore) : ControllerBase
{
    private readonly IAuthService _authService = authService;
    private readonly ITokenRevocationStore _revocationStore = revocationStore;

    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        if (!_authService.ValidateCredentials(request.Username, request.Password))
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Status = 401,
                Detail = "Invalid username or password."
            });
        }

        var response = _authService.GenerateToken(request.Username);
        return Ok(response);
    }

    /// <summary>
    /// Revokes the caller's current token so it can no longer be replayed after logout.
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Logout()
    {
        var jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
        if (!string.IsNullOrEmpty(jti))
        {
            var expiresAt = long.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Exp), out var expUnix)
                ? DateTimeOffset.FromUnixTimeSeconds(expUnix)
                : DateTimeOffset.UtcNow;
            _revocationStore.Revoke(jti, expiresAt);
        }

        return NoContent();
    }
}
