using AqarInsights.Api.Models;
using AqarInsights.Api.Models.ForeignOwnership;
using AqarInsights.Api.Services.ForeignOwnership.Projections;

namespace AqarInsights.Api.Services.ForeignOwnership;

public static class Periods
{
    public const string Monthly = "monthly";
    public const string Quarterly = "quarterly";
    public const string Yearly = "yearly";
}

// Orchestrates the foreign-ownership projections: recomputes an immutable snapshot at load time
// and serves either the cached snapshot (no filter) or a freshly-projected filtered view.
public class AggregationService : IAggregationService
{
    private readonly IDataLoaderService _dataLoader;
    private readonly IFilterOptionsService _filterOptionsService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AggregationService> _logger;

    private volatile AggregationSnapshot _snapshot = new(new(), new(), new(), new(), new(), new(), new());

    public AggregationService(
        IDataLoaderService dataLoader,
        ILogger<AggregationService> logger,
        IFilterOptionsService filterOptionsService,
        TimeProvider timeProvider)
    {
        _dataLoader = dataLoader;
        _logger = logger;
        _filterOptionsService = filterOptionsService;
        _timeProvider = timeProvider;
    }

    public void Recompute()
    {
        var records = _dataLoader.GetRecords();
        if (records.Count == 0)
        {
            _logger.LogWarning("No records to aggregate; publishing empty snapshot");
            _snapshot = new AggregationSnapshot(new(), new(), new(), new(), new(), new(), new());
            return;
        }

        _logger.LogInformation("Recomputing aggregations for {Count} records", records.Count);

        var anchor = PeriodMath.LatestDataDate(records, Now);
        // Single reporting window (last 6 periods) for every widget so all figures reconcile.
        var scoped = PeriodMath.FilterByLast6Periods(records, Periods.Monthly, anchor);
        _snapshot = new AggregationSnapshot(
            Summary: SummaryProjection.Build(scoped, Periods.Monthly, anchor),
            Regions: RegionsProjection.Build(scoped, Periods.Monthly, anchor),
            Nationalities: NationalitiesProjection.Build(scoped, Periods.Monthly, anchor),
            Properties: PropertiesProjection.Build(scoped, Periods.Monthly, anchor),
            Areas: AreasProjection.Build(scoped, Periods.Monthly, anchor),
            Financial: FinancialProjection.Build(scoped, Periods.Monthly, anchor),
            Strategic: StrategicProjection.Build(scoped, Periods.Monthly, anchor));
    }

    public SummaryResponse GetSummary(DashboardFilterParams? filters = null)
        => GetOrCompute(s => s.Summary, (r, p) => SummaryProjection.Build(r, p, Anchor), filters);

    // Current reporting period (quarter) derived from the latest transaction date.
    public string GetReportingPeriod()
    {
        var latest = LatestReportingDate();
        return latest == default ? "" : QuarterLabel.FromDate(latest);
    }

    // Latest data month label (e.g. "يونيو 2026"), shown as the "last updated" line.
    public string GetReportingMonth()
    {
        var latest = LatestReportingDate();
        return latest == default ? "" : QuarterLabel.MonthLabel(latest);
    }

    private DateTime LatestReportingDate() => _dataLoader.GetRecords()
        .Where(r => r.Date.HasValue).Select(r => r.Date!.Value).DefaultIfEmpty().Max();

    public RegionsResponse GetRegions(DashboardFilterParams? filters = null)
        => GetOrCompute(s => s.Regions, (r, p) => RegionsProjection.Build(r, p, Anchor), filters);

    public NationalitiesResponse GetNationalities(DashboardFilterParams? filters = null)
        => GetOrCompute(s => s.Nationalities, (r, p) => NationalitiesProjection.Build(r, p, Anchor), filters);

    public PropertiesResponse GetProperties(DashboardFilterParams? filters = null)
        => GetOrCompute(s => s.Properties, (r, p) => PropertiesProjection.Build(r, p, Anchor), filters);

