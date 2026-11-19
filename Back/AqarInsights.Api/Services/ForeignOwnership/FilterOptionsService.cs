using System.Text.Json;
using AqarInsights.Api.Models;
using AqarInsights.Api.Models.ForeignOwnership;
using AqarInsights.Api.Utilities;

namespace AqarInsights.Api.Services.ForeignOwnership;

public class FilterOptionsService : IFilterOptionsService
{
    private const string NoDistrict = "لا يوجد";

    private readonly string _filePath;
    private readonly IDataLoaderService _dataLoader;
    private readonly ILogger<FilterOptionsService> _logger;
    private volatile FilterOptions? _cachedOptions;

    public FilterOptionsService(string filePath, IDataLoaderService dataLoader, ILogger<FilterOptionsService> logger)
    {
        _filePath = filePath;
        _dataLoader = dataLoader;
        _logger = logger;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        FilterOptions? options = null;
        if (File.Exists(_filePath))
        {
            var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
            options = JsonSerializer.Deserialize<FilterOptions>(json, JsonDefaults.Read);
        }
        else
        {
            _logger.LogWarning("Filter options file not found: {Path}", _filePath);
        }

        var records = _dataLoader.GetRecords();
        if (options == null && records.Count == 0) return;

        options ??= new FilterOptions();
        NormalizeGeography(options);
        MergeFromRecords(options, records);
        if (records.Count > 0) RemoveCitiesWithoutData(options, records);
        NormalizeNationalities(options);

        _cachedOptions = options;
        _logger.LogInformation("Loaded filter options from {Path} merged with {Count} records", _filePath, records.Count);
    }

    // The file is a hand-curated taxonomy; the loaded data adds any cities/districts/nationalities it lacks.
    private static void MergeFromRecords(FilterOptions options, IReadOnlyList<TransactionRecord> records)
    {
        foreach (var r in records)
        {
            if (string.IsNullOrWhiteSpace(r.Region) || string.IsNullOrWhiteSpace(r.City)) continue;

            var region = GetOrAdd(options.Geography, r.Region);
            var city = GetOrAdd(region.Children, r.City);
            if (!string.IsNullOrWhiteSpace(r.District) && r.District != NoDistrict)
                GetOrAdd(city.Children, r.District);
        }

        options.Nationalities.AddRange(records.Select(r => r.Nationality));

        var ownerTypes = records.Select(r => r.OwnerType).ToHashSet();
        options.OwnerTypes = OwnerTypes.All.Where(ownerTypes.Contains).ToList();

        var propertyTypes = records.Select(r => r.PropertyType).ToHashSet();
        options.PropertyTypes = PropertyCategories.All.Where(propertyTypes.Contains).ToList();

        var dates = records.Where(r => r.Date.HasValue).Select(r => r.Date!.Value).ToList();
        if (dates.Count > 0)
        {
            options.MinDate = dates.Min().ToString("yyyy-MM-dd");
            options.MaxDate = dates.Max().ToString("yyyy-MM-dd");
        }
    }

    // The file lists every city in the Kingdom (it also drives import region lookup); only offer ones with data.
    private static void RemoveCitiesWithoutData(FilterOptions options, IReadOnlyList<TransactionRecord> records)
    {
        var citiesWithData = records.Select(r => r.City).ToHashSet();
        foreach (var region in options.Geography)
            region.Children.RemoveAll(c => !citiesWithData.Contains(c.Name));
        options.Geography.RemoveAll(r => r.Children.Count == 0);
    }

    private static GeographyNode GetOrAdd(List<GeographyNode> nodes, string name)
    {
        var node = nodes.Find(n => n.Name == name);
        if (node == null)
        {
            node = new GeographyNode { Name = name };
            nodes.Add(node);
        }
        return node;
    }

    // Collapse variant spellings so the dropdown matches the canonicalised record values.
    private static void NormalizeNationalities(FilterOptions options)
    {
        options.Nationalities = options.Nationalities
            .Select(NationalityNormalizer.Canonicalize)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct()
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }

    private static void NormalizeGeography(FilterOptions options)
    {
        foreach (var region in options.Geography)
        {
            region.Name = ArabicNormalizer.Normalize(region.Name);
            foreach (var city in region.Children)
            {
                city.Name = ArabicNormalizer.Normalize(city.Name);
                foreach (var district in city.Children)
                {
                    district.Name = ArabicNormalizer.Normalize(district.Name);
                }
            }
        }
    }

    public FilterOptions? GetOptions() => _cachedOptions;
}
