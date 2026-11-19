namespace AqarInsights.Api.Models.RentalControls.Raw;

// One city sheet of supply-demand-gap.xlsx. Gap = supply − demand (negative = shortage). Persisted as gap.* rows of
// the release's observations table (cities listed in gap_cities).
public class RawSupplyDemandGapCity
{
    public string CityId { get; set; } = "";
    public double? VacancyAssumptionPct { get; set; }
    public List<RawGapAnnualRow> Annual { get; set; } = new();
    public List<RawGapMonthlyRow> Monthly { get; set; } = new();
}

public record RawGapAnnualRow
{
    public int Year { get; set; }
    public long DemandExp { get; set; }
    public long SupplyExp { get; set; }
    public long? SupplyAct { get; set; }
    public long GapExp { get; set; }
    public long? GapAct { get; set; }
}

public record RawGapMonthlyRow
{
    public string Period { get; set; } = ""; // yyyy-MM
    public long DemandExp { get; set; }
    public long SupplyExp { get; set; }
    public long? SupplyAct { get; set; }
    public long GapExp { get; set; }
    public long? GapAct { get; set; }
}
