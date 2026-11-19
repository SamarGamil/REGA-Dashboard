using Microsoft.AspNetCore.Mvc;
using AqarInsights.Api.Controllers;
using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Models.RentalControls.Snapshots;
using AqarInsights.Api.Services.RentalControls;
using AqarInsights.Api.Services.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Snapshots;
using Xunit;

namespace AqarInsights.Api.Tests.RentalControls.Reports;

public class ReportsControllerTests
{
    private sealed class FakeReports : INationalReportService
    {
        public RentalControlsPeriodData? LastPeriod { get; private set; }

        public ReportFile GenerateNational(RentalControlsPeriodData period, ReportFormat format)
        {
            LastPeriod = period;
            return format == ReportFormat.Word
                ? new ReportFile(new byte[] { 1 }, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "n.docx")
                : new ReportFile(new byte[] { 2 }, "application/pdf", "n.pdf");
        }
    }

    private sealed class FakeCityReports : ICityReportService
    {
        public ReportFile? GenerateCity(RentalControlsPeriodData period, string cityId, ReportFormat format)
        {
            if (cityId != "riyadh") return null;
            return format == ReportFormat.Word
                ? new ReportFile(new byte[] { 3 }, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "c.docx")
                : new ReportFile(new byte[] { 4 }, "application/pdf", "c.pdf");
        }
    }

    private sealed class FakeHistoryStore : IReportHistoryStore
    {
        public List<ReportHistoryRecord> Records { get; } = new();

        public Task<IReadOnlyList<ReportHistoryRecord>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult((IReadOnlyList<ReportHistoryRecord>)Records);

        public Task AppendAsync(ReportHistoryRecord record, CancellationToken cancellationToken = default)
        {
            Records.Add(record);
            return Task.CompletedTask;
        }
    }

    // Resolves the current quarter (no period), "2026-Q2" and "2026-08"; anything else is unknown.
    private sealed class FakePeriods : IRentalControlsPeriodService
    {
        private static readonly FakeAggregation Agg = new();

        public ReportingPeriodsResponse GetPeriods() => new("2026-Q3",
            new[] { new ReportingPeriodOption("2026-08", "أغسطس 2026") },
            new[] { new ReportingPeriodOption("2026-Q3", "الربع الثالث 2026"), new ReportingPeriodOption("2026-Q2", "الربع الثاني 2026") });

        public Task<RentalControlsPeriodData?> ResolveAsync(string? period, CancellationToken cancellationToken = default)
            => Task.FromResult<RentalControlsPeriodData?>(period switch
            {
                null or "" => new("2026-Q3", "الربع الثالث 2026", false, Agg, new NullContent(), PeriodScope.None),
                "2026-Q2" => new("2026-Q2", "الربع الثاني 2026", false, Agg, new NullContent(), PeriodScope.None),
                "2026-08" => new("2026-08", "أغسطس 2026", true, Agg, new NullContent(), PeriodScope.None),
                _ => null,
            });
    }

    private sealed class NullContent : ICityReportContentStore
    {
        public CityReportContent? Get(string cityId) => null;
    }

    private sealed class FakeAggregation : IRentalControlsAggregationService
    {
        public void Recompute() { }
        public OverviewData GetOverview() => new();
        public IndicatorsData GetIndicators() => new();
        public IReadOnlyList<CityListItem> GetCityIndex() => Array.Empty<CityListItem>();
        public CityAnalysisData? GetCity(string cityId) => cityId == "riyadh" ? new CityAnalysisData { CityName = "الرياض" } : null;
        public MarketAnalysisData GetMarketAnalysis() => new();
    }

    private static ReportsController Create(FakeHistoryStore? store = null, FakeReports? reports = null)
        => new(reports ?? new FakeReports(), new FakeCityReports(), store ?? new FakeHistoryStore(), new FakePeriods());

