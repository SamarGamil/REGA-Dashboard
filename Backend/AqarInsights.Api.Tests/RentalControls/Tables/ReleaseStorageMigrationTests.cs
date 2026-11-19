using System.Security.Cryptography;
using AqarInsights.Api.Models.RentalControls.Tables;
using AqarInsights.Api.Services.RentalControls.Tables;
using AqarInsights.Api.Tests.RentalControls.Reports;

namespace AqarInsights.Api.Tests.RentalControls.Tables;

// Runs the migration on a copy of the committed release tables. Before the real data is migrated the copy holds live,
// 2026-06, 2026-08, 2026-09, 2026-Q2, 2026-Q3; afterwards the test still passes on the migrated layout.
public sealed class ReleaseStorageMigrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"rc-migrate-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static void Copy(string from, string to)
    {
        foreach (var dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(from, to));
        foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(from, to), overwrite: true);
    }

    private static Dictionary<string, string> Hashes(string dir) => Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
        .ToDictionary(f => Path.GetRelativePath(dir, f), f => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))));

    [Fact]
    public async Task Migrates_to_live_plus_closed_quarters_and_changes_nothing_else()
    {
        var tables = Path.Combine(_root, "tables");
        Directory.CreateDirectory(tables);
        Copy(RealReportData.TablesDir, tables);
        var repo = new JsonTablesRepository(tables);
        var before = Hashes(tables);
        var sources = new Dictionary<string, ReleaseData>();
        foreach (var r in repo.GetReleases()) sources[r.Id] = (await repo.LoadReleaseAsync(r.Id))!;

        await new ReleaseStorageMigration(repo).MigrateAsync();
        var afterFirst = Hashes(tables);
        await new ReleaseStorageMigration(repo).MigrateAsync();

        Assert.Equal(afterFirst, Hashes(tables)); // safe to run again
        Assert.Equal(new[] { "2026-Q2", "live" }, repo.GetReleases().Select(r => r.Id));
        // Kept partitions: every table byte-identical; only release_corrections.json left the partition.
        foreach (var (file, hash) in before.Where(f => f.Key.StartsWith("live") || f.Key.StartsWith("2026-Q2")))
        {
            if (file.EndsWith(TableFiles.LegacyReleaseCorrections)) Assert.False(afterFirst.ContainsKey(file), file);
            else Assert.Equal(hash, afterFirst[file]);
        }
        if (!sources.ContainsKey("2026-06")) return; // the committed data is already migrated

        // Monthly rows are copies of their source rows.
        var table = await repo.LoadMonthlyMovementAsync();
        Assert.Equal(new[] { ("2026-06", 28), ("2026-08", 28), ("2026-09", 42) },
            table.GroupBy(r => r.Month).Select(g => (g.Key, g.Count())));
        foreach (var row in table)
        {
            var source = sources[row.Month == "2026-09" ? ReleaseIds.Live : row.Month].MarketMovement
                .Single(m => m.CityId == row.CityId && m.Seq == row.Seq);
            Assert.Equal((source.Indicator, source.Level, source.Change, source.Assessment), (row.Indicator, row.Level, row.Change, row.Assessment));
        }
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(tables, "live", JsonTablesRepository.SourceFolder)));
        Assert.NotEmpty((await repo.LoadCorrectionsAsync("2026-Q2")).Notes);
        Assert.NotEmpty((await repo.LoadCorrectionsAsync(ReleaseIds.Live)).Notes);
    }
}
