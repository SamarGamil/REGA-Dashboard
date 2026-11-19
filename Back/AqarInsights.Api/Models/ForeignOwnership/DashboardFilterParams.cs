namespace AqarInsights.Api.Models.ForeignOwnership;

public class DashboardFilterParams
{
    public string? Region { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public string? Nationalities { get; set; }
    public string? TransactionType { get; set; }
    public string? PropertyType { get; set; }
    public string? OwnerType { get; set; }
    public string? Period { get; set; } = "monthly";

    public List<string> GetNationalitiesList()
    {
        if (string.IsNullOrWhiteSpace(Nationalities)) return new();
        return Nationalities.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    public bool HasFilters()
    {
        return !string.IsNullOrWhiteSpace(Region)
            || !string.IsNullOrWhiteSpace(City)
            || !string.IsNullOrWhiteSpace(District)
            || !string.IsNullOrWhiteSpace(Nationalities)
            || !string.IsNullOrWhiteSpace(TransactionType)
            || !string.IsNullOrWhiteSpace(PropertyType)
            || !string.IsNullOrWhiteSpace(OwnerType);
    }
}
