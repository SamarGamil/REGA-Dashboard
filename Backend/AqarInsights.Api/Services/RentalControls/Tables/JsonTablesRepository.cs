using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AqarInsights.Api.Models.RentalControls.Tables;
using Microsoft.Extensions.Logging.Abstractions;

namespace AqarInsights.Api.Services.RentalControls.Tables;

/// <summary>File names of the tables in a release partition (one file per table).</summary>
public static class TableFiles
{
    public const string Release = "release.json";
    public const string Cities = "cities.json";
    public const string CityStatus = "city_status.json";
    public const string Observations = "observations.json";
    public const string MarketMovement = "market_movement.json";
    public const string CityNarrative = "city_narrative.json";
    public const string ReportProducts = "report_products.json";
    public const string ReportAxes = "report_axes.json";
    public const string MediaMonitoring = "media_monitoring.json";
    public const string MediaItems = "media_items.json";
    public const string GapCities = "gap_cities.json";

    // Outside the release partitions: shared by all releases / kept when a partition is rewritten.
    public const string MonthlyMovement = "monthly_movement.json";
    public const string CorrectionsFolder = "corrections";

    // Former per-release free-text corrections table; moved to corrections/{id}.json notes by the storage migration.
    public const string LegacyReleaseCorrections = "release_corrections.json";
}

/// <summary>
/// Release-partitioned JSON tables under tables/{releaseId}/: one file per table, each a JSON array with one row per
/// line, shaped for a later bulk load into a database. A partition is built in a dot-prefixed staging folder and
/// swapped in, so readers never see a half-written release; source/ keeps the release's workbooks (audit copy).
/// </summary>
public sealed class JsonTablesRepository : IRentalControlsRepository
{
    public const string SourceFolder = "source";

    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static readonly Regex ReleaseIdPattern = new(@"^(live|\d{4}-\d{2}|\d{4}-Q[1-4])\z", RegexOptions.CultureInvariant);

    private readonly ILogger _logger;

    public JsonTablesRepository(string tablesDir, ILogger<JsonTablesRepository>? logger = null)
    {
        TablesDir = tablesDir;
        _logger = (ILogger?)logger ?? NullLogger.Instance;
    }

    public string TablesDir { get; }

    public string Location(string releaseId) => Path.Combine(TablesDir, releaseId);

    public IReadOnlyList<ReleaseRow> GetReleases()
    {
        if (!Directory.Exists(TablesDir)) return [];
        var releases = new List<ReleaseRow>();
        foreach (var dir in Directory.GetDirectories(TablesDir).Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(dir);
            var path = Path.Combine(dir, TableFiles.Release);
            if (name.StartsWith('.') || !File.Exists(path)) continue;
            if (!ReleaseIdPattern.IsMatch(name))
            {
                _logger.LogWarning("Skipping release folder {Dir}: '{Name}' is not a release id (live, yyyy-MM or yyyy-Qn)", dir, name);
                continue;
            }
            try
            {
                var release = ReadRows<ReleaseRow>(File.ReadAllText(path)).Single();
                if (release.Id == name) releases.Add(release);
                else _logger.LogWarning("Skipping release folder {Dir}: it holds release '{Id}'", dir, release.Id);
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Skipping unreadable release {Path}", path);
            }
        }
        return releases;
    }

