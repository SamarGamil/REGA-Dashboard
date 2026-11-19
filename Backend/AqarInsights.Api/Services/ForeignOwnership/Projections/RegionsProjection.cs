using AqarInsights.Api.Models;
using AqarInsights.Api.Models.ForeignOwnership;

namespace AqarInsights.Api.Services.ForeignOwnership.Projections;

public static class RegionsProjection
{
    public static RegionsResponse Build(IReadOnlyList<TransactionRecord> records, string period, DateTime now)
    {
        var periodRecords = PeriodMath.FilterByLatestPeriod(records, period, now);

        // Single-pass grouping by region
        var regionGroups = periodRecords
            .Where(r => !string.IsNullOrWhiteSpace(r.Region))
            .GroupBy(r => r.Region)
            .OrderByDescending(g => g.TxCount())
            .Select(g =>
            {
                var items = g.ToList();
                return new
                {
                    Region = g.Key,
                    Items = items,
                    Count = items.TxCount(),
                    PurchaseCount = items.TxCount(r => r.TransactionType == TransactionTypes.Purchase),
                    SaleCount = items.TxCount(r => r.TransactionType == TransactionTypes.Sale),
                    GiftCount = items.TxCount(r => r.TransactionType == TransactionTypes.Gift),
                    Value = items.Sum(r => r.Value)
                };
            })
            .ToList();

        var regions = regionGroups.Select((g, i) => new RegionDto
        {
            Id = i + 1,
            Name = g.Region,
            Transactions = g.Count.RoundCount(),
            Value = g.Value,
            PurchasePct = g.Count > 0 ? Math.Round(g.PurchaseCount / g.Count * 100, 0) : 0,
            SalePct = g.Count > 0 ? Math.Round(g.SaleCount / g.Count * 100, 0) : 0,
            GiftPct = g.Count > 0 ? Math.Round(g.GiftCount / g.Count * 100, 0) : 0
        }).ToList();

        var propertyByRegion = regionGroups.Select(g => new PropertyTypeByRegionDto
        {
            Region = g.Region,
            Types = g.Items.GroupBy(r => r.PropertyType).ToDictionary(pt => pt.Key, pt => pt.TxCount().RoundCount())
        }).ToList();

        var areaByRegion = regionGroups.Select(g => new AreaByRegionDto
        {
            Region = g.Region,
            Segments = ValueBuckets.ComputeAreaSegmentsDict(g.Items)
        }).ToList();

        return new RegionsResponse { Regions = regions, PropertyTypeByRegion = propertyByRegion, AreaByRegion = areaByRegion };
    }
}
