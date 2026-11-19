using System.Text.Encodings.Web;
using System.Text.Json;
using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Projections;
using AqarInsights.Api.Services.RentalControls.Reports;
using AqarInsights.Api.Tests.RentalControls.Reports;

namespace AqarInsights.Api.Tests.RentalControls.Snapshots;

// Parity proof for the move to release-tagged tables: every period's API documents and report models must stay
// byte-identical. RC_PERIOD_GOLDEN_WRITE=1 (re)writes the fixtures from the current pipeline.
public class PeriodGoldenTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    };

    private static readonly DateOnly Today = new(2026, 10, 1);

    private static readonly string FixturesDir =
        Path.Combine(RealReportData.SolutionRoot, "AqarInsights.Api.Tests", "Fixtures", "rental-controls-periods");

    private static bool WriteMode => Environment.GetEnvironmentVariable("RC_PERIOD_GOLDEN_WRITE") == "1";

    public static IEnumerable<object[]> PeriodIds()
    {
        yield return new object[] { "default" };
        var periods = RealReportData.Periods.GetPeriods();
        foreach (var p in periods.Monthly.Concat(periods.Quarterly))
            yield return new object[] { p.Id };
    }

    [Fact]
    public void Period_list_matches_the_golden_fixture()
    {
        var file = Path.Combine(FixturesDir, "periods.json");
        var produced = Serialize(RealReportData.Periods.GetPeriods());
        if (WriteMode)
        {
            Directory.CreateDirectory(FixturesDir);
            File.WriteAllText(file, produced);
            return;
        }
        Assert.True(File.Exists(file), "No periods.json fixture; run once with RC_PERIOD_GOLDEN_WRITE=1.");
        Assert.Equal(Normalize(File.ReadAllText(file)), Normalize(produced));
    }

    [Theory, MemberData(nameof(PeriodIds))]
    public async Task Period_documents_match_the_golden_fixtures(string periodId)
    {
        // The documents exactly as the API serves them and the reports render them (period rules applied).
        var data = (await RealReportData.Periods.ResolveAsync(periodId == "default" ? null : periodId))!;
        var agg = data.Aggregation;
        var scope = data.Scope;
        var indicators = PeriodValueMask.Apply(agg.GetIndicators(), scope);
        var docs = new List<(string Path, object? Value)>
        {
            ("period.json", new { data.Id, data.Label, data.IsMonthly, Quarter = scope.Quarter?.Id, Month = scope.Month?.Id, TextMonth = scope.TextMonth?.Id }),
            ("overview.json", agg.GetOverview()),
            ("indicators.json", indicators),
            ("market-analysis.json", agg.GetMarketAnalysis()),
            ("cities/index.json", agg.GetCityIndex()),
            ("reports/national.json", NationalReportModelBuilder.Build(
                agg.GetMarketAnalysis(), Today, new NationalReportOptions(), data.Label, data.IsMonthly, NationalReportService.ContextFor(data))),
        };
        foreach (var item in agg.GetCityIndex())
        {
            if (agg.GetCity(item.CityId) is not { } source) continue;
            var city = PeriodValueMask.Apply(source, scope);
            docs.Add(($"cities/{item.CityId}.json", city));
            var content = data.Content.Get(item.CityId);
            var overviewCity = agg.GetOverview().Cities.FirstOrDefault(c => c.Id == item.CityId);
            if (content is null || overviewCity is null) continue;
            var facts = CityReportFactsBuilder.Build(overviewCity, city, indicators.CurrentPeriodIndicators);
            docs.Add(($"reports/{item.CityId}.json", CityReportModelBuilder.Build(
                city, facts, CityReportModelBuilder.ForPeriod(content, scope, MonthlyCpiStatement.ForView(city, scope)), Today, data.Label, data.IsMonthly)));
        }

        AssertMatches(periodId, docs);
    }

    private static void AssertMatches(string periodId, List<(string Path, object? Value)> docs)
    {
        var dir = Path.Combine(FixturesDir, periodId);
        if (WriteMode)
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            foreach (var (path, value) in docs)
            {
                var file = Path.Combine(dir, path);
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.WriteAllText(file, Serialize(value));
            }
            return;
        }

        Assert.True(Directory.Exists(dir), $"No golden fixtures for {periodId}; run once with RC_PERIOD_GOLDEN_WRITE=1.");
        var stored = Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(dir, f).Replace('\\', '/')).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(stored, docs.Select(d => d.Path).Order(StringComparer.Ordinal).ToList());

        var mismatches = new List<string>();
        foreach (var (path, value) in docs)
        {
            var expected = Normalize(File.ReadAllText(Path.Combine(dir, path)));
            var actual = Normalize(Serialize(value));
            if (expected != actual) mismatches.Add($"{periodId}/{path}: {FirstDifference(expected, actual)}");
        }
        Assert.Empty(mismatches);
    }

    private static string Serialize(object? value) => JsonSerializer.Serialize(value, value?.GetType() ?? typeof(object), Json);

    private static string Normalize(string json) => json.Replace("\r\n", "\n").TrimEnd();

    private static string FirstDifference(string expected, string actual)
    {
        var i = 0;
        while (i < expected.Length && i < actual.Length && expected[i] == actual[i]) i++;
        string Around(string s)
        {
            var start = Math.Max(0, i - 40);
            return s.Substring(start, Math.Min(s.Length, i + 40) - start);
        }
        return $"char {i}: expected …{Around(expected)}… actual …{Around(actual)}…";
    }
}
