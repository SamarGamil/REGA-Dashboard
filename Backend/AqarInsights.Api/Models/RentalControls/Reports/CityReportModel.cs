namespace AqarInsights.Api.Models.RentalControls.Reports;

// Shape of the authored narrative Data/rental-controls-source/city-reports/{cityId}.json (imported into the release's
// city_narrative / report_products / report_axes tables) —
// the narrative only; numbers, statuses and recommendation come from CityReportFacts.
public class CityReportContent
{
    public string CityId { get; set; } = "";
    public List<string> ExecutiveBullets { get; set; } = new();
    public List<string> IndicatorsBullets { get; set; } = new();
    public List<string> MarketBullets { get; set; } = new();
    public List<CityReportProductRow> ProductTable { get; set; } = new();
    public string CityAveragesLine { get; set; } = "";
    public List<string> MediaBullets { get; set; } = new();
    public List<string> ConclusionBullets { get; set; } = new();
    public List<CityReportAxis> AssessmentAxes { get; set; } = new();
    public List<string> RecommendationBullets { get; set; } = new();
    public List<string> WatchList { get; set; } = new();
}

public class CityReportProductRow
{
    public string Product { get; set; } = "";
    public string RentalDeals { get; set; } = "";
    public string SalesDeals { get; set; } = "";
    public string FisherIndex { get; set; } = "";
}

public class CityReportAxis
{
    public string Axis { get; set; } = "";
    public string Status { get; set; } = "";
    public string ColorHex { get; set; } = "";
    public string BgHex { get; set; } = "";
}

public class CityReportSupplyStatView
{
    public string Label { get; set; } = "";
    public string Period { get; set; } = "";
    public string Value { get; set; } = "";
    public string Note { get; set; } = "";
    public string Reference { get; set; } = "";
    public string ColorHex { get; set; } = "";
    public string Frequency { get; set; } = "";
}

// Indicator row enriched with status colors for rendering.
public class CityReportIndicatorRowView
{
    public string Indicator { get; set; } = "";
    public string Frequency { get; set; } = "";
    public string Period { get; set; } = "";
    public string Value { get; set; } = "";
    public string Reference { get; set; } = "";
    public string Status { get; set; } = "";
    public string StatusColorHex { get; set; } = "";
    public string StatusBgHex { get; set; } = "";
}

// Render-ready model consumed by the Word/PDF renderers.
public class CityReportModel
{
    public string HeaderTitle { get; set; } = "";
    public string HeaderSubtitle { get; set; } = "";
    public string CityName { get; set; } = "";
    public string PeriodLabel { get; set; } = "";
    public string PeriodLine { get; set; } = "";      // e.g. "فترة التقرير: الربع الثاني 2026 (تقرير ربع سنوي)"
    public string GeneratedOnLabel { get; set; } = "";
    public string MarketState { get; set; } = "";
    public string MarketStateColorHex { get; set; } = "";
    public List<CityReportKpiCardView> KpiCards { get; set; } = new();
    public List<string> ExecutiveBullets { get; set; } = new();
    public List<string> IndicatorsBullets { get; set; } = new();
    public List<CityReportIndicatorRowView> IndicatorRows { get; set; } = new();
    public List<string> MarketBullets { get; set; } = new();
    public List<CityReportProductRow> ProductRows { get; set; } = new();
    public string CityAveragesLine { get; set; } = "";
    public List<CityReportSupplyStatView> SupplyInvestment { get; set; } = new();
    public bool MediaAvailable { get; set; }
    public string MediaSentiment { get; set; } = "";
    public string MediaSentimentColorHex { get; set; } = "#1CA59A";
    public string MediaBreakdown { get; set; } = "";
    public string MediaMonthPrefix { get; set; } = "";
    public int MediaPositivePct { get; set; }
    public int MediaNeutralPct { get; set; }
    public int MediaNegativePct { get; set; }
    public List<string> MediaBullets { get; set; } = new();
    public List<string> ConclusionBullets { get; set; } = new();
    public List<CityReportAxis> Axes { get; set; } = new();
    public string OverallAssessment { get; set; } = "";
    public List<string> RecommendationBullets { get; set; } = new();
    public string WatchTitle { get; set; } = "";
    public List<string> WatchList { get; set; } = new();
    // The two monthly line charts that end page two (الزيادة الإيجارية, then الإيجار الفعلي للمسكن CPI).
    public List<CityReportChartView> Charts { get; set; } = new();
}

public class CityReportChartView
{
    public string Title { get; set; } = "";
    public string Color { get; set; } = "";
    public List<string> Categories { get; set; } = new();
    public List<double?> Values { get; set; } = new();
    // The month the controls were activated (marked on the chart), -1 when it is outside the plotted months.
    public int MarkerIndex { get; set; } = -1;
    public string MarkerLabel { get; set; } = "";
}

public class CityReportKpiCardView
{
    public string Label { get; set; } = "";
    public string Frequency { get; set; } = "";
    public string Period { get; set; } = "";
    public string ValueText { get; set; } = "";
    public string Note { get; set; } = "";
    public string Reference { get; set; } = "";
    public string ColorHex { get; set; } = "";
}
