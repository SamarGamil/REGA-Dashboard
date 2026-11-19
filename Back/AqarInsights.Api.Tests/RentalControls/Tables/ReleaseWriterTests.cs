using AqarInsights.Api.Models.RentalControls.Tables;
using AqarInsights.Api.Services.RentalControls.Tables;
using AqarInsights.Api.Tests.RentalControls.Reports;

namespace AqarInsights.Api.Tests.RentalControls.Tables;

// Live data: September 2026 (latest CPI month), latest quarter 2026-Q3. Releases are closed quarters only.
public sealed class ReleaseWriterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"rc-releases-{Guid.NewGuid():N}");
    private readonly JsonTablesRepository _repo;

    public ReleaseWriterTests() => _repo = new JsonTablesRepository(Path.Combine(_root, "tables"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private ReleaseWriter Writer(TimeProvider? clock = null) => new(_repo, clock: clock);

    private static MappedRelease Live => RealReportData.LiveRelease;

    private async Task<MappedRelease> Stored(string id) => ReleaseMapper.FromRows((await _repo.LoadReleaseAsync(id))!);

    private static TimeProvider Clock(int year, int month) => new FixedTimeProvider(new DateTimeOffset(year, month, 15, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Rewriting_a_quarter_replaces_it_but_keeps_earlier_source_workbooks()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        File.WriteAllText(Path.Combine(source, "workbook.xlsx"), "x");
        await Writer().WriteAsync(Live, "2026-Q3", sourceDir: source, sourceCommit: "abc1234");
        File.WriteAllText(Path.Combine(_repo.Location("2026-Q3"), "stale.json"), "[]");

        await Writer().WriteAsync(Live, "2026-Q3");

        Assert.False(File.Exists(Path.Combine(_repo.Location("2026-Q3"), "stale.json")));
        Assert.True(File.Exists(Path.Combine(_repo.Location("2026-Q3"), JsonTablesRepository.SourceFolder, "workbook.xlsx")));
        Assert.Empty(Directory.GetDirectories(_repo.TablesDir, ".*"));
    }

    [Fact]
    public async Task Returns_null_for_an_empty_dataset()
    {
        Assert.Null(await Writer().WriteAsync(MappedRelease.Empty, "2026-Q3"));
    }

    // Releases are closed quarters only: a month (or anything else) is not a release period.
    [Theory]
    [InlineData("2026-09")]
    [InlineData("2026-Q5")]
    [InlineData("")]
    public async Task Rejects_anything_but_a_quarter(string period)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Writer().WriteAsync(Live, period));
    }

    // Guard against workbook date typos: a release may never be dated after the current month.
    [Fact]
    public async Task Rejects_a_quarter_in_the_future()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Writer(Clock(2026, 6)).WriteAsync(Live, "2026-Q3"));

        Assert.Contains("future", ex.Message);
    }
    [Fact]
    public async Task Quarter_period_keeps_the_release_untrimmed()
    {
        var release = await Writer().WriteAsync(Live, "2026-Q3");

        Assert.Equal(("2026-Q3", ReleaseKinds.Quarterly, "الربع الثالث 2026"), (release!.Id, release.Kind, release.Label));
        var stored = (await Stored("2026-Q3")).CityRaw["riyadh"];
        Assert.Equal(Live.CityRaw["riyadh"].Series.Count, stored.Series.Count);
        Assert.Equal(Live.CityRaw["riyadh"].Media!.Count, stored.Media!.Count);
    }

    [Fact]
    public async Task Quarter_period_must_be_the_data_latest_quarter()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Writer().WriteAsync(Live, "2026-Q2"));
    }
}
