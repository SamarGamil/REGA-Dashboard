using System.Text.Json;
using AqarInsights.Api.Models.RentalControls.Tables;
using AqarInsights.Api.Services.RentalControls.Tables;

namespace AqarInsights.Api.Tests.RentalControls.Tables;

public class ReleaseCorrectionsTests
{
    private static readonly ReleaseRow Live = new(ReleaseIds.Live, ReleaseKinds.Live, "", "2026-Q3", "", default, null);

    private static ReleaseData Data() => new(Live)
    {
        Observations =
        [
            new(ReleaseIds.Live, 0, "jeddah", "gap.demandExp", Grains.Month, "2026-03", "", 1411910, null),
            new(ReleaseIds.Live, 1, "jeddah", "gap.demandExp", Grains.Month, "2026-04", "", 1415000, null),
            new(ReleaseIds.Live, 2, "makkah", "gap.demandExp", Grains.Month, "2026-03", "", 824181, null),
            new(ReleaseIds.Live, 3, "makkah", "gap.demandExp", Grains.Month, "2026-03", "x", 824181, null),
        ],
        CityNarrative = [new(ReleaseIds.Live, "tabuk", "recommendation", 0, "مراقبة - العائد 3.9% + الزيادة الايجارية")],
    };

    private static JsonElement J(object v) => JsonSerializer.SerializeToElement(v);

    private static CorrectionRow Gap(string city, string period, double from, double to)
    {
        var match = new Dictionary<string, JsonElement> { ["cityId"] = J(city), ["measure"] = J("gap.demandExp"), ["grain"] = J("month"), ["period"] = J(period) };
        return new($"gap-{city}-{period}", "observations", match, "value", J(from), J(to), "x.5 rounded down", "2026-11-01");
    }

    [Fact]
    public void A_correction_whose_field_holds_from_is_applied()
    {
        var (data, outcomes) = ReleaseCorrections.Apply(Data(), new([Gap("jeddah", "2026-03", 1411910, 1411911)], []));

        Assert.Equal(1411911, data.Observations[0].Value);
        Assert.Equal(1415000, data.Observations[1].Value);
        Assert.True(Assert.Single(outcomes).Applied);
    }

    [Fact]
    public void A_changed_value_makes_the_correction_stale_and_untouched()
    {
        var (data, outcomes) = ReleaseCorrections.Apply(Data(), new([Gap("jeddah", "2026-03", 1411000, 1411911)], []));

        Assert.Equal(1411910, data.Observations[0].Value);
        var o = Assert.Single(outcomes);
        Assert.False(o.Applied);
        Assert.Contains("1411910", o.Detail);
    }

    [Fact]
    public void No_match_or_several_matches_are_stale()
    {
        var (_, outcomes) = ReleaseCorrections.Apply(Data(), new(
            [Gap("riyadh", "2026-03", 1, 2), Gap("makkah", "2026-03", 824181, 824182)], []));

        Assert.All(outcomes, o => Assert.False(o.Applied));
        Assert.Contains("0 rows", outcomes[0].Detail);
        Assert.Contains("2 rows", outcomes[1].Detail);
    }

    [Fact]
    public void Text_fields_are_corrected_the_same_way()
    {
        var c = new CorrectionRow("tabuk-rec", "city_narrative",
            new() { ["cityId"] = J("tabuk"), ["section"] = J("recommendation"), ["seq"] = J(0) },
            "text", J("مراقبة - العائد 3.9% + الزيادة الايجارية"), J("مراقبة - العائد 3.9%"), "increase is within the limit", "2026-11-02");

        var (data, outcomes) = ReleaseCorrections.Apply(Data(), new([c], ["a note"]));

        Assert.Equal("مراقبة - العائد 3.9%", data.CityNarrative[0].Text);
        Assert.True(Assert.Single(outcomes).Applied); // notes are never applied or reported
    }

    [Fact]
    public void An_unknown_table_or_field_is_stale()
    {
        var bad = Gap("jeddah", "2026-03", 1411910, 1411911) with { Table = "nope" };
        var badField = Gap("jeddah", "2026-03", 1411910, 1411911) with { Field = "nope" };

        var (_, outcomes) = ReleaseCorrections.Apply(Data(), new([bad, badField], []));

        Assert.All(outcomes, o => Assert.False(o.Applied));
    }
}
