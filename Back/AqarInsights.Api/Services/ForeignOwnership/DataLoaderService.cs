using AqarInsights.Api.Models;
using AqarInsights.Api.Models.ForeignOwnership;
using System.Text.Json;

namespace AqarInsights.Api.Services.ForeignOwnership;

public class DataLoaderService : IDataLoaderService
{
    private readonly string _transactionsFilePath;
    private readonly ILogger<DataLoaderService> _logger;
    private volatile IReadOnlyList<TransactionRecord> _records = [];

    public DataLoaderService(string transactionsFilePath, ILogger<DataLoaderService> logger)
    {
        _transactionsFilePath = transactionsFilePath;
        _logger = logger;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_transactionsFilePath))
        {
            _logger.LogWarning("Transactions file not found: {Path}", _transactionsFilePath);
            _records = [];
            return;
        }

        var json = await File.ReadAllTextAsync(_transactionsFilePath, cancellationToken);
        var deserialized = JsonSerializer.Deserialize<List<TransactionRecord>>(json, JsonDefaults.Read) ?? [];

        _records = deserialized;
        _logger.LogInformation("Loaded {Count} transaction records", deserialized.Count);
    }

    public IReadOnlyList<TransactionRecord> GetRecords() => _records;
}
