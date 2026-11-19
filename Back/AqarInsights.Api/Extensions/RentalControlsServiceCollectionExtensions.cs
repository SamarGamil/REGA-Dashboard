using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Tables;
using AqarInsights.Api.Services.RentalControls;
using AqarInsights.Api.Services.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Store;
using AqarInsights.Api.Services.RentalControls.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AqarInsights.Api.Extensions;

public static class RentalControlsServiceCollectionExtensions
{
    // Rental-controls dashboard: Excel import, release tables, the live release, aggregation, and tuning options.
    public static IServiceCollection AddRentalControlsServices(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var sourceDir = DataPaths.RentalControlsSourceDir(configuration, environment);
        var tablesDir = DataPaths.RentalControlsTablesDir(configuration, environment);

        services.AddSingleton<IRentalControlsRepository>(sp =>
            new JsonTablesRepository(tablesDir, sp.GetRequiredService<ILogger<JsonTablesRepository>>()));
        services.AddSingleton(sp =>
            new ReleaseWriter(sp.GetRequiredService<IRentalControlsRepository>(), sp.GetRequiredService<ILogger<ReleaseWriter>>()));
        services.AddSingleton<IRentalControlsConverterService>(sp => new RentalControlsConverterService(
            sourceDir, sp.GetRequiredService<IRentalControlsRepository>(),
            sp.GetRequiredService<ILogger<RentalControlsConverterService>>(), sp.GetRequiredService<ReleaseWriter>()));
        // The live release serves both the dashboard data and the report narrative.
        services.AddSingleton(sp => new TableRentalControlsStore(
            sp.GetRequiredService<IRentalControlsRepository>(), ReleaseIds.Live, sp.GetRequiredService<ILogger<TableRentalControlsStore>>()));
        services.AddSingleton<IRentalControlsStore>(sp => sp.GetRequiredService<TableRentalControlsStore>());
        services.AddSingleton<ICityReportContentStore>(sp => sp.GetRequiredService<TableRentalControlsStore>());
        services.AddSingleton<IRentalControlsAggregationService, RentalControlsAggregationService>();

        services.Configure<MarketAnalysisOptions>(configuration.GetSection("MarketAnalysis"));
        services.Configure<KpiThresholdsOptions>(configuration.GetSection("KpiThresholds"));
        services.Configure<RentalPolicyOptions>(configuration.GetSection("RentalPolicy"));

        return services;
    }
}