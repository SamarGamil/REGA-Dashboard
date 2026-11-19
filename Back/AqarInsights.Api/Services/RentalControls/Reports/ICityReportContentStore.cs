using AqarInsights.Api.Models.RentalControls.Reports;

namespace AqarInsights.Api.Services.RentalControls.Reports;

/// <summary>Per-city report narrative of one release (null when the city has none).</summary>
public interface ICityReportContentStore
{
    CityReportContent? Get(string cityId);
}
