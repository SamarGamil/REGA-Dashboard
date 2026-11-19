using AqarInsights.Api.Models;
using AqarInsights.Api.Models.ForeignOwnership;
using AqarInsights.Api.Services;
using AqarInsights.Api.Services.ForeignOwnership;

namespace AqarInsights.Api.Tests;

// Shared test doubles for the foreign-ownership aggregation stack.
internal sealed class FakeLoader(IReadOnlyList<TransactionRecord> records) : IDataLoaderService
{
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public IReadOnlyList<TransactionRecord> GetRecords() => records;
}

internal sealed class FakeFilterOptions : IFilterOptionsService
{
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public FilterOptions? GetOptions() => new();
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
