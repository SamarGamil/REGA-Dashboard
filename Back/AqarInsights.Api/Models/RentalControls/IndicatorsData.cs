namespace AqarInsights.Api.Models.RentalControls;

public class IndicatorsData
{
    public List<IndicatorPanel> CurrentPeriodIndicators { get; set; } = new();
    public List<HistoricalIndicator> HistoricalIndicators { get; set; } = new();
    public List<SupplyDemandGapCity> SupplyDemandGap { get; set; } = new();
    public Dictionary<string, string> CurrentToHistoricalMap { get; set; } = new();
}

public class IndicatorPanel
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Definition { get; set; } = "";
    public string ThresholdRule { get; set; } = "";
    public string ThresholdSummary { get; set; } = "";
    public string Frequency { get; set; } = "";   // "monthly" | "quarterly" | ""
    public string PeriodLabel { get; set; } = ""; // latest period in the indicator's data, e.g. "يونيو 2026"
    public string PeriodId { get; set; } = "";    // the same period as an id: "2026-06" | "2026-Q3" | ""
    public List<CityIndicatorValue> Cities { get; set; } = new();
}

public class CityIndicatorValue
{
    public string CityName { get; set; } = "";
    public string CityId { get; set; } = "";
    public double Value { get; set; }
    public string DisplayValue { get; set; } = "";
    public string Status { get; set; } = "";
    public int SortOrder { get; set; }
}

public class HistoricalIndicator
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public List<string> Years { get; set; } = new();
    public List<HistoricalCityRow> Cities { get; set; } = new();
    public string ThresholdType { get; set; } = "";
    public List<HistoricalThreshold> Thresholds { get; set; } = new();
}

public class HistoricalCityRow
{
    public string CityName { get; set; } = "";
    public Dictionary<string, double?> Values { get; set; } = new();
    public Dictionary<string, string> DisplayValues { get; set; } = new();
}

public class HistoricalThreshold
{
    public string Label { get; set; } = "";
    public double Min { get; set; }
    public double Max { get; set; }
    public string CssClass { get; set; } = "";
}

// Gap = supply − demand (negative = shortage). Annual and monthly points use actual supply/gap where IsActual,
// otherwise expected; demand is always expected.
public class SupplyDemandGapCity
{
    public string CityId { get; set; } = "";
    public string CityName { get; set; } = "";
    public double? VacancyAssumptionPct { get; set; }
    public GapKpis Kpis { get; set; } = new();
    public List<GapAnnualPoint> Annual { get; set; } = new();
    public List<GapMonthlyPoint> Monthly { get; set; } = new();
    public string? LastActualPeriod { get; set; }
}

public class GapKpis
{
    public GapYearKpi? FirstYear { get; set; }
    public GapYearKpi? SecondYear { get; set; }
    public GapMonthKpi? MonthExpected { get; set; }
    public GapMonthKpi? MonthActual { get; set; }
}

public class GapYearKpi
{
    public int Year { get; set; }
    public long Gap { get; set; }        // expected
    public long? ActualGap { get; set; } // null until the year has actuals
}

public class GapMonthKpi
{
    public string Period { get; set; } = "";
    public string Label { get; set; } = "";
    public long Gap { get; set; }
}

public class GapAnnualPoint
{
    public int Year { get; set; }
    public long Supply { get; set; }
    public long Demand { get; set; }
    public long Gap { get; set; }
    public long ExpectedGap { get; set; } // equals Gap unless IsActual
    public bool IsActual { get; set; }
}

public class GapMonthlyPoint
{
    public string Period { get; set; } = "";
    public string Label { get; set; } = "";
    public long Supply { get; set; }
    public long Demand { get; set; }
    public long Gap { get; set; }
    public long ExpectedGap { get; set; } // equals Gap unless IsActual
    public bool IsActual { get; set; }
}
