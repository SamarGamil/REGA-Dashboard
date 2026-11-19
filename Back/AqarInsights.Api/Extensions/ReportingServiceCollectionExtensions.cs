using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls;
using AqarInsights.Api.Services.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Snapshots;
using AqarInsights.Api.Services.RentalControls.Store;
using AqarInsights.Api.Services.RentalControls.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AqarInsights.Api.Extensions;

public static class ReportingServiceCollectionExtensions
{
    // National + per-city report generation, reporting periods (live + stored releases) and request history.
    public static IServiceCollection AddReportingServices(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var historyFile = DataPaths.ReportsHistoryFile(configuration, environment);

        services.Configure<NationalReportOptions>(configuration.GetSection("Reports:NationalReport"));
        services.AddScoped<INationalReportService, NationalReportService>();
        services.AddScoped<ICityReportService, CityReportService>();
        services.AddSingleton<IRentalControlsPeriodService>(sp => new RentalControlsPeriodService(
            sp.GetRequiredService<IRentalControlsRepository>(),
            sp.GetRequiredService<IRentalControlsStore>(),
            sp.GetRequiredService<IRentalControlsAggregationService>(),
            sp.GetRequiredService<ICityReportContentStore>(),
            sp.GetService<IOptions<MarketAnalysisOptions>>(),
            sp.GetService<IOptions<KpiThresholdsOptions>>(),
            sp.GetService<IOptions<RentalPolicyOptions>>(),
            sp.GetRequiredService<ILogger<RentalControlsPeriodService>>()));
        services.AddSingleton<IReportHistoryStore>(sp =>
            new JsonReportHistoryStore(historyFile, sp.GetRequiredService<ILogger<JsonReportHistoryStore>>()));

        return services;
    }
}
