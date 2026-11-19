using AqarInsights.Api.Models.ForeignOwnership;
using AqarInsights.Api.Services.ForeignOwnership;
using Microsoft.Extensions.Logging.Abstractions;

namespace AqarInsights.Api.Tests;

public class FilterOptionsServiceTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"filter-options-{Guid.NewGuid():N}.json");

    public void Dispose() => File.Delete(_file);

    private async Task<FilterOptions> Load(string json, params TransactionRecord[] records)
    {
        File.WriteAllText(_file, json);
        var svc = new FilterOptionsService(_file, new FakeLoader(records), NullLogger<FilterOptionsService>.Instance);
        await svc.LoadAsync();
        return svc.GetOptions()!;
    }

    private const string BaseJson = """
        {
          "geography": [
            { "name": "منطقة الرياض", "children": [ { "name": "الرياض", "children": [ { "name": "الملقا", "children": [] } ] } ] }
          ],
          "nationalities": [ "مصر" ],
          "minDate": "2020-01-01",
          "maxDate": "2030-01-01"
        }
        """;

    [Fact]
    public async Task Adds_cities_and_districts_missing_from_file()
    {
        var o = await Load(BaseJson,
            new TransactionRecord { Region = "منطقة الرياض", City = "الرياض", District = "النرجس" },
            new TransactionRecord { Region = "منطقة الرياض", City = "الدلم", District = "حي الخالدية" },
            new TransactionRecord { Region = "منطقة الجوف", City = "سكاكا", District = "لا يوجد" });

        var riyadh = Assert.Single(o.Geography, r => r.Name == "منطقة الرياض");
        Assert.Equal(["الملقا", "النرجس"], riyadh.Children.Single(c => c.Name == "الرياض").Children.Select(d => d.Name));
        Assert.Equal(["حي الخالدية"], riyadh.Children.Single(c => c.Name == "الدلم").Children.Select(d => d.Name));

        var jouf = Assert.Single(o.Geography, r => r.Name == "منطقة الجوف");
        Assert.Empty(Assert.Single(jouf.Children).Children);
    }

    [Fact]
    public async Task Hides_cities_and_regions_without_data()
    {
        var o = await Load("""
            {
              "geography": [
                { "name": "منطقة الرياض", "children": [ { "name": "الرياض", "children": [] }, { "name": "عفيف", "children": [] } ] },
                { "name": "منطقة نجران", "children": [ { "name": "نجران", "children": [] } ] }
              ]
            }
            """,
            new TransactionRecord { Region = "منطقة الرياض", City = "الرياض" });

        var region = Assert.Single(o.Geography);
        Assert.Equal("منطقة الرياض", region.Name);
        Assert.Equal("الرياض", Assert.Single(region.Children).Name);
    }

    [Fact]
    public async Task Merges_nationalities_and_takes_date_range_from_data()
    {
        var o = await Load(BaseJson,
            new TransactionRecord { Region = "منطقة الرياض", City = "الرياض", Nationality = "قطر", Date = new DateTime(2025, 2, 18) },
            new TransactionRecord { Region = "منطقة الرياض", City = "الرياض", Nationality = "مصر", Date = new DateTime(2026, 9, 28) });

        Assert.Equal(["قطر", "مصر"], o.Nationalities);
        Assert.Equal("2025-02-18", o.MinDate);
        Assert.Equal("2026-09-28", o.MaxDate);
    }

    [Fact]
    public async Task Owner_types_list_only_values_present_in_fixed_order()
    {
        var o = await Load(BaseJson,
            new TransactionRecord { Region = "منطقة الرياض", City = "الرياض", OwnerType = OwnerTypes.Company },
            new TransactionRecord { Region = "منطقة الرياض", City = "الرياض", OwnerType = OwnerTypes.Individual },
            new TransactionRecord { Region = "منطقة الرياض", City = "الرياض", OwnerType = "" });

        Assert.Equal([OwnerTypes.Individual, OwnerTypes.Company], o.OwnerTypes);
    }

    [Fact]
    public async Task Owner_types_empty_for_legacy_data()
    {
        var o = await Load(BaseJson, new TransactionRecord { Region = "منطقة الرياض", City = "الرياض" });
        Assert.Empty(o.OwnerTypes);
    }

    [Fact]
    public async Task Property_types_list_present_categories_in_canonical_order()
    {
        var o = await Load(BaseJson,
            new TransactionRecord { Region = "منطقة الرياض", City = "الرياض", PropertyType = PropertyCategories.Other },
            new TransactionRecord { Region = "منطقة الرياض", City = "الرياض", PropertyType = PropertyCategories.ResidentialApartment },
            new TransactionRecord { Region = "منطقة الرياض", City = "الرياض", PropertyType = PropertyCategories.ResidentialBuilding });

        Assert.Equal([PropertyCategories.ResidentialBuilding, PropertyCategories.ResidentialApartment, PropertyCategories.Other], o.PropertyTypes);
    }
}
