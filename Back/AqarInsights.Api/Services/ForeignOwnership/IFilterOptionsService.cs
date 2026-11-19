using AqarInsights.Api.Models;
using AqarInsights.Api.Models.ForeignOwnership;

namespace AqarInsights.Api.Services.ForeignOwnership;

public interface IFilterOptionsService
{
    Task LoadAsync(CancellationToken cancellationToken = default);
    FilterOptions? GetOptions();
}
