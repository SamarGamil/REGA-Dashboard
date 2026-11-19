using System.Text.Json.Serialization;

namespace AqarInsights.Api.Models.RentalControls.Raw;

public class CityRecord
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public double Cagr { get; set; }
    public string Path { get; set; } = "";           // متسارع / مرتفع / استباقي
    public double Vacancy { get; set; }
    public double? InvestmentReturn { get; set; }     // null = "—"
    public double RentalIncrease { get; set; }
    public double RentToIncome { get; set; }
    public double Burden { get; set; }
    public string Status { get; set; } = "";          // الحالة column (explicit operational status)
    public string AssessmentLine1 { get; set; } = "";
    public string AssessmentLine2 { get; set; } = "";
}

public class IndicatorSeriesPoint
{
    public string Indicator { get; set; } = "";       // rentToIncome|burden|roi|publicVacancy|rentalChange
    public string CityId { get; set; } = "";
    public int Year { get; set; }
    public int Period { get; set; }                    // quarter or month
    public double Value { get; set; }
    public string? Nationality { get; set; }
    public string? UnitType { get; set; }
}

public class RawMovementRow
{
    public string Indicator { get; set; } = "";
    public string Level { get; set; } = "";
    public string Change { get; set; } = "";
    public string Assessment { get; set; } = "";
}

// Per-city media-monitoring block, sourced from the الرصد الإعلامي workbook.
public class RawMediaMonitoring
{
    public string Period { get; set; } = "";   // YYYY-MM; empty = unspecified (single-month back-compat)
    public bool Available { get; set; }
    public string OverallSentiment { get; set; } = "";
    public int PositivePct { get; set; }
    public int NeutralPct { get; set; }
    public int NegativePct { get; set; }
    public double DiscussionSharePct { get; set; }
    public List<string> Topics { get; set; } = new();
    public List<string> PositiveImpressions { get; set; } = new();
    public List<string> NeutralImpressions { get; set; } = new();
    public List<string> NegativeImpressions { get; set; } = new();
}

// In-memory shape of one period of a city series (a period + its by-key values).
// Not serialized directly — persisted in long/flat form as CitySeriesPoint rows.
public class RawSeriesPoint
{
    public string Period { get; set; } = "";
    public Dictionary<string, double> Values { get; set; } = new();
    public string? Change { get; set; }               // e.g. rent-index yearly change text
}

// Long-format, DB-table-shaped row: one value per (series, period, key). This is the
// only persisted form of the city series — see CityRawData.Series.
public class CitySeriesPoint
{
    public string Series { get; set; } = "";          // discriminator, see SeriesTypes
    public string Period { get; set; } = "";
    public string Key { get; set; } = "";
    public double Value { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Change { get; set; }
}

// Series discriminators for the flat CitySeriesPoint rows.
public static class SeriesTypes
{
    public const string Rentals = "rentals";
    public const string Sales = "sales";
    public const string RentIndex = "rentIndex";
    public const string RentIndexByType = "rentIndexByType";
    public const string Affordability = "affordability";
    public const string Roi = "roi";
    public const string Increase = "increase";
    public const string RentVsInflation = "rentVsInflation";
    public const string Burden = "burden";
}

public class CityRawData
{
    public string CityId { get; set; } = "";
    public List<string> Bullets { get; set; } = new();
    public List<RawMovementRow> MovementRows { get; set; } = new();

    // Optional media-monitoring blocks, one per month (chronological). null/empty when the
    // city has no monitored media data. The latest available month drives the card/report.
    // Files written before monthly history (≤ Sep 2026) hold a single object; it reads as a one-item list.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(SingleOrListConverter<RawMediaMonitoring>))]
    public List<RawMediaMonitoring>? Media { get; set; }

    // In-memory per-type series (consumed by the projections). Persisted only through Series.
    [JsonIgnore] public List<RawSeriesPoint> RentalsSeries { get; set; } = new();
    [JsonIgnore] public List<RawSeriesPoint> SalesSeries { get; set; } = new();
    [JsonIgnore] public List<RawSeriesPoint> RentIndexSeries { get; set; } = new();
    [JsonIgnore] public List<RawSeriesPoint> RentIndexByTypeSeries { get; set; } = new();
    [JsonIgnore] public List<RawSeriesPoint> AffordabilitySeries { get; set; } = new();
    [JsonIgnore] public List<RawSeriesPoint> RoiSeries { get; set; } = new();
    [JsonIgnore] public List<RawSeriesPoint> IncreaseSeries { get; set; } = new();
    [JsonIgnore] public List<RawSeriesPoint> RentVsInflationSeries { get; set; } = new();
    [JsonIgnore] public List<RawSeriesPoint> BurdenSeries { get; set; } = new();

    // The persisted, flat (DB-table-shaped) view of every series. Flattened on read,
    // distributed back into the per-type lists on write.
    public List<CitySeriesPoint> Series
    {
        get => FlattenSeries();
        set => DistributeSeries(value);
    }

    private IEnumerable<(string Name, List<RawSeriesPoint> Points)> SeriesByType() => new[]
    {
        (SeriesTypes.Rentals, RentalsSeries),
        (SeriesTypes.Sales, SalesSeries),
        (SeriesTypes.RentIndex, RentIndexSeries),
        (SeriesTypes.RentIndexByType, RentIndexByTypeSeries),
        (SeriesTypes.Affordability, AffordabilitySeries),
        (SeriesTypes.Roi, RoiSeries),
        (SeriesTypes.Increase, IncreaseSeries),
        (SeriesTypes.RentVsInflation, RentVsInflationSeries),
        (SeriesTypes.Burden, BurdenSeries),
    };

    private List<CitySeriesPoint> FlattenSeries()
    {
        var rows = new List<CitySeriesPoint>();
        foreach (var (name, points) in SeriesByType())
            foreach (var p in points)
                foreach (var kv in p.Values)
                    rows.Add(new CitySeriesPoint
                    {
                        Series = name,
                        Period = p.Period,
                        Key = kv.Key,
                        Value = kv.Value,
                        Change = p.Change
                    });
        return rows;
    }

    private void DistributeSeries(List<CitySeriesPoint> rows)
    {
        RentalsSeries = new(); SalesSeries = new(); RentIndexSeries = new();
        RentIndexByTypeSeries = new(); AffordabilitySeries = new(); RoiSeries = new();
        IncreaseSeries = new(); RentVsInflationSeries = new(); BurdenSeries = new();

        var lists = SeriesByType().ToDictionary(s => s.Name, s => s.Points, StringComparer.Ordinal);
        var current = new Dictionary<string, RawSeriesPoint>(StringComparer.Ordinal);

        foreach (var row in rows ?? Enumerable.Empty<CitySeriesPoint>())
        {
            if (!lists.TryGetValue(row.Series, out var list)) continue;
            // Contiguous rows sharing (series, period) reconstruct one RawSeriesPoint.
            if (!current.TryGetValue(row.Series, out var point) || point.Period != row.Period)
            {
                point = new RawSeriesPoint { Period = row.Period, Change = row.Change };
                list.Add(point);
                current[row.Series] = point;
            }
            point.Values[row.Key] = row.Value;
            point.Change ??= row.Change;
        }
    }
}
