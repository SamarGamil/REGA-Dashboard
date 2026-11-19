using AqarInsights.Api.Models;
using AqarInsights.Api.Models.ForeignOwnership;

namespace AqarInsights.Api.Services.ForeignOwnership.Projections;

public static class PropertiesProjection
{
    public static PropertiesResponse Build(IReadOnlyList<TransactionRecord> records, string period, DateTime now)
    {
        var periodRecords = PeriodMath.FilterByLatestPeriod(records, period, now);
        var total = periodRecords.TxCount();
        var propertyTypes = periodRecords
            .GroupBy(r => r.PropertyType)
            .OrderByDescending(g => g.TxCount())
            .Select((g, i) =>
            {
                var count = g.TxCount();
                var value = g.Sum(r => r.Value);
                return new PropertyTypeDto
                {
                    Id = i + 1,
                    Name = g.Key,
                    Transactions = count.RoundCount(),
                    Value = value,
                    AvgValue = count > 0 ? Math.Round(value / count, 0) : 0,
                    Pct = total > 0 ? Math.Round(count / total * 100, 1) : 0
                };
            })
            .ToList();

        var propertyByMonthGrouped = records
            .Where(r => !string.IsNullOrEmpty(PeriodMath.GetPeriodGroupKey(r, period)))
            .GroupBy(r => PeriodMath.GetPeriodGroupKey(r, period))
            .ToDictionary(
                g => g.Key,
                g => new PropertyTypeByMonthDto
                {
                    Month = g.Key,
                    Label = PeriodMath.GetPeriodLabel(g.Key, period),
                    Types = g.GroupBy(r => r.PropertyType).ToDictionary(pt => pt.Key, pt => pt.TxCount().RoundCount())
                });

        var last6Keys = PeriodMath.GetLast6PeriodKeys(period, now);
        var propertyByMonth = last6Keys.Select(key => propertyByMonthGrouped.TryGetValue(key, out var existing)
            ? existing
            : new PropertyTypeByMonthDto { Month = key, Label = PeriodMath.GetPeriodLabel(key, period), Types = new Dictionary<string, decimal>() })
            .ToList();

        var propertyByNationality = periodRecords
            .Where(r => !string.IsNullOrWhiteSpace(r.Nationality))
            .GroupBy(r => r.Nationality)
            .OrderByDescending(g => g.TxCount())
            .Take(10)
            .Select(g => new PropertyTypeByNationalityDto
            {
                Nationality = g.Key,
                Types = g.GroupBy(r => r.PropertyType).ToDictionary(pt => pt.Key, pt => pt.TxCount().RoundCount())
            })
            .ToList();

        var propertyByRegion = periodRecords
            .Where(r => !string.IsNullOrWhiteSpace(r.Region))
            .GroupBy(r => r.Region)
            .OrderByDescending(g => g.TxCount())
            .Take(10)
            .Select(g => new PropertyTypeByRegionDto
            {
                Region = g.Key,
                Types = g.GroupBy(r => r.PropertyType).ToDictionary(pt => pt.Key, pt => pt.TxCount().RoundCount())
            })
            .ToList();

        return new PropertiesResponse { PropertyTypes = propertyTypes, PropertyTypeByMonth = propertyByMonth, PropertyTypeByNationality = propertyByNationality, PropertyTypeByRegion = propertyByRegion };
    }
}
