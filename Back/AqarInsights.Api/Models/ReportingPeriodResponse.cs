namespace AqarInsights.Api.Models;

// Reporting-period contract shared by the foreign-ownership and rental-controls dashboards.
// LastUpdated is a finer-grained "as of" label where the domain has one (empty otherwise).
public sealed record ReportingPeriodResponse(string Period, string LastUpdated);
