namespace AqarInsights.Api.Models.RentalControls;

// Bound from appsettings "MarketAnalysis". Overrides the built-in indicator thresholds.
public class MarketAnalysisOptions
{
    public Dictionary<string, ThresholdConfig> Thresholds { get; set; } = new();
}

public class ThresholdConfig
{
    public double Value { get; set; }
    public string? Label { get; set; }
}
