using System.Text.Json;

namespace AqarInsights.Api.Models.RentalControls.Tables;

// tables/monthly_movement.json: the as-published latest-month market-movement rows (CPI ×2, monthly rent change) of
// each month, kept after the month's data is superseded. Month "yyyy-MM".
public sealed record MonthlyMovementRow(
    string Month, string CityId, int Seq, string Indicator, string Level, string Change, string Assessment, DateTimeOffset ImportedAt);

// tables/corrections/<releaseId>.json: one guarded fix. Match selects exactly one row of Table (field names as stored,
// camelCase); applied only while Field still equals From.
public sealed record CorrectionRow(
    string Id, string Table, Dictionary<string, JsonElement> Match, string Field, JsonElement From, JsonElement To,
    string Reason, string Date);

public sealed record CorrectionsFile(IReadOnlyList<CorrectionRow> Corrections, IReadOnlyList<string> Notes)
{
    public static CorrectionsFile Empty { get; } = new([], []);
}

public sealed record CorrectionOutcome(string Id, bool Applied, string Detail);
