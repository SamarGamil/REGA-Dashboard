using System.Text.Encodings.Web;
using System.Text.Json;
using AqarInsights.Api.Models.RentalControls.Reports;

namespace AqarInsights.Api.Services.RentalControls.Reports;

public interface IReportHistoryStore
{
    Task<IReadOnlyList<ReportHistoryRecord>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AppendAsync(ReportHistoryRecord record, CancellationToken cancellationToken = default);
}

/// <summary>
/// JSON-file-based report request history. Appends are serialized behind a lock so concurrent
/// requests don't clobber the file. The flat record shape is DB-migration-ready.
/// </summary>
public class JsonReportHistoryStore : IReportHistoryStore
{
    private readonly string _filePath;
    private readonly ILogger<JsonReportHistoryStore>? _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public JsonReportHistoryStore(string filePath, ILogger<JsonReportHistoryStore>? logger = null)
    {
        _filePath = filePath;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ReportHistoryRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return await ReadAsync(cancellationToken); }
        finally { _gate.Release(); }
    }

    public async Task AppendAsync(ReportHistoryRecord record, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var list = new List<ReportHistoryRecord>(await ReadAsync(cancellationToken)) { record };
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(_filePath, JsonSerializer.Serialize(list, JsonOptions), cancellationToken);
        }
        finally { _gate.Release(); }
    }

    private async Task<List<ReportHistoryRecord>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath)) return new();
        try
        {
            var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
            return JsonSerializer.Deserialize<List<ReportHistoryRecord>>(json, JsonOptions) ?? new();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to read report history from {FilePath}", _filePath);
            return new();
        }
    }
}
