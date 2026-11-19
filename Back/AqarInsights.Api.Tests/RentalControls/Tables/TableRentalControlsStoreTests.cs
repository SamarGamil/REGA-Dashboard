using System.Text.Json;
using AqarInsights.Api.Models.RentalControls.Tables;
using AqarInsights.Api.Services.RentalControls.Tables;
using AqarInsights.Api.Tests.RentalControls.Reports;
using static AqarInsights.Api.Tests.RentalControls.Tables.TableTestData;

namespace AqarInsights.Api.Tests.RentalControls.Tables;

public sealed class TableRentalControlsStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"rc-store-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task A_missing_release_loads_as_empty()
    {
        var store = new TableRentalControlsStore(new JsonTablesRepository(Path.Combine(_root, "none")), ReleaseIds.Live);

        await store.LoadAsync();

        Assert.Empty(store.GetCities());
        Assert.Null(store.GetCityRaw("riyadh"));
        Assert.Null(store.Get("riyadh"));
    }

    [Fact]
    public async Task A_fixed_release_serves_its_data_and_narrative()
    {
        var store = new TableRentalControlsStore(Small());

        await store.LoadAsync();

        Assert.Equal("riyadh", Assert.Single(store.GetCities()).Id);
        Assert.Equal(2, store.GetIndicatorSeries().Count);
        Assert.Single(store.GetSupplyDemandGap());
        Assert.NotNull(store.GetCityRaw("riyadh"));
        Assert.Equal("riyadh", store.Get("RIYADH")!.CityId); // case-insensitive, like the narrative files
        Assert.Null(store.Get(""));
        Assert.Null(store.Get("jeddah"));
    }

    [Fact]
    public async Task Loading_a_release_applies_its_guarded_corrections()
    {
        var repo = new JsonTablesRepository(_root);
        var rows = (await RealReportData.Repository.LoadReleaseAsync(ReleaseIds.Live))!;
        await repo.WriteReleaseAsync(rows);
        var cpi = rows.MarketMovement.Single(m => m.CityId == "riyadh" && m.Indicator == "الإيجار الفعلي للمسكن (CPI)");
        JsonElement J(object v) => JsonSerializer.SerializeToElement(v);
        await repo.WriteCorrectionsAsync(ReleaseIds.Live, new(
            [new("t1", "market_movement", new() { ["cityId"] = J("riyadh"), ["seq"] = J(cpi.Seq) }, "change", J(cpi.Change), J("+9.9%"), "test", "2026-11-02")], []));
        var store = new TableRentalControlsStore(repo, ReleaseIds.Live);

        await store.LoadAsync();

        Assert.Equal("+9.9%", store.GetCityRaw("riyadh")!.MovementRows.Single(m => m.Indicator == "الإيجار الفعلي للمسكن (CPI)").Change);
    }
}
