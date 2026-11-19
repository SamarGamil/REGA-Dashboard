using AqarInsights.Api.Models;

namespace AqarInsights.Api.Services;

public interface IUserStore
{
    /// <summary>Returns the account with an exact (case-sensitive) username match, or null.</summary>
    UserAccount? Find(string username);
}
