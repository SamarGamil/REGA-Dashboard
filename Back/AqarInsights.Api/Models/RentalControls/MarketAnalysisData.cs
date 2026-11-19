namespace AqarInsights.Api.Models.RentalControls;

public class MarketAnalysisData
{
    public string LastQuarter { get; set; } = "";
    public List<CityMarketMetric> Cities { get; set; } = new();
    public List<MarketIndicatorDef> Indicators { get; set; } = new();
    public List<MarketRecommendation> Recommendations { get; set; } = new();
}

public class CityMarketMetric
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string OperationalStatus { get; set; } = "";
    public string Classification { get; set; } = "";
    public double? Cagr { get; set; }
    public double? RentalIncrease { get; set; }
    public double? HousingBurden { get; set; }
    public double? InvestmentReturn { get; set; }
    /// <summary>The unrounded yield (latest point of the city's ROI series); breaks ties between rounded yields.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public double? InvestmentReturnExact { get; set; }
    public double? ActualRentalGrowth { get; set; }
    public double? RentToIncome { get; set; }
    public double? VacancyRate { get; set; }
    public double? RentalTransactionCount { get; set; }
    public double? AvgRentalTransactionValue { get; set; }
    public double? SalesTransactionCount { get; set; }
    public double? AvgSalesTransactionValue { get; set; }
    public double? ActualRentCpi { get; set; }
    public double? GeneralInflationCpi { get; set; }
    public double? ApartmentRentIndex { get; set; }
    public double? FloorRentIndex { get; set; }
    public double? VillaRentIndex { get; set; }

    // The figures as displayed on the platform and in the city report ("5.3%", "+4.0%"); the national report prints
    // these rather than re-formatting the numbers (which would drop a ".0").
    [System.Text.Json.Serialization.JsonIgnore] public string? InvestmentReturnText { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public string? ActualRentalGrowthText { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public string? CagrText { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public string? RentalIncreaseText { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public string? RentToIncomeText { get; set; }

    public CityMarketMetric Clone() => (CityMarketMetric)MemberwiseClone();
}

public class MarketIndicatorDef
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Unit { get; set; } = "";
    public bool IsPercentage { get; set; }
    public string HigherIs { get; set; } = "neutral";
    public double Threshold { get; set; }
    public string ThresholdLabel { get; set; } = "";
}

public class MarketRecommendation
{
    public int Priority { get; set; }
    public string Title { get; set; } = "";
    public string AffectedCities { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Cta { get; set; } = "";
    public string Action { get; set; } = "";
}
