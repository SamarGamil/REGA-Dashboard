using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Projections;
using AqarInsights.Api.Services.RentalControls.Snapshots;

namespace AqarInsights.Api.Services.RentalControls.Reports;

public interface ICityReportService
{
    ReportFile? GenerateCity(RentalControlsPeriodData period, string cityId, ReportFormat format);
}

public class CityReportService : ICityReportService
{
    private readonly TimeProvider _clock;

    public CityReportService(TimeProvider clock)
    {
        _clock = clock;
    }

    public ReportFile? GenerateCity(RentalControlsPeriodData period, string cityId, ReportFormat format)
    {
        var agg = period.Aggregation;
        var sourceCity = agg.GetCity(cityId);
        var content = period.Content.Get(cityId);
        var overviewCity = agg.GetOverview().Cities.FirstOrDefault(c => c.Id == cityId);
        if (sourceCity is null || content is null || overviewCity is null) return null;

        // The report shows the period exactly as the portal does: monthly values of another month are dashes and the
        // narrative follows the same text rule.
        var city = PeriodValueMask.Apply(sourceCity, period.Scope);
        var panels = PeriodValueMask.Apply(agg.GetIndicators(), period.Scope).CurrentPeriodIndicators;
        var today = DateOnly.FromDateTime(_clock.GetLocalNow().DateTime);
        var facts = CityReportFactsBuilder.Build(overviewCity, city, panels);
        var model = CityReportModelBuilder.Build(city, facts,
            CityReportModelBuilder.ForPeriod(content, period.Scope, MonthlyCpiStatement.ForView(city, period.Scope)),
            today, period.Label, period.IsMonthly);
        var slug = ReportFileSlug.For(period);
        var citySlug = city.CityName.Replace(' ', '_');

        return format switch
        {
            ReportFormat.Word => new ReportFile(
                CityReportWordRenderer.Render(model),
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                $"تقرير_{citySlug}_REGA_{slug}.docx"),
            ReportFormat.Pdf => new ReportFile(
                CityReportPdfRenderer.Render(model),
                "application/pdf",
                $"تقرير_{citySlug}_REGA_{slug}.pdf"),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
    }
}
