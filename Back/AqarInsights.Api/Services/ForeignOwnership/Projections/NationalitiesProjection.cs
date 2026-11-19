using AqarInsights.Api.Models;
using AqarInsights.Api.Models.ForeignOwnership;

namespace AqarInsights.Api.Services.ForeignOwnership.Projections;

public static class NationalitiesProjection
{
    public static NationalitiesResponse Build(IReadOnlyList<TransactionRecord> records, string period, DateTime now)
    {
        var periodRecords = PeriodMath.FilterByLatestPeriod(records, period, now);
        var total = periodRecords.TxCount();

        // Single-pass grouping by nationality
        var nationalityGroups = periodRecords
            .Where(r => !string.IsNullOrWhiteSpace(r.Nationality))
            .GroupBy(r => r.Nationality)
            .OrderByDescending(g => g.TxCount())
            .Select(g =>
            {
                var items = g.ToList();
                return new { Nationality = g.Key, Items = items, Count = items.TxCount(), Value = items.Sum(r => r.Value) };
            })
            .ToList();

        var nationalities = nationalityGroups.Select((g, i) => new NationalityDto
        {
            Id = i + 1,
            Name = g.Nationality,
            Transactions = g.Count.RoundCount(),
            Value = g.Value,
            AvgValue = g.Count > 0 ? Math.Round(g.Value / g.Count, 0) : 0,
            MarketShare = total > 0 ? Math.Round(g.Count / total * 100, 1) : 0
        }).ToList();

        var top10 = nationalityGroups.Take(10).ToList();

        var propertyByNationality = top10.Select(g => new PropertyTypeByNationalityDto
        {
            Nationality = g.Nationality,
            Types = g.Items.GroupBy(r => r.PropertyType).ToDictionary(pt => pt.Key, pt => pt.TxCount().RoundCount())
        }).ToList();

        var areaByNationality = top10.Select(g => new AreaByNationalityDto
        {
            Nationality = g.Nationality,
            Segments = ValueBuckets.ComputeAreaSegmentsDict(g.Items)
        }).ToList();

        return new NationalitiesResponse { Nationalities = nationalities, PropertyTypeByNationality = propertyByNationality, AreaByNationality = areaByNationality };
    }
}
