namespace AqarInsights.Api.Models.RentalControls;

public class OverviewData
{
    public List<CityData> Cities { get; set; } = new();
    public NationalSummaryData NationalSummary { get; set; } = new();
    public List<OperationalStage> OperationalStages { get; set; } = new();
    public List<ClassificationLegendItem> ClassificationLegend { get; set; } = new();
    public List<Recommendation> Recommendations { get; set; } = new();
}

public class CityData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string OperationalStatus { get; set; } = "";
    public string Classification { get; set; } = "";
    public string ClassificationLabel { get; set; } = "";
    public string Cagr { get; set; } = "";
    public string HousingBurden { get; set; } = "";
    public string InvestmentReturn { get; set; } = "";
    public string ActualRentalGrowth { get; set; } = "";
    public string VacancyRate { get; set; } = "";
    public string RentToIncome { get; set; } = "";
    public ImpactData ImpactData { get; set; } = new();
    public string Recommendation { get; set; } = "";
    public int Priority { get; set; }
}

public class ImpactData
{
    public bool Available { get; set; }
    public string BeforeValue { get; set; } = "—";
    public string AfterValue { get; set; } = "—";
    public string Change { get; set; } = "—";
    public string Description { get; set; } = "";
}

public class NationalSummaryData
{
    public int AcceleratedCities { get; set; }
    public int ElevatedCities { get; set; }
    public int SustainableCities { get; set; }
    public int RegulationActivated { get; set; }
    public int DetailedStudy { get; set; }
    public int ProactiveMonitoring { get; set; }
    public int RoutineMonitoring { get; set; }
}

public class OperationalStage
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public int Count { get; set; }
}

public class ClassificationLegendItem
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Range { get; set; } = "";
    public string ColorClass { get; set; } = "";
}

public class Recommendation
{
    public string City { get; set; } = "";
    public string Text { get; set; } = "";
}
