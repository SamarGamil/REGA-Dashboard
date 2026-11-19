namespace AqarInsights.Api.Services.RentalControls.Snapshots;

/// <summary>
/// What a resolved period shows: its quarter, the selected month (month views only) and the month the source's written
/// text was made for (monthly sentences are kept only in that month and in quarter views).
/// </summary>
public sealed record PeriodScope(YearQuarter? Quarter, YearMonth? Month, YearMonth? TextMonth)
{
    /// <summary>No period rules (data with no periods at all).</summary>
    public static PeriodScope None { get; } = new(null, null, null);

    public bool IsMonth => Month is not null;
}
