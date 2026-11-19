using System.Collections.Concurrent;
using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Raw;
using AqarInsights.Api.Models.RentalControls.Snapshots;
using AqarInsights.Api.Models.RentalControls.Tables;
using AqarInsights.Api.Services.RentalControls.Projections;
using AqarInsights.Api.Services.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Store;
using AqarInsights.Api.Services.RentalControls.Tables;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AqarInsights.Api.Services.RentalControls.Snapshots;

/// <summary>The dataset a period is shown and reported from, with its label and the period rules to apply.</summary>
/// <param name="Scope">
/// The quarter, the selected month (month views) and the text month; <see cref="Projections.PeriodValueMask"/> applies
/// them to the documents and the narrative.
/// </param>
public sealed record RentalControlsPeriodData(
    string Id,
    string Label,
    bool IsMonthly,
    IRentalControlsAggregationService Aggregation,
    ICityReportContentStore Content,
    PeriodScope Scope)
{
    /// <summary>The quarter before this period's, as a quarterly view; null when it is not available (trend arrows).</summary>
    public RentalControlsPeriodData? PreviousQuarter { get; init; }

    /// <summary>The month before this period's month (a quarter's last month), as a monthly view; null when not available.</summary>
    public RentalControlsPeriodData? PreviousMonth { get; init; }
}

public interface IRentalControlsPeriodService
{
    /// <summary>Months and quarters a report can be generated for, newest first.</summary>
    ReportingPeriodsResponse GetPeriods();

    /// <summary>
    /// Resolves "yyyy-MM" (monthly), "yyyy-Qn" (quarterly) or null/empty (the live dataset's quarter).
    /// Returns null for an unknown or unavailable period.
    /// </summary>
    Task<RentalControlsPeriodData?> ResolveAsync(string? period, CancellationToken cancellationToken = default);
}

/// <summary>
/// Combines the live dataset with the closed quarters' releases (IRentalControlsRepository): the live dataset is its
/// own quarter's source; a closed quarter uses its release. A month is its quarter's data cut at the month, with the
/// month's own market-movement rows from monthly_movement. Release views are built on first use and cached until the
/// release changes.
/// </summary>
public class RentalControlsPeriodService : IRentalControlsPeriodService
{
    // Month is null for a quarterly release (it is not tied to one month). ReleaseId is null for the live dataset.
    private sealed record Candidate(string Key, YearMonth? Month, YearQuarter? Quarter, string? ReleaseId, DateTimeOffset UpdatedAt)
    {
        public bool IsLive => ReleaseId is null;
        public bool IsQuarterRelease => Month is null;
    }

    private sealed record Loaded(DateTimeOffset UpdatedAt, Task<(IRentalControlsStore Store, IRentalControlsAggregationService Agg, ICityReportContentStore Content)> View);

    private readonly IRentalControlsRepository _repository;
    private readonly IRentalControlsStore _liveStore;
    private readonly IRentalControlsAggregationService _liveAggregation;
    private readonly ICityReportContentStore _liveContent;
    private readonly IOptions<MarketAnalysisOptions>? _marketOptions;
    private readonly IOptions<KpiThresholdsOptions>? _kpiThresholds;
    private readonly IOptions<RentalPolicyOptions>? _rentalPolicy;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, Loaded> _cache = new(StringComparer.Ordinal);
    // Rental-controls imports run from the CLI and the API is restarted, so the table is read once per process.
    private readonly Lazy<Task<IReadOnlyList<MonthlyMovementRow>>> _monthlyMovement;

    public RentalControlsPeriodService(
        IRentalControlsRepository repository,
        IRentalControlsStore liveStore,
        IRentalControlsAggregationService liveAggregation,
        ICityReportContentStore liveContent,
        IOptions<MarketAnalysisOptions>? marketOptions = null,
        IOptions<KpiThresholdsOptions>? kpiThresholds = null,
        IOptions<RentalPolicyOptions>? rentalPolicy = null,
        ILogger<RentalControlsPeriodService>? logger = null)
    {
        _repository = repository;
        _liveStore = liveStore;
        _liveAggregation = liveAggregation;
        _liveContent = liveContent;
        _marketOptions = marketOptions;
        _kpiThresholds = kpiThresholds;
        _rentalPolicy = rentalPolicy;
        _logger = (ILogger?)logger ?? NullLogger.Instance;
        _monthlyMovement = new(() => _repository.LoadMonthlyMovementAsync());
    }