    public AreasResponse GetAreas(DashboardFilterParams? filters = null)
        => GetOrCompute(s => s.Areas, (r, p) => AreasProjection.Build(r, p, Anchor), filters);

    public FinancialResponse GetFinancial(DashboardFilterParams? filters = null)
    {
        var period = filters?.Period ?? Periods.Monthly;
        if (filters == null || (!filters.HasFilters() && period == Periods.Monthly))
            return _snapshot.Financial;
        var filtered = ApplyFilters(_dataLoader.GetRecords(), filters);
        return FinancialProjection.Build(PeriodMath.FilterByLast6Periods(filtered, period, Anchor), period, Anchor);
    }

    public StrategicResponse GetStrategic(DashboardFilterParams? filters = null)
    {
        var period = filters?.Period ?? Periods.Monthly;
        if (filters == null || (!filters.HasFilters() && period == Periods.Monthly))
            return _snapshot.Strategic;
        var filtered = ApplyFilters(_dataLoader.GetRecords(), filters);
        return StrategicProjection.Build(PeriodMath.FilterByLast6Periods(filtered, period, Anchor), period, Anchor);
    }

    private DateTime Now => _timeProvider.GetLocalNow().DateTime;

    // Fixed reporting anchor (latest data date across ALL records) so region/city/nationality
    // filters subset within the same period window instead of moving it.
    private DateTime Anchor => PeriodMath.LatestDataDate(_dataLoader.GetRecords(), Now);

    private List<TransactionRecord> ApplyFilters(IReadOnlyList<TransactionRecord> records, DashboardFilterParams? filters)
    {
        if (filters == null || !filters.HasFilters()) return records.ToList();

        var result = records.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filters.Region))
        {
            var options = _filterOptionsService.GetOptions();
            if (options != null)
            {
                var optionsCities = options.Geography
                    .Where(c => c.Name.Contains(filters.Region))
                    .SelectMany(c => c.Children)
                    .Select(c => c.Name)
                    .ToHashSet();
                result = result.Where(r => optionsCities.Contains(r.City));
            }
        }

        if (!string.IsNullOrWhiteSpace(filters.City))
            result = result.Where(r => r.City == filters.City);

        if (!string.IsNullOrWhiteSpace(filters.District))
            result = result.Where(r => r.District == filters.District);

        var nationalitiesList = filters.GetNationalitiesList();
        if (nationalitiesList.Count > 0)
        {
            var nationalitiesSet = nationalitiesList.ToHashSet();
            result = result.Where(r => nationalitiesSet.Contains(r.Nationality));
        }

        if (!string.IsNullOrWhiteSpace(filters.TransactionType))
            result = result.Where(r => r.TransactionType == filters.TransactionType);

        if (!string.IsNullOrWhiteSpace(filters.PropertyType))
            result = result.Where(r => r.PropertyType == filters.PropertyType);

        if (!string.IsNullOrWhiteSpace(filters.OwnerType))
            result = result.Where(r => r.OwnerType == filters.OwnerType);

        return result.ToList();
    }

    private T GetOrCompute<T>(
        Func<AggregationSnapshot, T> cacheSelector,
        Func<IReadOnlyList<TransactionRecord>, string, T> computeFunc,
        DashboardFilterParams? filters)
    {
        var period = filters?.Period ?? Periods.Monthly;
        if (filters == null || (!filters.HasFilters() && period == Periods.Monthly))
        {
            return cacheSelector(_snapshot);
        }

        var filtered = ApplyFilters(_dataLoader.GetRecords(), filters);
        // Scope to the single last-6 window so filtered figures match their charts.
        return computeFunc(PeriodMath.FilterByLast6Periods(filtered, period, Anchor), period);
    }
}

internal sealed record AggregationSnapshot(
    SummaryResponse Summary,
    RegionsResponse Regions,
    NationalitiesResponse Nationalities,
    PropertiesResponse Properties,
    AreasResponse Areas,
    FinancialResponse Financial,
    StrategicResponse Strategic);
