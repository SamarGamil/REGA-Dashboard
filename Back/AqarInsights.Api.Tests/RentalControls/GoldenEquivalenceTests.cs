using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Tables;
using AqarInsights.Api.Services.RentalControls;
using AqarInsights.Api.Services.RentalControls.Store;
using AqarInsights.Api.Services.RentalControls.Tables;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace AqarInsights.Api.Tests.RentalControls;

/// <summary>
/// Golden equivalence integration test: verifies the pipeline (live release tables -> TableRentalControlsStore -> Recompute)
/// produces the SAME API output as the legacy page JSON.
/// </summary>
public class GoldenEquivalenceTests
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _tablesDir;
    private readonly string _goldenDir;

    public GoldenEquivalenceTests()
    {
        // JSON options matching the API convention
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };

        // Locate the release tables (API project's Data/rental-controls/tables)
        // Walking up from test assembly directory to find the solution root
        var testAssemblyDir = AppContext.BaseDirectory;
        var currentDir = new DirectoryInfo(testAssemblyDir);
        
        // Walk up to find the solution directory (where .sln file exists)
        while (currentDir != null && !File.Exists(Path.Combine(currentDir.FullName, "AqarInsights.Api.sln")))
        {
            currentDir = currentDir.Parent;
        }

        if (currentDir == null)
        {
            throw new InvalidOperationException("Could not locate solution root from test assembly directory");
        }

        var solutionRoot = currentDir.FullName;
        _tablesDir = Path.Combine(solutionRoot, "AqarInsights.Api", "Data", "rental-controls", "tables");
        _goldenDir = Path.Combine(solutionRoot, "AqarInsights.Api.Tests", "Fixtures", "rental-controls-golden");

        // Verify directories exist
        if (!Directory.Exists(_tablesDir))
        {
            throw new InvalidOperationException($"Release tables directory not found: {_tablesDir}");
        }
        if (!Directory.Exists(_goldenDir))
        {
            throw new InvalidOperationException($"Golden fixtures directory not found: {_goldenDir}");
        }
    }

    [Fact]
    public async Task NewPipeline_ProducesIdenticalOutput_ToLegacyPageJson()
    {
        // Arrange: Load the store from the generated raw data
        var store = new TableRentalControlsStore(new JsonTablesRepository(_tablesDir), ReleaseIds.Live);
        await store.LoadAsync();

        // Act: Recompute projections
        var service = new RentalControlsAggregationService(store);
        service.Recompute();

        // Assert: Overview
        var producedOverview = service.GetOverview();
        var goldenOverviewPath = Path.Combine(_goldenDir, "overview.json");
        AssertJsonEquivalent(producedOverview, goldenOverviewPath, "overview");

        // Assert: Indicators
        var producedIndicators = service.GetIndicators();
        var goldenIndicatorsPath = Path.Combine(_goldenDir, "indicators.json");
        AssertJsonEquivalent(producedIndicators, goldenIndicatorsPath, "indicators");

        // Assert: Market analysis
        var producedMarket = service.GetMarketAnalysis();
        var goldenMarketPath = Path.Combine(_goldenDir, "market-analysis.json");
        AssertJsonEquivalent(producedMarket, goldenMarketPath, "market-analysis");

        // Assert: City Index
        var producedIndex = service.GetCityIndex();
        var goldenIndexPath = Path.Combine(_goldenDir, "cities", "index.json");
        AssertJsonEquivalent(producedIndex, goldenIndexPath, "cities/index");

        // Assert: Each City
        var cityIds = producedIndex.Select(c => c.CityId).ToList();
        foreach (var cityId in cityIds)
        {
            var producedCity = service.GetCity(cityId);
            if (producedCity == null)
            {
                Assert.Fail($"City '{cityId}' returned null from GetCity but exists in index");
            }

            var goldenCityPath = Path.Combine(_goldenDir, "cities", $"{cityId}.json");
            if (File.Exists(goldenCityPath))
            {
                AssertJsonEquivalent(producedCity, goldenCityPath, $"cities/{cityId}");
            }
            else
            {
                // Golden fixture missing for this city - this is okay if city has no raw data
                Assert.Null(producedCity);
            }
        }
    }

    private void AssertJsonEquivalent<T>(T produced, string goldenPath, string documentName)
    {
        // Read golden fixture
        if (!File.Exists(goldenPath))
        {
            Assert.Fail($"Golden fixture not found: {goldenPath}");
        }

        var goldenJson = File.ReadAllText(goldenPath);

        // Serialize produced data
        var producedJson = JsonSerializer.Serialize(produced, _jsonOptions);

        // Normalize both by deserializing and re-serializing with same options
        // This handles whitespace/property-order differences
        var producedNormalized = NormalizeJson<T>(producedJson);
        var goldenNormalized = NormalizeJson<T>(goldenJson);

        // Compare
        if (producedNormalized != goldenNormalized)
        {
            // RC_GOLDEN_REGEN=1 rewrites only the fixtures that differ; review the diff before committing.
            if (Environment.GetEnvironmentVariable("RC_GOLDEN_REGEN") == "1")
            {
                File.WriteAllText(goldenPath, producedJson);
                return;
            }

            // Find first difference for helpful error message
            var diff = FindFirstDifference(producedNormalized, goldenNormalized);
            Assert.Fail($"Document '{documentName}' does not match golden fixture.\n" +
                       $"First difference at position {diff.Position}: \n" +
                       $"  Produced: '{diff.ProducedSnippet}'\n" +
                       $"  Golden:   '{diff.GoldenSnippet}'\n" +
                       $"Full produced JSON written to: {WriteDebugFile(documentName, producedNormalized)}");
        }
    }

    private string NormalizeJson<T>(string json)
    {
        // Deserialize and re-serialize to normalize formatting
        var obj = JsonSerializer.Deserialize<T>(json, _jsonOptions);
        return JsonSerializer.Serialize(obj, _jsonOptions);
    }

    private (int Position, string ProducedSnippet, string GoldenSnippet) FindFirstDifference(string produced, string golden)
    {
        var maxLen = Math.Max(produced.Length, golden.Length);
        for (int i = 0; i < maxLen; i++)
        {
            if (i >= produced.Length)
            {
                return (i, "<END>", GetSnippet(golden, i, 50));
            }
            if (i >= golden.Length)
            {
                return (i, GetSnippet(produced, i, 50), "<END>");
            }
            if (produced[i] != golden[i])
            {
                return (i, GetSnippet(produced, i, 50), GetSnippet(golden, i, 50));
            }
        }
        return (0, "", ""); // Should not reach here
    }

    private string GetSnippet(string text, int position, int length)
    {
        var start = Math.Max(0, position - 10);
        var end = Math.Min(text.Length, position + length);
        return text.Substring(start, end - start);
    }

    private string WriteDebugFile(string documentName, string content)
    {
        var debugDir = Path.Combine(Path.GetTempPath(), "rental-controls-debug");
        Directory.CreateDirectory(debugDir);
        var debugFile = Path.Combine(debugDir, $"{documentName.Replace('/', '-')}.json");
        File.WriteAllText(debugFile, content);
        return debugFile;
    }
}
