using AqarInsights.Api.Services.ForeignOwnership;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AqarInsights.Api.Extensions;

public static class ForeignOwnershipServiceCollectionExtensions
{
    // Foreign-ownership dashboard: Excel import, transaction loading, and aggregation.
    public static IServiceCollection AddForeignOwnershipServices(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var inputDir = DataPaths.InputDir(configuration, environment);
        var outputDir = DataPaths.OutputDir(configuration, environment);
        var transactionsFile = DataPaths.TransactionsFile(configuration, environment);
        var filterOptionsFile = DataPaths.FilterOptionsFile(configuration, environment);

        services.AddSingleton<IExcelConverterService>(sp =>
            new ExcelConverterService(inputDir, outputDir, filterOptionsFile, sp.GetRequiredService<ILogger<ExcelConverterService>>()));
        services.AddSingleton<IDataLoaderService>(sp =>
            new DataLoaderService(transactionsFile, sp.GetRequiredService<ILogger<DataLoaderService>>()));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAggregationService, AggregationService>();
        services.AddSingleton<IFilterOptionsService>(sp =>
            new FilterOptionsService(filterOptionsFile, sp.GetRequiredService<IDataLoaderService>(), sp.GetRequiredService<ILogger<FilterOptionsService>>()));

        return services;
    }
}