    public ReportingPeriodsResponse GetPeriods()
    {
        var candidates = Candidates();
        var monthly = AvailableMonths(candidates).OrderByDescending(m => m)
            .Select(m => new ReportingPeriodOption(m.Id, m.Label)).ToList();
        var quarterly = candidates
            .Where(c => c.Quarter is not null).Select(c => c.Quarter!.Value).Distinct().OrderByDescending(q => q)
            .Select(q => new ReportingPeriodOption(q.Id, q.Label)).ToList();
        var live = candidates.FirstOrDefault(c => c.IsLive);
        return new ReportingPeriodsResponse(live?.Quarter?.Id ?? "", monthly, quarterly);
    }

    public async Task<RentalControlsPeriodData?> ResolveAsync(string? period, CancellationToken cancellationToken = default)
    {
        var data = await ResolveCoreAsync(period, cancellationToken);
        if (data?.Scope.Quarter is not { } quarter) return data;
        var month = data.Scope.Month ?? quarter.LastMonth;
        return data with
        {
            PreviousQuarter = await ResolveCoreAsync(quarter.Previous().Id, cancellationToken),
            PreviousMonth = await ResolveCoreAsync(month.Previous().Id, cancellationToken),
        };
    }

    private async Task<RentalControlsPeriodData?> ResolveCoreAsync(string? period, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(period))
        {
            // The live quarter's view, exactly as ?period=<live quarter>.
            if (LiveQuarter() is { } liveQuarter && await ResolveCoreAsync(liveQuarter.Id, cancellationToken) is { } current)
                return current;
            return new RentalControlsPeriodData("", _liveAggregation.GetMarketAnalysis().LastQuarter, false,
                _liveAggregation, _liveContent, PeriodScope.None);
        }

        var candidates = Candidates();
        if (YearMonth.TryParse(period, out var month))
        {
            if (!AvailableMonths(candidates).Contains(month)) return null;
            // A month is its quarter's data (as the quarter shows it) cut at the month: monthly values, media and the
            // CPI rows are the month's own.
            if (QuarterSource(candidates, month.Quarter) is { } source)
                return await ViewAsync(source, month.Id, month.Label, isMonthly: true, month.Quarter, month, capAt: month);
            // A month after the newest quarter (its quarterly data has not arrived): its own dataset, as is.
            var own = candidates.First(c => c.Month == month);
            return await ViewAsync(own, month.Id, month.Label, isMonthly: true, month.Quarter, month);
        }

        if (YearQuarter.TryParse(period, out var q))
        {
            // Current quarter → live data (freshest, incl. narrative edits); closed quarter → its release. Monthly
            // series are cut at the quarter's last month, so a quarterly report never shows months after the quarter
            // (e.g. the Q2 release carried July–August media and CPI).
            var match = QuarterSource(candidates, q);
            return match is null ? null : await ViewAsync(match, q.Id, q.Label, isMonthly: false, q, month: null, capAt: q.LastMonth);
        }

