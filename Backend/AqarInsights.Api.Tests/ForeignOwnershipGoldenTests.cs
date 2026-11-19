using System.Text.Encodings.Web;
using System.Text.Json;
using AqarInsights.Api.Models.ForeignOwnership;
using AqarInsights.Api.Services;
using AqarInsights.Api.Services.ForeignOwnership;
using Microsoft.Extensions.Logging.Abstractions;

namespace AqarInsights.Api.Tests;

// Golden regression guard for the foreign-ownership aggregation output.
// Feeds the committed Data/transactions.json through the real pipeline and snapshots each of the 7 responses.
// Output is anchored to the latest transaction date, so it is deterministic regardless of wall-clock.
// Regenerate fixtures with FO_GOLDEN_REGEN=1.
public class ForeignOwnershipGoldenTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    };

    private static string SolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AqarInsights.Api.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("solution root not found");
    }

    private static async Task<AggregationService> BuildAsync()
    {
        var root = SolutionRoot();
        var loader = new DataLoaderService(
            Path.Combine(root, "AqarInsights.Api", "Data", "transactions.json"),
            NullLogger<DataLoaderService>.Instance);
        await loader.LoadAsync();

        var svc = new AggregationService(loader, NullLogger<AggregationService>.Instance, new FakeFilterOptions(), TimeProvider.System);
        svc.Recompute();
        return svc;
    }

    [Fact]
    public async Task Output_matches_golden()
    {
        var svc = await BuildAsync();
        var goldenDir = Path.Combine(SolutionRoot(), "AqarInsights.Api.Tests", "Fixtures", "foreign-ownership-golden");
        Directory.CreateDirectory(goldenDir);

        var cases = new (string Name, object Value)[]
        {
            ("summary", svc.GetSummary()),
            ("regions", svc.GetRegions()),
            ("nationalities", svc.GetNationalities()),
            ("properties", svc.GetProperties()),
            ("areas", svc.GetAreas()),
            ("financial", svc.GetFinancial()),
            ("strategic", svc.GetStrategic()),
        };

        var regen = Environment.GetEnvironmentVariable("FO_GOLDEN_REGEN") == "1";
        foreach (var (name, value) in cases)
        {
            var produced = JsonSerializer.Serialize(value, Json);
            var path = Path.Combine(goldenDir, $"{name}.json");
            if (regen || !File.Exists(path))
            {
                await File.WriteAllTextAsync(path, produced);
                continue;
            }
            var golden = await File.ReadAllTextAsync(path);
            Assert.True(Normalize(golden) == Normalize(produced),
                $"FO golden mismatch for '{name}'. Review the diff; set FO_GOLDEN_REGEN=1 to regenerate if intended.");
        }
    }

    private static string Normalize(string s) => s.Replace("\r\n", "\n").Trim();
}
