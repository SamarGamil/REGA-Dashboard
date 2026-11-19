using Microsoft.Extensions.Options;
using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Projections;
using AqarInsights.Api.Services.RentalControls.Snapshots;

namespace AqarInsights.Api.Services.RentalControls.Reports;

public class NationalReportService : INationalReportService
{
    private readonly NationalReportOptions _options;
    private readonly TimeProvider _clock;

    public NationalReportService(IOptions<NationalReportOptions> options, TimeProvider clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public ReportFile GenerateNational(RentalControlsPeriodData period, ReportFormat format)
    {
        var data = period.Aggregation.GetMarketAnalysis();
        var today = DateOnly.FromDateTime(_clock.GetLocalNow().DateTime);
        var model = NationalReportModelBuilder.Build(data, today, _options, period.Label, period.IsMonthly, ContextFor(period));
        var slug = ReportFileSlug.For(period);

        return format switch
        {
            ReportFormat.Word => new ReportFile(
                NationalReportWordRenderer.Render(model),
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                $"التقرير_الوطني_REGA_{slug}.docx"),
            ReportFormat.Pdf => new ReportFile(
                NationalReportPdfRenderer.Render(model),
                "application/pdf",
                $"التقرير_الوطني_REGA_{slug}.pdf"),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
    }

    /// <summary>What the period's national report takes beyond its market data (previous periods, the cities' facts).</summary>
    public static NationalReportContext ContextFor(RentalControlsPeriodData period)
    {
        var agg = period.Aggregation;
        var market = agg.GetMarketAnalysis();
        var overview = agg.GetOverview().Cities;
        var cityDocs = overview
            .Select(c => (City: c, Doc: agg.GetCity(c.Id))).Where(p => p.Doc is not null)
            .Select(p => (p.City, Doc: PeriodValueMask.Apply(p.Doc!, period.Scope))).ToList();
        var cities = cityDocs.Select(p => p.Doc).ToList();
        var impact = cityDocs.Where(p => p.City.OperationalStatus == "الضوابط مفعلة" && p.City.ImpactData.Available)
            .Select(p => new ImpactCitySeries(p.City.Name, p.Doc.RentalIncreaseSeries, p.Doc.ActualRentMonthly,
                market.Cities.FirstOrDefault(c => c.Name == p.City.Name)?.Cagr, market.LastQuarter)).ToList();
        // The facts each city report is built from (CityReportService: the same masked city page and panels).
        var panels = PeriodValueMask.Apply(agg.GetIndicators(), period.Scope).CurrentPeriodIndicators;
        var facts = cityDocs.ToDictionary(p => p.City.Name, p => CityReportFactsBuilder.Build(p.City, p.Doc, panels), StringComparer.Ordinal);
        return new NationalReportContext(
            IndicatorPeriods(period, cities),
            period.PreviousQuarter?.Aggregation.GetMarketAnalysis(),
            period.PreviousMonth?.Aggregation.GetMarketAnalysis(),
            facts,
            impact);
    }

    // The period each indicator belongs to, exactly as the platform shows it: the quarter for the quarterly panels,
    // the month of the latest rent CPI figure for the actual rent.
    private static Dictionary<string, string> IndicatorPeriods(RentalControlsPeriodData period, List<CityAnalysisData> cities)
    {
        var panels = PeriodValueMask.Apply(period.Aggregation.GetIndicators(), period.Scope).CurrentPeriodIndicators;
        string Panel(string id) => ReportPeriodText.Word(panels.FirstOrDefault(p => p.Id == id)?.PeriodLabel);
        var rentMonth = cities.Select(CityReportFactsBuilder.RentCpiMonth).FirstOrDefault(m => m.Length > 0) ?? "";
        return new()
        {
            ["rental-increase"] = Panel("rental-increase"),
            ["rent-to-income"] = Panel("rent-to-income"),
            ["housing-burden"] = Panel("housing-burden"),
            ["investment-return"] = Panel("investment-return"),
            ["actual-rent"] = ReportPeriodText.Word(rentMonth),
        };
    }
}

internal static class ReportFileSlug
{
    public static string For(RentalControlsPeriodData period)
        => string.IsNullOrWhiteSpace(period.Id) ? "current" : period.Id;
}
