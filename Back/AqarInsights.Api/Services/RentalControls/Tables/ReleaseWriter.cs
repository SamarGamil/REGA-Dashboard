using AqarInsights.Api.Models.RentalControls.Raw;
using AqarInsights.Api.Models.RentalControls.Tables;
using AqarInsights.Api.Services.RentalControls.Projections;
using AqarInsights.Api.Services.RentalControls.Snapshots;
using Microsoft.Extensions.Logging.Abstractions;

namespace AqarInsights.Api.Services.RentalControls.Tables;

/// <summary>
/// Freezes a dataset as a closed quarter's release (yyyy-Qn), kept as published. Writing an id that already exists
/// replaces it; the input dataset is never modified.
/// </summary>
public class ReleaseWriter
{
    private readonly IRentalControlsRepository _repository;
    private readonly ILogger _logger;
    private readonly TimeProvider _clock;

    public ReleaseWriter(IRentalControlsRepository repository, ILogger<ReleaseWriter>? logger = null, TimeProvider? clock = null)
    {
        _repository = repository;
        _logger = (ILogger?)logger ?? NullLogger.Instance;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>
    /// Writes the dataset as the release of <paramref name="quarterId"/> (yyyy-Qn), which must be the data's latest
    /// quarter; nothing is trimmed (a closed quarter is kept as published). Null when there is no data.
    /// </summary>
    public async Task<ReleaseRow?> WriteAsync(
        MappedRelease data, string quarterId, string? sourceDir = null, string? sourceCommit = null,
        CancellationToken cancellationToken = default)
    {
        if (!YearQuarter.TryParse(quarterId, out var quarter))
            throw new ArgumentException($"Release period must be a quarter (yyyy-Qn), got '{quarterId}'.", nameof(quarterId));
        if (data.Cities.Count == 0) return null;

        var copy = ReleaseMapper.Copy(data);
        var dataQuarter = ReportingPeriod.LatestQuarter(CityRaws(copy));
        if (dataQuarter != quarter)
            throw new InvalidOperationException($"The data's latest quarter is {dataQuarter?.Id ?? "none"}, not {quarter.Id}.");
        // Guard against workbook date typos: a release may never be dated after the current month.
        var now = _clock.GetLocalNow();
        if (quarter.FirstMonth > new YearMonth(now.Year, now.Month))
            throw new InvalidOperationException($"Release quarter {quarter.Id} is in the future (today is {now:yyyy-MM}); check the workbook dates.");

        var release = new ReleaseRow(quarter.Id, ReleaseKinds.Quarterly, quarter.Label, quarter.Id, quarter.Label, now,
            string.IsNullOrWhiteSpace(sourceCommit) ? null : sourceCommit);
        await _repository.WriteReleaseAsync(ReleaseMapper.ToRows(release, copy), sourceDir, cancellationToken);

        _logger.LogInformation("Wrote rental-controls release {Release} to {Location}", quarter.Id, _repository.Location(quarter.Id));
        return release;
    }
    private static List<CityRawData> CityRaws(MappedRelease data)
        => data.Cities.Select(c => data.CityRaw.GetValueOrDefault(c.Id)).OfType<CityRawData>().ToList();
}
