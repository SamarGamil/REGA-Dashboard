namespace AqarInsights.Api.Models.RentalControls;

public class CityAnalysisData
{
    public string CityName { get; set; } = "";
    public string OperationalStatus { get; set; } = "";
    public string Classification { get; set; } = "";
    public string ClassificationColor { get; set; } = "";
    public string Period { get; set; } = "";
    public string LastUpdate { get; set; } = "";
    public ExecutiveSummary ExecutiveSummary { get; set; } = new();
    public MediaMonitoring MediaMonitoring { get; set; } = new();
    public List<KpiItem> Kpis { get; set; } = new();
    public List<RentalIndicatorChart> RentalIndicatorCharts { get; set; } = new();
    // Full (uncapped) quarterly الزيادة الإيجارية history, for the controls-impact chart.
    public List<RentalIndicatorPoint> RentalIncreaseSeries { get; set; } = new();
    // Monthly الزيادة الإيجارية and الإيجار الفعلي (CPI) history (Period "yyyy-MM"), as far as the data goes; the city report plots both.
    [System.Text.Json.Serialization.JsonIgnore] public List<MonthlyPoint> RentalIncreaseMonthly { get; set; } = new();
    [System.Text.Json.Serialization.JsonIgnore] public List<MonthlyPoint> ActualRentMonthly { get; set; } = new();
    public List<MarketMovementRow> MarketMovementTable { get; set; } = new();
    public List<MarketAnalysisTab> MarketAnalysisTabs { get; set; } = new();
    public List<ExecRecommendation> Recommendations { get; set; } = new();
    public List<MonitorItem> WhatToMonitor { get; set; } = new();
    public List<CityJourneyStage> CityJourney { get; set; } = new();
}

public class ExecutiveSummary { public List<string> Bullets { get; set; } = new(); public StatusBadge StatusBadge { get; set; } = new(); }
public class StatusBadge { public string Label { get; set; } = ""; public string Color { get; set; } = ""; public string BgColor { get; set; } = ""; }
public class MediaMonitoring { public bool Available { get; set; } public string Period { get; set; } = ""; public string MonthLabel { get; set; } = ""; public string OverallSentiment { get; set; } = ""; public int PositivePct { get; set; } public int NeutralPct { get; set; } public int NegativePct { get; set; } public List<string> TopHeadlines { get; set; } = new(); public List<string> PositiveImpressions { get; set; } = new(); public List<string> NeutralImpressions { get; set; } = new(); public List<string> NegativeImpressions { get; set; } = new(); public List<MediaSentimentPoint> History { get; set; } = new(); }
public class MediaSentimentPoint { public string Period { get; set; } = ""; public string MonthLabel { get; set; } = ""; public string OverallSentiment { get; set; } = ""; public int PositivePct { get; set; } public int NeutralPct { get; set; } public int NegativePct { get; set; } public double DiscussionSharePct { get; set; } public List<string> TopHeadlines { get; set; } = new(); public List<string> PositiveImpressions { get; set; } = new(); public List<string> NeutralImpressions { get; set; } = new(); public List<string> NegativeImpressions { get; set; } = new(); }
public class KpiItem { public string Label { get; set; } = ""; public string Value { get; set; } = ""; public string QuarterlyChange { get; set; } = ""; public string Status { get; set; } = ""; public string Trend { get; set; } = ""; public string Frequency { get; set; } = ""; public string PeriodLabel { get; set; } = ""; public string PeriodId { get; set; } = ""; }
public class RentalIndicatorChart { public string Label { get; set; } = ""; public string Value { get; set; } = ""; public string Status { get; set; } = ""; public string Trend { get; set; } = ""; public List<RentalIndicatorPoint> Points { get; set; } = new(); }
public class RentalIndicatorPoint { public string Period { get; set; } = ""; public double Value { get; set; } public bool IsLast { get; set; } }
public class MonthlyPoint { public string Period { get; set; } = ""; public double Value { get; set; } }
public class MarketMovementRow { public string Indicator { get; set; } = ""; public string Level { get; set; } = ""; public string Change { get; set; } = ""; public string Assessment { get; set; } = ""; }
public class MarketAnalysisTab
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Insight { get; set; } = "";
    public string ChartType { get; set; } = "line";  // "bar" | "line"
    public bool Stacked { get; set; }
    public List<TabSeries> Series { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public TabThreshold? Threshold { get; set; }
    public List<string> TableColumns { get; set; } = new();
    public List<List<string>> TableRows { get; set; } = new();
}
public class TabSeries { public string Name { get; set; } = ""; public List<double?> Data { get; set; } = new(); } // null = no value in the workbook (a gap, not 0)
public class TabThreshold { public double Value { get; set; } public string Label { get; set; } = ""; }
public class ExecRecommendation { public int Priority { get; set; } public string PriorityLabel { get; set; } = ""; public string Recommendation { get; set; } = ""; public string Description { get; set; } = ""; }
public class MonitorItem { public string Item { get; set; } = ""; public string CurrentStatus { get; set; } = ""; public string Trend { get; set; } = ""; public string Priority { get; set; } = ""; }
public class CityJourneyStage { public string Label { get; set; } = ""; public bool IsCurrent { get; set; } public bool IsCompleted { get; set; } }

public class CityListItem { public string CityId { get; set; } = ""; public string CityName { get; set; } = ""; public string OperationalStatus { get; set; } = ""; public string Classification { get; set; } = ""; public string ClassificationColor { get; set; } = ""; }
