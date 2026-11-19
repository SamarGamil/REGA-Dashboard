using AqarInsights.Api.Models;
using AqarInsights.Api.Models.ForeignOwnership;
using AqarInsights.Api.Services;
using AqarInsights.Api.Services.ForeignOwnership;

namespace AqarInsights.Api.Tests.Assistant;

/// <summary>Test double for the foreign-ownership aggregation service. Empty by default;
/// set <see cref="Summary"/> to make the foreign product report data.</summary>
internal sealed class FakeForeignAgg : IAggregationService
{
    public SummaryResponse Summary = new();
    /// <summary>When set, answers GetSummary per filter (e.g. owner-type splits); falls back to Summary.</summary>
    public Func<DashboardFilterParams?, SummaryResponse>? SummaryByFilter;
    public NationalitiesResponse Nationalities = new();
    public RegionsResponse Regions = new();
    public PropertiesResponse Properties = new();
    public AreasResponse Areas = new();
    public FinancialResponse Financial = new();
    public StrategicResponse Strategic = new();

    public void Recompute() { }
    public SummaryResponse GetSummary(DashboardFilterParams? filters = null) => SummaryByFilter?.Invoke(filters) ?? Summary;
    public RegionsResponse GetRegions(DashboardFilterParams? filters = null) => Regions;
    public NationalitiesResponse GetNationalities(DashboardFilterParams? filters = null) => Nationalities;
    public PropertiesResponse GetProperties(DashboardFilterParams? filters = null) => Properties;
    public AreasResponse GetAreas(DashboardFilterParams? filters = null) => Areas;
    public FinancialResponse GetFinancial(DashboardFilterParams? filters = null) => Financial;
    public StrategicResponse GetStrategic(DashboardFilterParams? filters = null) => Strategic;
    public string GetReportingPeriod() => "";
    public string GetReportingMonth() => "";
}
