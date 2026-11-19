using AqarInsights.Api.Models;

namespace AqarInsights.Api.Services;

public interface IAuthService
{
    bool ValidateCredentials(string username, string password);
    LoginResponse GenerateToken(string username);
}
