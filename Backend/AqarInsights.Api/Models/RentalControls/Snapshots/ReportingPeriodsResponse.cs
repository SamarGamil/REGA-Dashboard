namespace AqarInsights.Api.Models.RentalControls.Snapshots;

public record ReportingPeriodOption(string Id, string Label);

// Periods a report can be generated for. Current is the live dataset's quarter id.
public record ReportingPeriodsResponse(
    string Current,
    IReadOnlyList<ReportingPeriodOption> Monthly,
    IReadOnlyList<ReportingPeriodOption> Quarterly);
