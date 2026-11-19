using AqarInsights.Api.Models;
using AqarInsights.Api.Models.ForeignOwnership;

namespace AqarInsights.Api.Services.ForeignOwnership;

public interface IAggregationService
{
    void Recompute();
    SummaryResponse GetSummary(DashboardFilterParams? filters = null);
    RegionsResponse GetRegions(DashboardFilterParams? filters = null);
    NationalitiesResponse GetNationalities(DashboardFilterParams? filters = null);
    PropertiesResponse GetProperties(DashboardFilterParams? filters = null);
    AreasResponse GetAreas(DashboardFilterParams? filters = null);
    FinancialResponse GetFinancial(DashboardFilterParams? filters = null);
    StrategicResponse GetStrategic(DashboardFilterParams? filters = null);
    string GetReportingPeriod();
    string GetReportingMonth();
}
