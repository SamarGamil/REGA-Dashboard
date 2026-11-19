using System.Text.Json;
using AqarInsights.Api.Models.RentalControls.Tables;
using AqarInsights.Api.Services.RentalControls.Tables;

namespace AqarInsights.Api.Tests.RentalControls.Tables;

public sealed class JsonTablesRepositoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"rc-tables-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private JsonTablesRepository Repo() => new(Path.Combine(_root, "tables"));

    private static ReleaseData Sample(string id) => new(new ReleaseRow(
        id, id == ReleaseIds.Live ? ReleaseKinds.Live : ReleaseKinds.Quarterly, "الربع الثاني 2026", "2026-Q2",
        "الربع الثاني 2026", new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.FromHours(3)), null))
    {
        Cities = [new(id, "riyadh", 0, "الرياض", true, true)],
        CityStatus = [new(id, "riyadh", "مرتفع", "الضوابط مفعلة", "سطر 1", "سطر 2")],
        Observations =
        [
            new(id, 0, "riyadh", "kpi.cagr", Grains.Latest, "", "", 0.0888427784540666, null),
            new(id, 1, "riyadh", "city.rentIndex", Grains.Month, "2026-06", "الرقم القياسي", 104.3, "+1.2%"),
        ],
        MarketMovement = [new(id, "riyadh", 0, "التضخم العام (CPI)", "مرتفع", "+2.2%", "متابعة")],
        CityNarrative = [new(id, "riyadh", "executive", 0, "نص")],
        ReportProducts = [new(id, "riyadh", 0, "شقة", "+5%", "-3%", "101.2")],
        ReportAxes = [new(id, "riyadh", 0, "الطلب", "مرتفع")],
        MediaMonitoring = [new(id, "riyadh", 0, "2026-06", true, "محايد", 30, 50, 20, 12.5)],
        MediaItems = [new(id, "riyadh", 0, "topic", 0, "موضوع")],
        GapCities = [new(id, "riyadh", 0)],
    };

    private static string Json(ReleaseData data) => JsonSerializer.Serialize(data);

    [Fact]
    public async Task Corrections_round_trip_and_are_not_a_release()
    {
        var repo = Repo();
        await repo.WriteReleaseAsync(Sample(ReleaseIds.Live));
        var file = new CorrectionsFile(
            [new("c1", "observations", new() { ["cityId"] = JsonSerializer.SerializeToElement("riyadh") }, "value",
                JsonSerializer.SerializeToElement(1), JsonSerializer.SerializeToElement(2), "سبب", "2026-11-02")],
            ["ملاحظة"]);

        await repo.WriteCorrectionsAsync(ReleaseIds.Live, file);

        Assert.Equal(JsonSerializer.Serialize(file), JsonSerializer.Serialize(await repo.LoadCorrectionsAsync(ReleaseIds.Live)));
        Assert.Same(CorrectionsFile.Empty, await repo.LoadCorrectionsAsync("2026-Q2"));
        Assert.Equal(new[] { "live" }, repo.GetReleases().Select(r => r.Id));
    }

    [Fact]
    public async Task A_malformed_corrections_file_fails_with_its_path()
    {
        var repo = Repo();
        Directory.CreateDirectory(Path.Combine(_root, "tables", "corrections"));
        File.WriteAllText(Path.Combine(_root, "tables", "corrections", "live.json"), "{ \"corrections\": [ { \"id\": ");

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => repo.LoadCorrectionsAsync(ReleaseIds.Live));

        Assert.Contains(Path.Combine("corrections", "live.json"), ex.Message);
    }

    [Fact]
    public async Task Monthly_movement_round_trips_and_a_release_can_be_deleted()
    {
        var repo = Repo();
        var rows = new List<MonthlyMovementRow>
        {
            new("2026-08", "riyadh", 0, "الإيجار الفعلي للمسكن (CPI)", "مدينة - \u200E2026-08", "+5.1%", "ارتفاع", new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)),
        };
        Assert.Empty(await repo.LoadMonthlyMovementAsync());

        await repo.WriteMonthlyMovementAsync(rows);
        await repo.WriteReleaseAsync(Sample("2026-Q2"));
        await repo.DeleteReleaseAsync("2026-Q2");
        await repo.DeleteReleaseAsync("2026-Q2");

        Assert.Equal(rows, await repo.LoadMonthlyMovementAsync());
        Assert.Empty(repo.GetReleases());
    }

    [Fact]
    public async Task Written_release_loads_back_identically()
    {
        var repo = Repo();

        await repo.WriteReleaseAsync(Sample("2026-Q2"));

        Assert.Equal(Json(Sample("2026-Q2")), Json((await repo.LoadReleaseAsync("2026-Q2"))!));
    }

    [Fact]
    public async Task Tables_are_json_arrays_with_one_row_per_line()
    {
        var repo = Repo();

        await repo.WriteReleaseAsync(Sample(ReleaseIds.Live));

        var lines = File.ReadAllLines(Path.Combine(repo.Location(ReleaseIds.Live), TableFiles.Observations));
        Assert.Equal("[", lines[0]);
        Assert.Equal("]", lines[^1]);
        Assert.Equal(2, lines.Length - 2);
        Assert.All(lines[1..^1], line => Assert.StartsWith("{\"releaseId\":\"live\",\"seq\":", line));
        Assert.DoesNotContain("note", lines[1]);
        Assert.Contains("\"note\":\"+1.2%\"", lines[2]);
        Assert.Contains("الرياض", File.ReadAllText(Path.Combine(repo.Location(ReleaseIds.Live), TableFiles.Cities)));
    }

    [Fact]
    public async Task Lists_releases_by_id_and_skips_staging_folders()
    {
        var repo = Repo();
        await repo.WriteReleaseAsync(Sample(ReleaseIds.Live));
        await repo.WriteReleaseAsync(Sample("2026-Q2"));
        var staging = Directory.CreateDirectory(Path.Combine(repo.TablesDir, ".2026-08.abc.tmp")).FullName;
        File.Copy(Path.Combine(repo.Location(ReleaseIds.Live), TableFiles.Release), Path.Combine(staging, TableFiles.Release));

        Assert.Equal(new[] { "2026-Q2", "live" }, repo.GetReleases().Select(r => r.Id));
    }

    [Fact]
    public async Task Unknown_release_loads_as_null_and_a_missing_table_as_empty()
    {
        var repo = Repo();
        Assert.Null(await repo.LoadReleaseAsync("2026-08"));
        Assert.Empty(repo.GetReleases());

        await repo.WriteReleaseAsync(Sample(ReleaseIds.Live));
        File.Delete(Path.Combine(repo.Location(ReleaseIds.Live), TableFiles.MediaItems));

        Assert.Empty((await repo.LoadReleaseAsync(ReleaseIds.Live))!.MediaItems);
    }

    [Fact]
    public async Task Rewriting_replaces_the_tables_but_keeps_earlier_source_workbooks()
    {
        var repo = Repo();
        var source = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        File.WriteAllText(Path.Combine(source, "workbook.xlsx"), "x");
        await repo.WriteReleaseAsync(Sample("2026-Q2"), source);
        File.WriteAllText(Path.Combine(repo.Location("2026-Q2"), "stale.json"), "[]");

        await repo.WriteReleaseAsync(Sample("2026-Q2"));

        Assert.False(File.Exists(Path.Combine(repo.Location("2026-Q2"), "stale.json")));
        Assert.True(File.Exists(Path.Combine(repo.Location("2026-Q2"), JsonTablesRepository.SourceFolder, "workbook.xlsx")));
        Assert.Empty(Directory.GetDirectories(repo.TablesDir, ".*"));
    }

    [Fact]
    public async Task Rejects_rows_of_another_release_and_unsafe_ids()
    {
        var repo = Repo();
        var mixed = Sample(ReleaseIds.Live) with { Cities = [new("2026-Q2", "riyadh", 0, "الرياض", true, true)] };

        await Assert.ThrowsAsync<ArgumentException>(() => repo.WriteReleaseAsync(mixed));
        await Assert.ThrowsAsync<ArgumentException>(() => repo.LoadReleaseAsync("../live"));
        await Assert.ThrowsAsync<ArgumentException>(() => repo.LoadReleaseAsync("live\n"));
    }

    [Fact]
    public async Task Skips_folders_that_are_not_release_ids_or_hold_another_release()
    {
        var repo = Repo();
        await repo.WriteReleaseAsync(Sample("2026-Q2"));
        CopyFolder(repo.Location("2026-Q2"), Path.Combine(repo.TablesDir, "2026-Q3")); // holds release 2026-Q2
        var odd = Directory.CreateDirectory(Path.Combine(repo.TablesDir, "2026-6")).FullName;
        File.WriteAllText(Path.Combine(odd, TableFiles.Release),
            File.ReadAllText(Path.Combine(repo.Location("2026-Q2"), TableFiles.Release)).Replace("\"2026-Q2\"", "\"2026-6\""));

        Assert.Equal(new[] { "2026-Q2" }, repo.GetReleases().Select(r => r.Id));
        await Assert.ThrowsAsync<InvalidDataException>(() => repo.LoadReleaseAsync("2026-Q3"));
    }

    [Fact]
    public async Task Loading_rejects_a_row_of_another_release()
    {
        var repo = Repo();
        await repo.WriteReleaseAsync(Sample("2026-Q2"));
        var cities = Path.Combine(repo.Location("2026-Q2"), TableFiles.Cities);
        File.WriteAllText(cities, File.ReadAllText(cities).Replace("\"releaseId\":\"2026-Q2\"", "\"releaseId\":\"live\""));

        await Assert.ThrowsAsync<InvalidDataException>(() => repo.LoadReleaseAsync("2026-Q2"));
    }

    private static void CopyFolder(string from, string to)
    {
        foreach (var dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, dir)));
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(to, Path.GetRelativePath(from, file)));
    }
}
