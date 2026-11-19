using System.Text.Json.Serialization;

namespace AqarInsights.Api.Models.ForeignOwnership;

public class TransactionRecord
{
    public DateTime? Date { get; set; }
    public string Month { get; set; } = "";
    public string TransactionType { get; set; } = "";
    public string Region { get; set; } = "";
    public string City { get; set; } = "";
    public string District { get; set; } = "";
    public string Nationality { get; set; } = "";
    public string PropertyType { get; set; } = "";
    public decimal Area { get; set; }
    public decimal Value { get; set; }
    public string OwnerType { get; set; } = "";
    // Owner's fraction of the property (0–1); null for sources without the column.
    public decimal? TransactionShare { get; set; }

    // Counting weight: the owner's share; sources without the column count each row as 1.
    [JsonIgnore]
    public decimal Weight => TransactionShare ?? 1m;
}
