namespace AqarInsights.Api.Models.RentalControls.Reports;

public enum ReportFormat { Pdf, Word }

public class NationalReportOptions
{
    public double BurdenReferenceThreshold { get; set; } = 30;
    public double YieldReferenceThreshold { get; set; } = 6;
}

public class NationalReportModel
{
    public string HeaderTitle { get; set; } = "";
    public string AssessmentLine { get; set; } = "";
    public string Title { get; set; } = "";
    public string PeriodLabel { get; set; } = "";
    public string PeriodLine { get; set; } = "";      // e.g. "فترة التقرير: الربع الثاني 2026 (تقرير ربع سنوي)"
    public string GeneratedOnLabel { get; set; } = "";
    public List<ReportKpiCard> KpiCards { get; set; } = new();
    public List<string> SummaryLines { get; set; } = new();
    public string SortNote { get; set; } = "";
    // Period word per indicator column: "housing-burden", "investment-return", "actual-rent".
    public Dictionary<string, string> IndicatorPeriods { get; set; } = new();
    public List<ReportCityRow> Rows { get; set; } = new();
    public List<InterventionGroup> InterventionGroups { get; set; } = new();
    public List<ImpactCityView> Impact { get; set; } = new();
    public string MonitoringTitle { get; set; } = "";
    public List<string> MonitoringBullets { get; set; } = new();
}

public class InterventionGroup
{
    public string Label { get; set; } = "";
    public string ColorHex { get; set; } = "";
    public List<string> Cities { get; set; } = new();
}

public class ReportKpiCard
{
    public string Label { get; set; } = "";
    public string Frequency { get; set; } = "";
    public string Period { get; set; } = "";
    public string ValueText { get; set; } = "";
    public string CityName { get; set; } = "";
    public bool IsUp { get; set; }
    public string ColorHex { get; set; } = "";
}

public class ReportCityRow
{
    public int Rank { get; set; }
    public string CityName { get; set; } = "";
    public string Region { get; set; } = "";
    public string TrackLabel { get; set; } = "";
    public string TrackColorHex { get; set; } = "";
    public string BurdenText { get; set; } = "";
    public string BurdenColorHex { get; set; } = "";
    public string RentalIncreaseText { get; set; } = "";
    public string RentalIncreaseColorHex { get; set; } = "";
    public string RentToIncomeText { get; set; } = "";
    public string RentToIncomeColorHex { get; set; } = "";
    // Change from the previous quarter / month: "↑", "↓", "" (unchanged) or "—" (no previous value).
    public string BurdenTrend { get; set; } = "";
    public string ActualRentTrend { get; set; } = "";
    public string YieldText { get; set; } = "";
    public string YieldColorHex { get; set; } = "";
    public string ActualRentText { get; set; } = "";
    public string RentColorHex { get; set; } = "";
}

/// <summary>A city whose controls are activated, with the effect points the platform's impact panel shows for it.</summary>
public class ImpactCityView
{
    public string CityName { get; set; } = "";
    public List<string> Points { get; set; } = new();
}

/// <summary>What the national report takes from other periods and the city pages, beyond the period's own market data.</summary>
/// <param name="IndicatorPeriods">Period word per indicator column ("housing-burden", "investment-return", "actual-rent", ...).</param>
/// <param name="PreviousQuarter">The previous quarter's market data (burden trend); null when not available.</param>
/// <param name="PreviousMonth">The previous month's market data (actual-rent trend); null when not available.</param>
/// <param name="CityFacts">Each city's indicator facts exactly as its city report builds them (by city name): the table
/// colours each cell by the same platform status, so both reports show one colour for one figure.</param>
/// <param name="ImpactCities">The rent-increase and actual-rent series of each city whose controls are activated.</param>
public sealed record NationalReportContext(
    IReadOnlyDictionary<string, string> IndicatorPeriods,
    MarketAnalysisData? PreviousQuarter = null,
    MarketAnalysisData? PreviousMonth = null,
    IReadOnlyDictionary<string, CityReportFacts>? CityFacts = null,
    IReadOnlyList<ImpactCitySeries>? ImpactCities = null);

/// <param name="Cagr">The city's compound rental growth (%) for the report's period; null when unavailable.</param>
/// <param name="CagrPeriod">The quarter label that figure belongs to.</param>
public sealed record ImpactCitySeries(
    string City, IReadOnlyList<RentalIndicatorPoint> IncreaseQuarterly, IReadOnlyList<MonthlyPoint> ActualRentMonthly,
    double? Cagr = null, string? CagrPeriod = null);
