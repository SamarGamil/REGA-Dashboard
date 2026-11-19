using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AqarInsights.Api.Extensions;

// Central resolution of on-disk data locations from configuration, so the DI registrations
// and the CLI import handlers share one source of truth for paths and their defaults.
internal static class DataPaths
{
    public static string InputDir(IConfiguration c, IHostEnvironment e) => Resolve(c, e, "DataSettings", "InputDir", "Data");
    public static string OutputDir(IConfiguration c, IHostEnvironment e) => Resolve(c, e, "DataSettings", "OutputDir", "../Data");
    public static string TransactionsFile(IConfiguration c, IHostEnvironment e) => Resolve(c, e, "DataSettings", "TransactionsFile", "Data/transactions.json");
    public static string FilterOptionsFile(IConfiguration c, IHostEnvironment e) => Resolve(c, e, "DataSettings", "FilterOptionsFile", "Data/filter-options-updated.json");

    public static string UsersFile(IConfiguration c, IHostEnvironment e) => Resolve(c, e, "AuthSettings", "UsersFile", "Data/users-accounts.json");

    public static string RentalControlsSourceDir(IConfiguration c, IHostEnvironment e) => Resolve(c, e, "RentalControlsDataSettings", "SourceDir", "Data/rental-controls-source");
    public static string RentalControlsOutputDir(IConfiguration c, IHostEnvironment e) => Resolve(c, e, "RentalControlsDataSettings", "OutputDir", "Data/rental-controls");
    public static string RentalControlsTablesDir(IConfiguration c, IHostEnvironment e) => Path.Combine(RentalControlsOutputDir(c, e), "tables");

    // Report-history path may live outside the read-only deployment folder and can use env vars (e.g. %HOME%).
    public static string ReportsHistoryFile(IConfiguration c, IHostEnvironment e)
    {
        var configured = c["Reports:HistoryFile"];
        return string.IsNullOrWhiteSpace(configured)
            ? Path.GetFullPath("Data/reports/report-history.json", e.ContentRootPath)
            : Path.GetFullPath(Environment.ExpandEnvironmentVariables(configured), e.ContentRootPath);
    }

    private static string Resolve(IConfiguration c, IHostEnvironment e, string section, string key, string fallback)
        => Path.GetFullPath(c.GetSection(section)[key] ?? fallback, e.ContentRootPath);
}
