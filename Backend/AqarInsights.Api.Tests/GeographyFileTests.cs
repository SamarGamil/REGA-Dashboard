using System.Text.Json;
using AqarInsights.Api.Models.ForeignOwnership;
using AqarInsights.Api.Services;
using AqarInsights.Api.Utilities;

namespace AqarInsights.Api.Tests;

// Guards the committed geography file: it is the single source for import region lookup and filters.
public class GeographyFileTests
{
    private static string DataFile(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AqarInsights.Api.sln")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, "AqarInsights.Api", "Data", name);
    }

    private static readonly List<(string Region, string City)> Pairs =
        JsonSerializer.Deserialize<FilterOptions>(File.ReadAllText(DataFile("filter-options-updated.json")), JsonDefaults.Read)!
            .Geography
            .SelectMany(r => r.Children.Select(c => (ArabicNormalizer.Normalize(r.Name), ArabicNormalizer.Normalize(c.Name))))
            .ToList();

    [Fact]
    public void Lists_all_13_regions()
        => Assert.Equal(13, Pairs.Select(p => p.Region).Distinct().Count());

    [Fact]
    public void Each_city_belongs_to_exactly_one_region()
    {
        var duplicates = Pairs.GroupBy(p => p.City).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.Empty(duplicates);
    }

    [Fact]
    public void Every_city_in_committed_data_is_listed()
    {
        var known = Pairs.Select(p => p.City).ToHashSet();
        var records = JsonSerializer.Deserialize<List<TransactionRecord>>(File.ReadAllText(DataFile("transactions.json")), JsonDefaults.Read)!;
        var unknown = records.Select(r => r.City).Distinct().Where(c => !known.Contains(c)).ToList();
        Assert.Empty(unknown);
    }
}
