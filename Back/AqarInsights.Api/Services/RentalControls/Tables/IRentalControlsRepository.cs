using AqarInsights.Api.Models.RentalControls.Tables;

namespace AqarInsights.Api.Services.RentalControls.Tables;

/// <summary>
/// Rental-controls releases stored as tables, one partition per release. JSON files today; a database later only
/// needs another implementation of this interface.
/// </summary>
public interface IRentalControlsRepository
{
    /// <summary>Every stored release (including "live"), ordered by id.</summary>
    IReadOnlyList<ReleaseRow> GetReleases();

    /// <summary>All rows of one release, or null when it does not exist.</summary>
    Task<ReleaseData?> LoadReleaseAsync(string releaseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the release atomically. <paramref name="sourceDir"/>: workbooks to keep with it (audit copy);
    /// null keeps the ones already stored with the release.
    /// </summary>
    Task WriteReleaseAsync(ReleaseData data, string? sourceDir = null, CancellationToken cancellationToken = default);

    /// <summary>The release's guarded corrections and notes (tables/corrections/{id}.json); empty when none.</summary>
    Task<CorrectionsFile> LoadCorrectionsAsync(string releaseId, CancellationToken cancellationToken = default);

    Task WriteCorrectionsAsync(string releaseId, CorrectionsFile file, CancellationToken cancellationToken = default);

    /// <summary>The as-published latest-month market-movement rows of every month (tables/monthly_movement.json).</summary>
    Task<IReadOnlyList<MonthlyMovementRow>> LoadMonthlyMovementAsync(CancellationToken cancellationToken = default);

    Task WriteMonthlyMovementAsync(IReadOnlyList<MonthlyMovementRow> rows, CancellationToken cancellationToken = default);

    /// <summary>Removes a release partition (no-op when missing); its corrections file is kept.</summary>
    Task DeleteReleaseAsync(string releaseId, CancellationToken cancellationToken = default);

    /// <summary>Where the release is stored (for logs and import reports).</summary>
    string Location(string releaseId);
}
