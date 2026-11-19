// One-time script to normalize Arabic text in existing JSON data files.
// Run with: dotnet run --project AqarInsights.Api -- --normalize-data
// After running, this flag can be removed.

using System.Text.Json;
using System.Text.RegularExpressions;
using AqarInsights.Api.Models;
using AqarInsights.Api.Models.ForeignOwnership;
using AqarInsights.Api.Services;
using AqarInsights.Api.Utilities;

namespace AqarInsights.Api.Tools;

public static class DataNormalizationScript
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task RunAsync(string dataDir)
    {
        Console.WriteLine($"Normalizing data in: {dataDir}");

        await NormalizeFilterOptions(Path.Combine(dataDir, "filter-options.json"));
        await NormalizeFilterOptions(Path.Combine(dataDir, "filter-options-updated.json"));
        await NormalizeTransactions(Path.Combine(dataDir, "transactions.json"));

        Console.WriteLine("Done.");
    }

    private static async Task NormalizeFilterOptions(string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"  Skipping (not found): {path}");
            return;
        }

        var json = await File.ReadAllTextAsync(path);
        var options = JsonSerializer.Deserialize<FilterOptions>(json, JsonOptions);
        if (options == null) return;

        int merged = 0;
        foreach (var region in options.Geography)
        {
            region.Name = ArabicNormalizer.Normalize(region.Name);
            merged += MergeAndNormalizeChildren(region.Children);
        }

        merged += MergeAndNormalizeChildren(options.Geography);

        var normalized = JsonSerializer.Serialize(options, JsonOptions);
        await File.WriteAllTextAsync(path, normalized);
        Console.WriteLine($"  Normalized: {Path.GetFileName(path)} (merged {merged} duplicates)");
    }

    private static int MergeAndNormalizeChildren(List<GeographyNode> nodes)
    {
        int totalMerged = 0;

        // Normalize all names first
        foreach (var node in nodes)
        {
            node.Name = ArabicNormalizer.Normalize(node.Name);
        }

        // Group by normalized name and merge children
        var grouped = nodes.GroupBy(n => n.Name).ToList();
        if (grouped.Count < nodes.Count)
        {
            totalMerged += nodes.Count - grouped.Count;
            nodes.Clear();
            foreach (var group in grouped)
            {
                var merged = new GeographyNode
                {
                    Name = group.Key,
                    Children = group.SelectMany(g => g.Children).ToList()
                };
                nodes.Add(merged);
            }
        }

        // Recurse into children
        foreach (var node in nodes)
        {
            if (node.Children.Count > 0)
                totalMerged += MergeAndNormalizeChildren(node.Children);
        }

        return totalMerged;
    }

    private static async Task NormalizeTransactions(string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"  Skipping (not found): {path}");
            return;
        }

        var json = await File.ReadAllTextAsync(path);
        var records = JsonSerializer.Deserialize<List<TransactionRecord>>(json, JsonOptions);
        if (records == null) return;

        foreach (var rec in records)
        {
            rec.Region = ArabicNormalizer.Normalize(rec.Region);
            rec.City = ArabicNormalizer.Normalize(rec.City);
            rec.District = ArabicNormalizer.Normalize(rec.District);
            rec.Nationality = NationalityNormalizer.Canonicalize(rec.Nationality);
        }

        var normalized = JsonSerializer.Serialize(records, JsonOptions);
        await File.WriteAllTextAsync(path, normalized);
        Console.WriteLine($"  Normalized: {Path.GetFileName(path)} ({records.Count} records)");
    }
}
