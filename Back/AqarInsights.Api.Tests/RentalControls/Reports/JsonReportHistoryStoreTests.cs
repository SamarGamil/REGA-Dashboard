using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Reports;
using Xunit;

namespace AqarInsights.Api.Tests.RentalControls.Reports;

public class JsonReportHistoryStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _file;

    public JsonReportHistoryStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "report-history-tests", Guid.NewGuid().ToString());
        _file = Path.Combine(_dir, "report-history.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static ReportHistoryRecord Record(string id) => new()
    {
        Id = id,
        Name = "تقرير الضوابط الإيجارية الوطني",
        Type = "التقرير الوطني",
        Scope = "جميع المدن",
        Period = "الربع الثاني 2026",
        Format = "pdf",
        Status = "ready",
        FileName = "n.pdf",
        RequestedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task GetAll_returns_empty_when_file_missing()
    {
        var store = new JsonReportHistoryStore(_file);
        Assert.Empty(await store.GetAllAsync());
    }

    [Fact]
    public async Task Append_persists_and_survives_a_new_store_instance()
    {
        var store = new JsonReportHistoryStore(_file);
        await store.AppendAsync(Record("a"));
        await store.AppendAsync(Record("b"));

        var reloaded = new JsonReportHistoryStore(_file);
        var items = await reloaded.GetAllAsync();

        Assert.Equal(2, items.Count);
        Assert.Equal("a", items[0].Id);
        Assert.Equal("b", items[1].Id);
        Assert.Equal("الربع الثاني 2026", items[0].Period);
    }
}