        return null;
    }

    // The live data for its own quarter; a closed quarter's release otherwise.
    private static Candidate? QuarterSource(IReadOnlyList<Candidate> candidates, YearQuarter q)
        => candidates.FirstOrDefault(c => c.IsLive && c.Quarter == q)
            ?? candidates.FirstOrDefault(c => c.IsQuarterRelease && c.Quarter == q);

    // Every month of each quarter that has data, up to the newest dataset's month, plus any month with a dataset of its
    // own (e.g. a live month whose quarter has no quarterly data yet).
    private static IReadOnlyList<YearMonth> AvailableMonths(IReadOnlyList<Candidate> candidates)
    {
        var own = candidates.Where(c => c.Month is not null).Select(c => c.Month!.Value).ToList();
        if (own.Count == 0) return [];
        var newest = own.Max();
        return candidates.Where(c => c.Quarter is not null).Select(c => c.Quarter!.Value).Distinct()
            .SelectMany(q => new[] { q.FirstMonth, q.FirstMonth.Next(), q.LastMonth })
            .Where(m => m <= newest)
            .Concat(own).Distinct().ToList();
    }

    // The text month is the dataset's own month, uncapped: once a monthly update moves live past its quarter, the
    // written text describes that newer month, and the quarter's views mask it (PeriodValueMask.KeepsSentence).
    private async Task<RentalControlsPeriodData> ViewAsync(Candidate candidate, string id, string label, bool isMonthly,
        YearQuarter quarter, YearMonth? month, YearMonth? capAt = null)
    {
        IRentalControlsStore store;
        IRentalControlsAggregationService agg;
        ICityReportContentStore content;
        DateTimeOffset version;
        if (candidate.IsLive)
        {
            (store, agg, content, version) = (_liveStore, _liveAggregation, _liveContent, default);
        }
        else
        {
            var loaded = _cache.AddOrUpdate(candidate.Key,
                _ => new Loaded(candidate.UpdatedAt, LoadReleaseAsync(candidate.ReleaseId!)),
                (_, existing) => existing.UpdatedAt == candidate.UpdatedAt && !existing.View.IsFaulted
                    ? existing
                    : new Loaded(candidate.UpdatedAt, LoadReleaseAsync(candidate.ReleaseId!)));
            (store, agg, content) = await loaded.View;
            version = candidate.UpdatedAt;
        }

        var textMonth = SnapshotPeriod.DetectMonth(Raws(store));
        // A frozen release's text was aligned to its quarter (corrections), even when its stored series run past it;
        // live's text keeps its own month (a monthly update may move it past the current quarter).
        if (!candidate.IsLive && textMonth is { } t && t > quarter.LastMonth) textMonth = quarter.LastMonth;

        if (capAt is { } cap && ExtendsBeyond(store, cap))
        {
            var panel = MonthlyMovement.ForMonth(await _monthlyMovement.Value, cap);
            var capped = _cache.AddOrUpdate($"{candidate.Key}|≤{cap.Id}",
                _ => new Loaded(version, CapAsync(store, content, cap, panel)),
                (_, existing) => existing.UpdatedAt == version && !existing.View.IsFaulted
                    ? existing
                    : new Loaded(version, CapAsync(store, content, cap, panel)));
            (_, agg, _) = await capped.View;
        }

        return new RentalControlsPeriodData(id, label, isMonthly, agg, content, new PeriodScope(quarter, month, textMonth));
    }

    private static List<CityRawData> Raws(IRentalControlsStore store)
        => store.GetCities().Select(c => store.GetCityRaw(c.Id)).OfType<CityRawData>().ToList();

    private static bool ExtendsBeyond(IRentalControlsStore store, YearMonth cap) => SnapshotPeriod.ExtendsBeyond(
        store.GetCities().Select(c => store.GetCityRaw(c.Id)).OfType<CityRawData>(), store.GetIndicatorSeries(),
        store.GetSupplyDemandGap(), cap);

    private Task<(IRentalControlsStore, IRentalControlsAggregationService, ICityReportContentStore)> CapAsync(
        IRentalControlsStore store, ICityReportContentStore content, YearMonth cap,
        IReadOnlyDictionary<string, IReadOnlyList<RawMovementRow>>? panel)
    {
        var capped = new CappedRentalControlsStore(store, cap, panel);
        var agg = new RentalControlsAggregationService(capped, _marketOptions, _kpiThresholds, _rentalPolicy);
        agg.Recompute();
        _logger.LogInformation("Built rental-controls view capped at {Month}", cap.Id);
        return Task.FromResult<(IRentalControlsStore, IRentalControlsAggregationService, ICityReportContentStore)>((capped, agg, content));
    }

    private async Task<(IRentalControlsStore, IRentalControlsAggregationService, ICityReportContentStore)> LoadReleaseAsync(string releaseId)
    {
        var (rows, _) = await _repository.LoadCorrectedReleaseAsync(releaseId);
        if (rows is null) throw new InvalidOperationException($"Rental-controls release '{releaseId}' is no longer stored.");
        // The supply/demand gap is not period data (a single sheet, no history): every period shows the live workbook.
        var store = new TableRentalControlsStore(ReleaseMapper.FromRows(rows) with { SupplyDemandGap = _liveStore.GetSupplyDemandGap() });
        var agg = new RentalControlsAggregationService(store, _marketOptions, _kpiThresholds, _rentalPolicy);
        agg.Recompute();
        _logger.LogInformation("Loaded rental-controls release {Release}", releaseId);
        return (store, agg, store);
    }

    private IReadOnlyList<Candidate> Candidates()
    {
        var result = new List<Candidate>();
        var liveRaws = LiveCityRaws();
        if (SnapshotPeriod.DetectMonth(liveRaws) is { } liveMonth)
            result.Add(new Candidate("live", liveMonth, ReportingPeriod.LatestQuarter(liveRaws), null, default));

        foreach (var release in _repository.GetReleases())
        {
            // Only closed quarters' releases are period sources (monthly releases are no longer read).
            if (release.Kind != ReleaseKinds.Quarterly || !YearQuarter.TryParse(release.Id, out var quarter)) continue;
            result.Add(new Candidate(release.Id, null, quarter, release.Id, release.UpdatedAt));
        }
        return result;
    }

    private List<CityRawData> LiveCityRaws()
        => _liveStore.GetCities().Select(c => _liveStore.GetCityRaw(c.Id)).OfType<CityRawData>().ToList();

    private YearQuarter? LiveQuarter() => ReportingPeriod.LatestQuarter(LiveCityRaws());
}
