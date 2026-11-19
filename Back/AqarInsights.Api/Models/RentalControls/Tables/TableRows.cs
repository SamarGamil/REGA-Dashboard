namespace AqarInsights.Api.Models.RentalControls.Tables;

/// <summary>A row that belongs to one release partition.</summary>
public interface IReleaseRow
{
    string ReleaseId { get; }
}

public static class ReleaseIds
{
    public const string Live = "live";
}

public static class ReleaseKinds
{
    public const string Live = "live";
    public const string Monthly = "monthly";
    public const string Quarterly = "quarterly";
}

public static class Grains
{
    public const string Latest = "latest";
    public const string Year = "year";
    public const string Quarter = "quarter";
    public const string Month = "month";
}

// release.json: one row per partition. Id "live", "yyyy-MM" (monthly) or "yyyy-Qn" (quarterly release).
public sealed record ReleaseRow(
    string Id, string Kind, string Label, string LatestQuarter, string LatestQuarterLabel,
    DateTimeOffset UpdatedAt, string? SourceCommit);

// HasDetail: the city has per-city data (series, bullets, movement, media); HasReport: it has a report narrative.
public sealed record CityRow(string ReleaseId, string CityId, int Seq, string Name, bool HasDetail, bool HasReport) : IReleaseRow;

public sealed record CityStatusRow(
    string ReleaseId, string CityId, string Path, string Status, string AssessmentLine1, string AssessmentLine2) : IReleaseRow;

// Every number. Measure "kpi.*" | "indicator.*" | "city.*" | "gap.*"; grain latest|year|quarter|month;
// period "" | "2026" | "2026-Q2" | "2026-06"; dim "" when unused. A missing value has no row.
public sealed record ObservationRow(
    string ReleaseId, int Seq, string CityId, string Measure, string Grain, string Period, string Dim,
    double Value, string? Note) : IReleaseRow;

public sealed record MarketMovementTableRow(
    string ReleaseId, string CityId, int Seq, string Indicator, string Level, string Change, string Assessment) : IReleaseRow;

// Section "dashboard" (city bullets) or a report section: executive, indicators, market, media, conclusion,
// recommendation, watchList, cityAverages.
public sealed record CityNarrativeRow(string ReleaseId, string CityId, string Section, int Seq, string Text) : IReleaseRow;

public sealed record ReportProductTableRow(
    string ReleaseId, string CityId, int Seq, string Product, string RentalDeals, string SalesDeals, string FisherIndex) : IReleaseRow;

public sealed record ReportAxisTableRow(string ReleaseId, string CityId, int Seq, string Axis, string Status) : IReleaseRow;

// Seq orders a city's media months and keys their items (legacy months may have an empty period).
public sealed record MediaMonitoringRow(
    string ReleaseId, string CityId, int Seq, string Period, bool Available, string OverallSentiment,
    int PositivePct, int NeutralPct, int NegativePct, double DiscussionSharePct) : IReleaseRow;

// Kind topic | positive | neutral | negative; MediaSeq = the owning media_monitoring row's Seq.
public sealed record MediaItemRow(string ReleaseId, string CityId, int MediaSeq, string Kind, int Seq, string Text) : IReleaseRow;

// Cities present in the supply/demand gap data, in order.
public sealed record GapCityRow(string ReleaseId, string CityId, int Seq) : IReleaseRow;

/// <summary>All rows of one release partition.</summary>
public sealed record ReleaseData(ReleaseRow Release)
{
    public IReadOnlyList<CityRow> Cities { get; init; } = [];
    public IReadOnlyList<CityStatusRow> CityStatus { get; init; } = [];
    public IReadOnlyList<ObservationRow> Observations { get; init; } = [];
    public IReadOnlyList<MarketMovementTableRow> MarketMovement { get; init; } = [];
    public IReadOnlyList<CityNarrativeRow> CityNarrative { get; init; } = [];
    public IReadOnlyList<ReportProductTableRow> ReportProducts { get; init; } = [];
    public IReadOnlyList<ReportAxisTableRow> ReportAxes { get; init; } = [];
    public IReadOnlyList<MediaMonitoringRow> MediaMonitoring { get; init; } = [];
    public IReadOnlyList<MediaItemRow> MediaItems { get; init; } = [];
    public IReadOnlyList<GapCityRow> GapCities { get; init; } = [];

    public IEnumerable<IReleaseRow> AllRows() => Cities.Cast<IReleaseRow>()
        .Concat(CityStatus).Concat(Observations).Concat(MarketMovement).Concat(CityNarrative)
        .Concat(ReportProducts).Concat(ReportAxes).Concat(MediaMonitoring).Concat(MediaItems).Concat(GapCities);
}
