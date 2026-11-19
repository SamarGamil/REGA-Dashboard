using AqarInsights.Api.Models.RentalControls.Tables;
using AqarInsights.Api.Services.RentalControls;
using AqarInsights.Api.Services.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Snapshots;
using AqarInsights.Api.Services.RentalControls.Store;
using AqarInsights.Api.Services.RentalControls.Tables;

namespace AqarInsights.Api.Tests.RentalControls.Reports;

// Committed platform data (Data/rental-controls/tables: the live release + frozen releases), loaded once per test run.
internal static class RealReportData
{
    public static string SolutionRoot { get; } = FindSolutionRoot();

    public static string DataDir { get; } = Path.Combine(SolutionRoot, "AqarInsights.Api", "Data", "rental-controls");

    public static string SourceDir { get; } = Path.Combine(SolutionRoot, "AqarInsights.Api", "Data", "rental-controls-source");

    public static string NarrativeSourceDir { get; } = Path.Combine(SourceDir, RentalControlsConverterService.NarrativeFolder);

    public static string TablesDir { get; } = Path.Combine(DataDir, "tables");

    public static JsonTablesRepository Repository { get; } = new(TablesDir);

    private static readonly Lazy<(TableRentalControlsStore Store, RentalControlsAggregationService Aggregation)> LazyLive = new(Load);
    private static readonly Lazy<RentalControlsPeriodService> LazyPeriods = new(() => new RentalControlsPeriodService(
        Repository, LazyLive.Value.Store, LazyLive.Value.Aggregation, LazyLive.Value.Store));

    public static IRentalControlsStore LiveStore => LazyLive.Value.Store;

    public static ICityReportContentStore LiveContent => LazyLive.Value.Store;

    public static MappedRelease LiveRelease => LazyLive.Value.Store.Data;

    public static RentalControlsAggregationService Aggregation => LazyLive.Value.Aggregation;

    // Live release + stored releases, as the API resolves report periods.
    public static RentalControlsPeriodService Periods => LazyPeriods.Value;

    public static RentalControlsPeriodData Current => Periods.ResolveAsync(null).GetAwaiter().GetResult()!;

    private static (TableRentalControlsStore, RentalControlsAggregationService) Load()
    {
        var store = new TableRentalControlsStore(Repository, ReleaseIds.Live);
        store.LoadAsync().GetAwaiter().GetResult();
        var service = new RentalControlsAggregationService(store);
        service.Recompute();
        return (store, service);
    }

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AqarInsights.Api.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate AqarInsights.Api.sln above the test output directory");
    }
}