    [Theory]
    [InlineData("pdf", "application/pdf")]
    [InlineData("word", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    public async Task National_returns_file_for_supported_format(string format, string contentType)
    {
        var controller = Create();
        var result = Assert.IsType<FileContentResult>(await controller.GetNational(format));
        Assert.Equal(contentType, result.ContentType);
        Assert.NotEmpty(result.FileContents);
    }

    [Fact]
    public async Task National_records_history_on_generate()
    {
        var store = new FakeHistoryStore();
        var controller = Create(store);

        await controller.GetNational("pdf");

        var record = Assert.Single(store.Records);
        Assert.Equal("pdf", record.Format);
        Assert.Equal("ready", record.Status);
        Assert.Equal("الربع الثالث 2026", record.Period);
        Assert.Equal("2026-Q3", record.PeriodId);
        Assert.Equal("n.pdf", record.FileName);
        Assert.False(string.IsNullOrWhiteSpace(record.Id));
    }

    [Fact]
    public void Periods_lists_available_months_and_quarters()
    {
        var ok = Assert.IsType<OkObjectResult>(Create().GetPeriods());
        var periods = Assert.IsType<ReportingPeriodsResponse>(ok.Value);
        Assert.Equal("2026-Q3", periods.Current);
        Assert.Equal(new[] { "2026-Q3", "2026-Q2" }, periods.Quarterly.Select(p => p.Id));
        Assert.Equal(new[] { "2026-08" }, periods.Monthly.Select(p => p.Id));
    }

    [Theory]
    [InlineData("2026-Q2", "الربع الثاني 2026")]
    [InlineData("2026-08", "أغسطس 2026")]
    public async Task National_generates_and_records_the_requested_period(string period, string label)
    {
        var store = new FakeHistoryStore();
        var reports = new FakeReports();
        var controller = Create(store, reports);

        Assert.IsType<FileContentResult>(await controller.GetNational("pdf", period: period));

        Assert.Equal(period, reports.LastPeriod!.Id);
        var record = Assert.Single(store.Records);
        Assert.Equal(label, record.Period);
        Assert.Equal(period, record.PeriodId);
    }

    [Fact]
    public async Task Unknown_period_returns_404_without_recording()
    {
        var store = new FakeHistoryStore();
        var controller = Create(store);

        var national = Assert.IsType<ObjectResult>(await controller.GetNational("pdf", period: "2026-07"));
        var city = Assert.IsType<ObjectResult>(await controller.GetCity("riyadh", "pdf", period: "2025-Q1"));

        Assert.Equal(404, national.StatusCode);
        Assert.Equal(404, city.StatusCode);
        Assert.Empty(store.Records);
    }

    [Fact]
    public async Task National_rejects_unknown_format_without_recording()
    {
        var store = new FakeHistoryStore();
        var controller = Create(store);

        var r = Assert.IsType<ObjectResult>(await controller.GetNational("xml"));
        Assert.Equal(400, r.StatusCode);
        Assert.Empty(store.Records);
    }

    [Fact]
    public async Task National_with_record_false_does_not_log_history()
    {
        var store = new FakeHistoryStore();
        var controller = Create(store);

        var result = Assert.IsType<FileContentResult>(await controller.GetNational("pdf", record: false));
        Assert.NotEmpty(result.FileContents);
        Assert.Empty(store.Records);
    }

    [Fact]
    public async Task History_returns_recorded_items()
    {
        var store = new FakeHistoryStore();
        var controller = Create(store);
        await controller.GetNational("pdf");
        await controller.GetNational("word");

        var ok = Assert.IsType<OkObjectResult>(await controller.GetHistory());
        var items = Assert.IsAssignableFrom<IEnumerable<ReportHistoryRecord>>(ok.Value).ToList();
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task History_recovers_the_period_of_entries_recorded_before_period_selection()
    {
        var store = new FakeHistoryStore();
        store.Records.Add(new ReportHistoryRecord { Id = "old-q2", Period = "الربع الثاني 2026", RequestedAt = DateTimeOffset.UtcNow.AddDays(-30) });
        store.Records.Add(new ReportHistoryRecord { Id = "old-unknown", Period = "الربع الرابع 2025", RequestedAt = DateTimeOffset.UtcNow.AddDays(-60) });
        store.Records.Add(new ReportHistoryRecord { Id = "new", Period = "أغسطس 2026", PeriodId = "2026-08", RequestedAt = DateTimeOffset.UtcNow });

        var ok = Assert.IsType<OkObjectResult>(await Create(store).GetHistory());
        var items = Assert.IsAssignableFrom<IEnumerable<ReportHistoryRecord>>(ok.Value).ToDictionary(r => r.Id);

        Assert.Equal("2026-Q2", items["old-q2"].PeriodId);
        Assert.Null(items["old-unknown"].PeriodId); // no data for that period: left as recorded
        Assert.Equal("2026-08", items["new"].PeriodId);
        Assert.Null(store.Records[0].PeriodId);      // the stored history itself is not rewritten
    }

    [Theory]
    [InlineData("pdf", "application/pdf")]
    [InlineData("word", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    public async Task City_returns_file_for_supported_format(string format, string contentType)
    {
        var controller = Create();
        var result = Assert.IsType<FileContentResult>(await controller.GetCity("riyadh", format));
        Assert.Equal(contentType, result.ContentType);
        Assert.NotEmpty(result.FileContents);
    }

    [Fact]
    public async Task City_records_history_with_city_scope()
    {
        var store = new FakeHistoryStore();
        var controller = Create(store);

        await controller.GetCity("riyadh", "pdf", period: "2026-08");

        var record = Assert.Single(store.Records);
        Assert.Equal("تقرير مدينة", record.Type);
        Assert.Equal("الرياض", record.Scope);
        Assert.Equal("أغسطس 2026", record.Period);
        Assert.Equal("2026-08", record.PeriodId);
        Assert.Equal("c.pdf", record.FileName);
    }

    [Fact]
    public async Task City_returns_404_for_unknown_city_without_recording()
    {
        var store = new FakeHistoryStore();
        var controller = Create(store);

        var r = Assert.IsType<ObjectResult>(await controller.GetCity("atlantis", "pdf"));
        Assert.Equal(404, r.StatusCode);
        Assert.Empty(store.Records);
    }

    [Fact]
    public async Task City_rejects_unknown_format_without_recording()
    {
        var store = new FakeHistoryStore();
        var controller = Create(store);

        var r = Assert.IsType<ObjectResult>(await controller.GetCity("riyadh", "xml"));
        Assert.Equal(400, r.StatusCode);
        Assert.Empty(store.Records);
    }

    [Fact]
    public async Task City_with_record_false_does_not_log_history()
    {
        var store = new FakeHistoryStore();
        var controller = Create(store);

        var result = Assert.IsType<FileContentResult>(await controller.GetCity("riyadh", "pdf", record: false));
        Assert.NotEmpty(result.FileContents);
        Assert.Empty(store.Records);
    }
}
