namespace AqarInsights.Api.Models.RentalControls.Reports;

// One persisted report-generation request. Normalized/flat — maps 1:1 to a future DB row.
public class ReportHistoryRecord
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Scope { get; set; } = "";
    public string Period { get; set; } = "";
    public string? PeriodId { get; set; }           // yyyy-MM | yyyy-Qn; absent on records made before period selection
    public string Format { get; set; } = "";      // pdf | word
    public string Status { get; set; } = "";       // ready | failed
    public string FileName { get; set; } = "";
    public string? RequestedBy { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
}
