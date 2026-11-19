using AqarInsights.Api.Models.RentalControls.Tables;
using AqarInsights.Api.Services.RentalControls.Snapshots;
using Microsoft.Extensions.Logging.Abstractions;

namespace AqarInsights.Api.Services.RentalControls.Tables;

/// <summary>
/// One-time move to the quarter-only layout: monthly releases' latest-month rows → monthly_movement; release_corrections
/// notes → corrections/{id}.json; the open quarter's workbooks → live/source; then the monthly releases and the copy of
/// the open quarter are deleted. Safe to run again (it only acts on what is left).
/// </summary>
public sealed class ReleaseStorageMigration(IRentalControlsRepository repository, ILogger<ReleaseStorageMigration>? logger = null)
{
    private readonly ILogger _logger = (ILogger?)logger ?? NullLogger.Instance;

    private sealed record LegacyCorrection(string ReleaseId, int Seq, string Text);

    public async Task<IReadOnlyList<string>> MigrateAsync(CancellationToken cancellationToken = default)
    {
        var log = new List<string>();
        var releases = repository.GetReleases();
        if (releases.FirstOrDefault(r => r.Id == ReleaseIds.Live) is not { } liveRow)
            throw new InvalidOperationException("No live release: nothing to migrate.");
        var live = ReleaseMapper.FromRows((await repository.LoadReleaseAsync(ReleaseIds.Live, cancellationToken))!);
        var liveMonth = SnapshotPeriod.DetectMonth(live.CityRaw.Values)
            ?? throw new InvalidOperationException("The live release has no month.");
        YearQuarter? openQuarter = YearQuarter.TryParse(liveRow.LatestQuarter, out var oq) ? oq : null;
        var monthly = releases.Where(r => YearMonth.TryParse(r.Id, out _)).OrderBy(r => r.Id, StringComparer.Ordinal).ToList();
        var openCopy = releases.FirstOrDefault(r => openQuarter is { } q && r.Id == q.Id);

        // 1. monthly_movement: each monthly release under its own month, then live (the live month's rows win).
        var table = await repository.LoadMonthlyMovementAsync(cancellationToken);
        foreach (var release in monthly)
        {
            var data = ReleaseMapper.FromRows((await repository.LoadReleaseAsync(release.Id, cancellationToken))!);
            YearMonth.TryParse(release.Id, out var month);
            var (rows, skipped) = MonthlyMovement.Capture(data, month, release.UpdatedAt);
            table = MonthlyMovement.Upsert(table, month, rows);
            log.Add($"monthly_movement {month.Id}: {rows.Count} rows from release {release.Id}"
                + (skipped.Count > 0 ? $", skipped {skipped.Count} ({skipped[0]} …)" : ""));
        }
        var (liveRows, liveSkipped) = MonthlyMovement.Capture(live, liveMonth, liveRow.UpdatedAt);
        table = MonthlyMovement.Upsert(table, liveMonth, liveRows);
        log.Add($"monthly_movement {liveMonth.Id}: {liveRows.Count} rows from live" + (liveSkipped.Count > 0 ? $", skipped {liveSkipped.Count}" : ""));
        await repository.WriteMonthlyMovementAsync(table, cancellationToken);

        // 2. Kept releases' free-text corrections become notes of their corrections file.
        var deleted = monthly.Select(r => r.Id)
            .Concat(openCopy is null ? Array.Empty<string>() : new[] { openCopy.Id })
            .ToHashSet(StringComparer.Ordinal);
        foreach (var release in releases.Where(r => !deleted.Contains(r.Id)))
        {
            var legacy = Path.Combine(repository.Location(release.Id), TableFiles.LegacyReleaseCorrections);
            if (!File.Exists(legacy)) continue;
            var texts = TableJson.ReadRows<LegacyCorrection>(await File.ReadAllTextAsync(legacy, cancellationToken))
                .OrderBy(c => c.Seq).Select(c => c.Text);
            var file = await repository.LoadCorrectionsAsync(release.Id, cancellationToken);
            var notes = file.Notes.Concat(texts.Where(t => !file.Notes.Contains(t))).ToList();
            await repository.WriteCorrectionsAsync(release.Id, file with { Notes = notes }, cancellationToken);
            File.Delete(legacy);
            log.Add($"corrections/{release.Id}.json: {notes.Count} notes");
        }

        // 3. Keep the audit copy of the current workbooks with live.
        var liveSource = Path.Combine(repository.Location(ReleaseIds.Live), JsonTablesRepository.SourceFolder);
        if (!Directory.Exists(liveSource))
        {
            var from = new[] { openCopy?.Id, liveMonth.Id }.OfType<string>()
                .Select(id => Path.Combine(repository.Location(id), JsonTablesRepository.SourceFolder))
                .FirstOrDefault(Directory.Exists);
            if (from is not null)
            {
                Directory.CreateDirectory(liveSource);
                foreach (var f in Directory.GetFiles(from)) File.Copy(f, Path.Combine(liveSource, Path.GetFileName(f)));
                log.Add($"live/source: workbooks copied from {from}");
            }
        }

        // 4. Monthly releases and the copy of the open quarter go.
        foreach (var id in deleted.Order(StringComparer.Ordinal))
        {
            await repository.DeleteReleaseAsync(id, cancellationToken);
            log.Add($"deleted release {id}");
        }

        foreach (var line in log) _logger.LogInformation("Release storage migration: {Step}", line);
        return log;
    }
}
