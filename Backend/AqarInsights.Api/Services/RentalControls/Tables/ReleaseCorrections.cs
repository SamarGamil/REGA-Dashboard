using System.Text.Json;
using System.Text.Json.Nodes;
using AqarInsights.Api.Models.RentalControls.Tables;

namespace AqarInsights.Api.Services.RentalControls.Tables;

/// <summary>
/// Applies a release's guarded corrections to its rows: a correction changes one field of the single row its Match
/// selects, and only while that field still holds From; anything else is stale and leaves the data untouched.
/// </summary>
public static class ReleaseCorrections
{
    public static readonly IReadOnlyList<string> Tables =
        ["cities", "city_status", "observations", "market_movement", "city_narrative", "report_products", "report_axes",
         "media_monitoring", "media_items", "gap_cities"];

    public static (ReleaseData Data, IReadOnlyList<CorrectionOutcome> Outcomes) Apply(ReleaseData data, CorrectionsFile file)
    {
        var outcomes = new List<CorrectionOutcome>();
        foreach (var c in file.Corrections)
        {
            CorrectionOutcome outcome;
            (data, outcome) = c.Table switch
            {
                "cities" => With(data, c, data.Cities, (d, r) => d with { Cities = r }),
                "city_status" => With(data, c, data.CityStatus, (d, r) => d with { CityStatus = r }),
                "observations" => With(data, c, data.Observations, (d, r) => d with { Observations = r }),
                "market_movement" => With(data, c, data.MarketMovement, (d, r) => d with { MarketMovement = r }),
                "city_narrative" => With(data, c, data.CityNarrative, (d, r) => d with { CityNarrative = r }),
                "report_products" => With(data, c, data.ReportProducts, (d, r) => d with { ReportProducts = r }),
                "report_axes" => With(data, c, data.ReportAxes, (d, r) => d with { ReportAxes = r }),
                "media_monitoring" => With(data, c, data.MediaMonitoring, (d, r) => d with { MediaMonitoring = r }),
                "media_items" => With(data, c, data.MediaItems, (d, r) => d with { MediaItems = r }),
                "gap_cities" => With(data, c, data.GapCities, (d, r) => d with { GapCities = r }),
                _ => (data, new CorrectionOutcome(c.Id, false, $"stale: unknown table '{c.Table}'")),
            };
            outcomes.Add(outcome);
        }
        return (data, outcomes);
    }

    private static (ReleaseData, CorrectionOutcome) With<T>(
        ReleaseData data, CorrectionRow c, IReadOnlyList<T> rows, Func<ReleaseData, IReadOnlyList<T>, ReleaseData> set)
    {
        var nodes = rows.Select(r => JsonSerializer.SerializeToNode(r, TableJson.Options)!.AsObject()).ToList();
        var hits = Enumerable.Range(0, nodes.Count).Where(i => Matches(nodes[i], c.Match)).ToList();
        if (hits.Count != 1)
            return (data, new(c.Id, false, $"stale: {hits.Count} rows match in {c.Table}"));
        var node = nodes[hits[0]];
        if (!node.TryGetPropertyValue(c.Field, out var current))
            return (data, new(c.Id, false, $"stale: {c.Table} has no field '{c.Field}'"));
        if (!JsonNode.DeepEquals(current, JsonNode.Parse(c.From.GetRawText())))
            return (data, new(c.Id, false, $"stale: {c.Field} is {current?.ToJsonString(TableJson.Options) ?? "null"}, expected {c.From.GetRawText()}"));

        node[c.Field] = JsonNode.Parse(c.To.GetRawText());
        var updated = rows.ToList();
        updated[hits[0]] = node.Deserialize<T>(TableJson.Options)!;
        return (set(data, updated), new(c.Id, true, $"applied: {c.Field} {c.From.GetRawText()} → {c.To.GetRawText()}"));
    }

    private static bool Matches(JsonObject row, Dictionary<string, JsonElement> match)
        => match.All(m => row.TryGetPropertyValue(m.Key, out var v) && JsonNode.DeepEquals(v, JsonNode.Parse(m.Value.GetRawText())));
}
