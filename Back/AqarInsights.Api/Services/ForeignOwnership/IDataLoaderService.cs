using AqarInsights.Api.Models;
using AqarInsights.Api.Models.ForeignOwnership;

namespace AqarInsights.Api.Services.ForeignOwnership;

public interface IDataLoaderService
{
    Task LoadAsync(CancellationToken cancellationToken = default);
    IReadOnlyList<TransactionRecord> GetRecords();
}