    public async Task<ReleaseData?> LoadReleaseAsync(string releaseId, CancellationToken cancellationToken = default)
    {
        Validate(releaseId);
        var dir = Location(releaseId);
        var releasePath = Path.Combine(dir, TableFiles.Release);
        if (!File.Exists(releasePath)) return null;

        async Task<List<T>> Table<T>(string file)
        {
            var path = Path.Combine(dir, file);
            return File.Exists(path) ? ReadRows<T>(await File.ReadAllTextAsync(path, cancellationToken)) : [];
        }

        var release = (await Table<ReleaseRow>(TableFiles.Release)).Single();
        if (release.Id != releaseId)
            throw new InvalidDataException($"{releasePath} holds release '{release.Id}', not '{releaseId}'.");

        var data = new ReleaseData(release)
        {
            Cities = await Table<CityRow>(TableFiles.Cities),
            CityStatus = await Table<CityStatusRow>(TableFiles.CityStatus),
            Observations = await Table<ObservationRow>(TableFiles.Observations),
            MarketMovement = await Table<MarketMovementTableRow>(TableFiles.MarketMovement),
            CityNarrative = await Table<CityNarrativeRow>(TableFiles.CityNarrative),
            ReportProducts = await Table<ReportProductTableRow>(TableFiles.ReportProducts),
            ReportAxes = await Table<ReportAxisTableRow>(TableFiles.ReportAxes),
            MediaMonitoring = await Table<MediaMonitoringRow>(TableFiles.MediaMonitoring),
            MediaItems = await Table<MediaItemRow>(TableFiles.MediaItems),
            GapCities = await Table<GapCityRow>(TableFiles.GapCities),
        };
        if (data.AllRows().FirstOrDefault(r => r.ReleaseId != releaseId) is { } stray)
            throw new InvalidDataException($"Release '{releaseId}' holds a row of release '{stray.ReleaseId}': {stray}");
        return data;
    }

    public async Task WriteReleaseAsync(ReleaseData data, string? sourceDir = null, CancellationToken cancellationToken = default)
    {
        var id = data.Release.Id;
        Validate(id);
        if (data.AllRows().FirstOrDefault(r => r.ReleaseId != id) is { } stray)
            throw new ArgumentException($"Release '{id}' cannot hold a row of release '{stray.ReleaseId}': {stray}", nameof(data));

        Directory.CreateDirectory(TablesDir);
        var target = Location(id);
        var staging = Path.Combine(TablesDir, $".{id}.{Guid.NewGuid():N}.tmp");
        string? previous = null;
        var replaced = false;
        try
        {
            Directory.CreateDirectory(staging);
            async Task Write<T>(string file, IEnumerable<T> rows)
                => await File.WriteAllTextAsync(Path.Combine(staging, file), Serialize(rows), Utf8, cancellationToken);

            await Write(TableFiles.Release, new[] { data.Release });
            await Write(TableFiles.Cities, data.Cities);
            await Write(TableFiles.CityStatus, data.CityStatus);
            await Write(TableFiles.Observations, data.Observations);
            await Write(TableFiles.MarketMovement, data.MarketMovement);
            await Write(TableFiles.CityNarrative, data.CityNarrative);
            await Write(TableFiles.ReportProducts, data.ReportProducts);
            await Write(TableFiles.ReportAxes, data.ReportAxes);
            await Write(TableFiles.MediaMonitoring, data.MediaMonitoring);
            await Write(TableFiles.MediaItems, data.MediaItems);
            await Write(TableFiles.GapCities, data.GapCities);

            CopyFiles(string.IsNullOrWhiteSpace(sourceDir) ? Path.Combine(target, SourceFolder) : sourceDir,
                Path.Combine(staging, SourceFolder));

            // The previous release is set aside (not deleted) until the new one is in place, and put back if the move
            // fails, so a failed write never loses a release.
            if (Directory.Exists(target))
            {
                previous = Path.Combine(TablesDir, $".{id}.{Guid.NewGuid():N}.old");
                await MoveDirectoryAsync(target, previous, allowCopy: false, cancellationToken);
            }
            try
            {
                await MoveDirectoryAsync(staging, target, allowCopy: true, cancellationToken);
            }
            catch
            {
                if (previous is not null)
                {
                    if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
                    Directory.Move(previous, target);
                    previous = null;
                }
                throw;
            }
            replaced = true;
        }
        finally
        {
            // The set-aside release is removed only once the new one is in place; otherwise it is the only copy.
            foreach (var leftover in replaced ? new[] { staging, previous } : new[] { staging })
            {
                try
                {
                    if (leftover is not null && Directory.Exists(leftover)) Directory.Delete(leftover, recursive: true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(ex, "Could not remove release work folder {Dir}", leftover);
                }
            }
            if (!replaced && previous is not null && Directory.Exists(previous))
                _logger.LogError("Release {Release} could not be restored; its previous tables are in {Dir}", id, previous);
        }

        _logger.LogInformation("Wrote rental-controls release {Release} ({Observations} observations) to {Dir}",
            id, data.Observations.Count, target);
    }

    private static string Serialize<T>(IEnumerable<T> rows) => TableJson.SerializeRows(rows);

    private string CorrectionsPath(string releaseId) => Path.Combine(TablesDir, TableFiles.CorrectionsFolder, $"{releaseId}.json");

    public async Task<CorrectionsFile> LoadCorrectionsAsync(string releaseId, CancellationToken cancellationToken = default)
    {
        Validate(releaseId);
        var path = CorrectionsPath(releaseId);
        if (!File.Exists(path)) return CorrectionsFile.Empty;
        try
        {
            return JsonSerializer.Deserialize<CorrectionsFile>(await File.ReadAllTextAsync(path, cancellationToken), TableJson.Options)
                ?? CorrectionsFile.Empty;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Malformed corrections file {path} (line {ex.LineNumber + 1}): {ex.Message}", ex);
        }
    }

    public async Task WriteCorrectionsAsync(string releaseId, CorrectionsFile file, CancellationToken cancellationToken = default)
    {
        Validate(releaseId);
        var json = JsonSerializer.Serialize(file, new JsonSerializerOptions(TableJson.Options) { WriteIndented = true }) + "\n";
        await WriteFileAtomicallyAsync(CorrectionsPath(releaseId), json, cancellationToken);
    }

    public async Task<IReadOnlyList<MonthlyMovementRow>> LoadMonthlyMovementAsync(CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(TablesDir, TableFiles.MonthlyMovement);
        return File.Exists(path) ? ReadRows<MonthlyMovementRow>(await File.ReadAllTextAsync(path, cancellationToken)) : [];
    }

    public Task WriteMonthlyMovementAsync(IReadOnlyList<MonthlyMovementRow> rows, CancellationToken cancellationToken = default)
        => WriteFileAtomicallyAsync(Path.Combine(TablesDir, TableFiles.MonthlyMovement), Serialize(rows), cancellationToken);

    public Task DeleteReleaseAsync(string releaseId, CancellationToken cancellationToken = default)
    {
        Validate(releaseId);
        var dir = Location(releaseId);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
            _logger.LogInformation("Deleted rental-controls release {Release}", releaseId);
        }
        return Task.CompletedTask;
    }

