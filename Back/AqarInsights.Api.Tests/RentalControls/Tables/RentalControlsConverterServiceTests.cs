using AqarInsights.Api.Models.RentalControls.Tables;
using AqarInsights.Api.Services.RentalControls;
using AqarInsights.Api.Services.RentalControls.Tables;
using AqarInsights.Api.Tests.RentalControls.Reports;
using Microsoft.Extensions.Logging.Abstractions;

namespace AqarInsights.Api.Tests.RentalControls.Tables;

public sealed class RentalControlsConverterServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"rc-import-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Import_writes_live_with_its_workbooks_and_the_months_rows_without_freezing_the_open_quarter()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        foreach (var file in Directory.GetFiles(RealReportData.SourceDir, "*.xlsx").Where(f => !Path.GetFileName(f).StartsWith("~$")))
            File.Copy(file, Path.Combine(source, Path.GetFileName(file)));
        // A narrative with a stale Q2 figure (+35%; the imported data has +37%) must surface as a warning.
        var narratives = Directory.CreateDirectory(Path.Combine(source, RentalControlsConverterService.NarrativeFolder)).FullName;
        File.WriteAllText(Path.Combine(narratives, "buraidah.json"),
            """{ "cityId": "buraidah", "marketBullets": ["النشاط الإيجاري يرتفع (الشقة +35%)"] }""");
        var repo = new JsonTablesRepository(Path.Combine(_root, "tables"));
        var converter = new RentalControlsConverterService(
            source, repo, NullLogger<RentalControlsConverterService>.Instance, new ReleaseWriter(repo));

        var first = await converter.ConvertAllAsync();
        var again = await converter.ConvertAllAsync(); // re-import: outgoing + new data, same ids overwritten

        Assert.True(first.Success, string.Join("; ", first.Errors));
        Assert.True(again.Success, string.Join("; ", again.Errors));
        Assert.DoesNotContain(again.Warnings, w => w.Sheet == "snapshot");
        Assert.DoesNotContain(again.Warnings, w => w.Sheet.StartsWith("supply-demand-gap"));
        Assert.Contains(again.Warnings, w => w.Sheet == "city-reports/buraidah" && w.Issue.Contains("+37%"));
        Assert.Equal(new[] { "live" }, repo.GetReleases().Select(r => r.Id));
        Assert.True(File.Exists(Path.Combine(repo.Location(ReleaseIds.Live), JsonTablesRepository.SourceFolder, "لوحة الضوابط الايجارية للمدن.xlsx")));
        Assert.True(File.Exists(Path.Combine(repo.Location(ReleaseIds.Live), JsonTablesRepository.SourceFolder, "supply-demand-gap.xlsx")));
        Assert.Equal(42, (await repo.LoadMonthlyMovementAsync()).Count(r => r.Month == "2026-10"));
        var live = ReleaseMapper.FromRows((await repo.LoadReleaseAsync(ReleaseIds.Live))!);
        Assert.Equal(14, live.Cities.Count);
        Assert.Equal(new[] { "النشاط الإيجاري يرتفع (الشقة +35%)" }, live.ReportContent["buraidah"].MarketBullets);
        Assert.Null(live.ReportContent.GetValueOrDefault("riyadh")); // only narratives in the source folder are imported
    }

    [Fact]
    public async Task A_missing_narrative_folder_fails_the_import_without_touching_the_releases()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        foreach (var file in Directory.GetFiles(RealReportData.SourceDir, "*.xlsx").Where(f => !Path.GetFileName(f).StartsWith("~$")))
            File.Copy(file, Path.Combine(source, Path.GetFileName(file)));
        var narratives = Directory.CreateDirectory(Path.Combine(source, RentalControlsConverterService.NarrativeFolder)).FullName;
        File.WriteAllText(Path.Combine(narratives, "riyadh.json"), """{ "cityId": "riyadh", "marketBullets": ["سطر"] }""");
        var repo = new JsonTablesRepository(Path.Combine(_root, "tables"));
        var converter = new RentalControlsConverterService(
            source, repo, NullLogger<RentalControlsConverterService>.Instance, new ReleaseWriter(repo));
        Assert.True((await converter.ConvertAllAsync()).Success);
        var before = repo.GetReleases().ToDictionary(r => r.Id, r => r.UpdatedAt);

        Directory.Delete(narratives, recursive: true);
        var report = await converter.ConvertAllAsync();

        Assert.False(report.Success);
        Assert.Contains(report.Errors, e => e.Contains(RentalControlsConverterService.NarrativeFolder));
        Assert.Equal(before, repo.GetReleases().ToDictionary(r => r.Id, r => r.UpdatedAt));
        Assert.Single(ReleaseMapper.FromRows((await repo.LoadReleaseAsync(ReleaseIds.Live))!).ReportContent);
    }

    // Live holds Q2's data; importing the Q3 workbooks closes Q2: the outgoing live is frozen as 2026-Q2 with its
    // corrections, once.
    [Fact]
    public async Task A_newer_quarter_freezes_the_outgoing_live_as_its_quarter_with_its_corrections()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        foreach (var file in Directory.GetFiles(RealReportData.SourceDir, "*.xlsx").Where(f => !Path.GetFileName(f).StartsWith("~$")))
            File.Copy(file, Path.Combine(source, Path.GetFileName(file)));
        var narratives = Directory.CreateDirectory(Path.Combine(source, RentalControlsConverterService.NarrativeFolder)).FullName;
        foreach (var file in Directory.GetFiles(RealReportData.NarrativeSourceDir))
            File.Copy(file, Path.Combine(narratives, Path.GetFileName(file)));
        var repo = new JsonTablesRepository(Path.Combine(_root, "tables"));
        var q2 = ReleaseMapper.FromRows((await RealReportData.Repository.LoadReleaseAsync("2026-Q2"))!);
        var seeded = ReleaseMapper.ToRows(new ReleaseRow(ReleaseIds.Live, ReleaseKinds.Live, "الربع الثاني 2026", "2026-Q2", "الربع الثاني 2026", DateTimeOffset.Now, null), q2);
        await repo.WriteReleaseAsync(seeded);
        var corrections = new CorrectionsFile([], ["ملاحظة الربع الثاني"]);
        await repo.WriteCorrectionsAsync(ReleaseIds.Live, corrections);
        var converter = new RentalControlsConverterService(source, repo, NullLogger<RentalControlsConverterService>.Instance, new ReleaseWriter(repo));

        var first = await converter.ConvertAllAsync();
        var frozenAt = repo.GetReleases().Single(r => r.Id == "2026-Q2").UpdatedAt;
        var again = await converter.ConvertAllAsync();

        Assert.True(first.Success, string.Join("; ", first.Errors));
        Assert.True(again.Success, string.Join("; ", again.Errors));
        Assert.Equal(new[] { "2026-Q2", "live" }, repo.GetReleases().Select(r => r.Id));
        Assert.Equal(seeded.Observations.Select(o => o.Value), (await repo.LoadReleaseAsync("2026-Q2"))!.Observations.Select(o => o.Value));
        Assert.Equal(corrections.Notes, (await repo.LoadCorrectionsAsync("2026-Q2")).Notes);
        Assert.Equal(frozenAt, repo.GetReleases().Single(r => r.Id == "2026-Q2").UpdatedAt); // frozen once
    }
}
