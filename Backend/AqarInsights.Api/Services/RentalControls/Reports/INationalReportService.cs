using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Snapshots;

namespace AqarInsights.Api.Services.RentalControls.Reports;

public interface INationalReportService
{
    ReportFile GenerateNational(RentalControlsPeriodData period, ReportFormat format);
}
