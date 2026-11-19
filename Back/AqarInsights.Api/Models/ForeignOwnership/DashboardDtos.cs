namespace AqarInsights.Api.Models.ForeignOwnership;

public class KpiSummaryDto
{
    public decimal TotalTransactions { get; set; }
    public decimal TotalValue { get; set; }
    public decimal TotalPurchases { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalGifts { get; set; }
    public decimal? GrowthRate { get; set; }
    public decimal AvgTransactionValue { get; set; }
    public string TopNationality { get; set; } = "";
    public string TopMarket { get; set; } = "";
    public string TopPropertyCategory { get; set; } = "";
}

public class MonthlySummaryDto
{
    public string Month { get; set; } = "";
    public string Label { get; set; } = "";
    public decimal Purchases { get; set; }
    public decimal Sales { get; set; }
    public decimal Gifts { get; set; }
    public decimal Total { get; set; }
    public decimal Value { get; set; }
}

public class GrowthRateDto
{
    public string Month { get; set; } = "";
    public string Label { get; set; } = "";
    public decimal? Rate { get; set; }
}

public class RegionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Transactions { get; set; }
    public decimal Value { get; set; }
    public decimal PurchasePct { get; set; }
    public decimal SalePct { get; set; }
    public decimal GiftPct { get; set; }
}

public class NationalityDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Transactions { get; set; }
    public decimal Value { get; set; }
    public decimal AvgValue { get; set; }
    public decimal MarketShare { get; set; }
}

public class PropertyTypeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Transactions { get; set; }
    public decimal Value { get; set; }
    public decimal AvgValue { get; set; }
    public decimal Pct { get; set; }
}

public class AreaSegmentDto
{
    public int Id { get; set; }
    public string Range { get; set; } = "";
    public decimal Min { get; set; }
    public decimal Max { get; set; }
    public decimal Transactions { get; set; }
    public decimal Value { get; set; }
    public decimal AvgValue { get; set; }
}

public class TransactionValueDistributionDto
{
    public string Range { get; set; } = "";
    public decimal Count { get; set; }
    public decimal AvgValue { get; set; }
}

public class FinancialMetricsDto
{
    public decimal TotalValue { get; set; }
    public decimal AvgValue { get; set; }
    public decimal MedianValue { get; set; }
    public decimal MaxValue { get; set; }
    public decimal MinValue { get; set; }
}

public class HotSpotDto
{
    public int Id { get; set; }
    public string City { get; set; } = "";
    public string District { get; set; } = "";
    public decimal Transactions { get; set; }
    public decimal Value { get; set; }
    public decimal Growth { get; set; }
    public string Reason { get; set; } = "";
}

public class EmergingAreaDto
{
    public string City { get; set; } = "";
    public string District { get; set; } = "";
    public decimal Transactions { get; set; }
    public decimal Growth { get; set; }
    public string Trend { get; set; } = "";
}

public class HighGrowthRegionDto
{
    public string Region { get; set; } = "";
    public decimal Growth { get; set; }
    public string Reason { get; set; } = "";
}

public class OpportunityDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Priority { get; set; } = "";
}

public class StrategicInsightsDto
{
    public decimal ConcentrationIndex { get; set; }
    public decimal InvestmentConcentrationIndex { get; set; }
    public decimal NationalityDiversityIndex { get; set; }
    public decimal GeographicSpreadIndex { get; set; }
    public List<HotSpotDto> HotSpots { get; set; } = new();
    public List<EmergingAreaDto> EmergingAreas { get; set; } = new();
    public List<HighGrowthRegionDto> HighGrowthRegions { get; set; } = new();
    public List<OpportunityDto> TopOpportunities { get; set; } = new();
}

public class PropertyTypeByMonthDto
{
    public string Month { get; set; } = "";
    public string Label { get; set; } = "";
    public Dictionary<string, decimal> Types { get; set; } = new();
}

public class PropertyTypeByNationalityDto
{
    public string Nationality { get; set; } = "";
    public Dictionary<string, decimal> Types { get; set; } = new();
}

public class PropertyTypeByRegionDto
{
    public string Region { get; set; } = "";
    public Dictionary<string, decimal> Types { get; set; } = new();
}

public class AreaByNationalityDto
{
    public string Nationality { get; set; } = "";
    public Dictionary<string, decimal> Segments { get; set; } = new();
}

public class AreaByRegionDto
{
    public string Region { get; set; } = "";
    public Dictionary<string, decimal> Segments { get; set; } = new();
}

public class SummaryResponse
{
    public KpiSummaryDto Kpi { get; set; } = new();
    public List<MonthlySummaryDto> MonthlySummary { get; set; } = new();
    public List<GrowthRateDto> GrowthRates { get; set; } = new();
}

public class RegionsResponse
{
    public List<RegionDto> Regions { get; set; } = new();
    public List<PropertyTypeByRegionDto> PropertyTypeByRegion { get; set; } = new();
    public List<AreaByRegionDto> AreaByRegion { get; set; } = new();
}

public class NationalitiesResponse
{
    public List<NationalityDto> Nationalities { get; set; } = new();
    public List<PropertyTypeByNationalityDto> PropertyTypeByNationality { get; set; } = new();
    public List<AreaByNationalityDto> AreaByNationality { get; set; } = new();
}

public class PropertiesResponse
{
    public List<PropertyTypeDto> PropertyTypes { get; set; } = new();
    public List<PropertyTypeByMonthDto> PropertyTypeByMonth { get; set; } = new();
    public List<PropertyTypeByNationalityDto> PropertyTypeByNationality { get; set; } = new();
    public List<PropertyTypeByRegionDto> PropertyTypeByRegion { get; set; } = new();
}

public class AreasResponse
{
    public List<AreaSegmentDto> AreaSegments { get; set; } = new();
    public List<AreaByNationalityDto> AreaByNationality { get; set; } = new();
    public List<AreaByRegionDto> AreaByRegion { get; set; } = new();
}

public class FinancialResponse
{
    public FinancialMetricsDto Metrics { get; set; } = new();
    public List<TransactionValueDistributionDto> ValueDistribution { get; set; } = new();
    public List<MonthlySummaryDto> MonthlySummary { get; set; } = new();
    public List<GrowthRateDto> GrowthRates { get; set; } = new();
}

public class StrategicResponse
{
    public StrategicInsightsDto Insights { get; set; } = new();
}