    // Written next to the target and moved over it, so a reader never sees a half-written file.
    private static async Task WriteFileAtomicallyAsync(string path, string content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(temp, content, Utf8, cancellationToken);
        File.Move(temp, path, overwrite: true);
    }

    private static List<T> ReadRows<T>(string json) => TableJson.ReadRows<T>(json);

    private static void Validate(string releaseId)
    {
        if (!ReleaseIdPattern.IsMatch(releaseId ?? ""))
            throw new ArgumentException($"Invalid release id '{releaseId}'; expected live, yyyy-MM or yyyy-Qn.", nameof(releaseId));
    }

    private static void CopyFiles(string fromDir, string toDir)
    {
        if (!Directory.Exists(fromDir)) return;
        Directory.CreateDirectory(toDir);
        foreach (var file in Directory.GetFiles(fromDir))
            File.Copy(file, Path.Combine(toDir, Path.GetFileName(file)), overwrite: true);
    }

    // On Windows a just-written folder can be briefly locked (indexer/antivirus/file watchers), so a rename
    // is retried and, if it keeps failing and allowCopy is set, replaced by a copy (the caller removes the source).
    private static async Task MoveDirectoryAsync(string from, string to, bool allowCopy, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Directory.Move(from, to);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                await Task.Delay(200 * (attempt + 1), cancellationToken);
            }
            catch (Exception ex) when (allowCopy && ex is IOException or UnauthorizedAccessException)
            {
                break;
            }
        }

        foreach (var dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, dir)));
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(to, Path.GetRelativePath(from, file)), overwrite: true);
    }
}
