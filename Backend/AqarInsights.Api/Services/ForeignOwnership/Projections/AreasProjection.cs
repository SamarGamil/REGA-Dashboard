using AqarInsights.Api.Models;
using AqarInsights.Api.Models.ForeignOwnership;

namespace AqarInsights.Api.Services.ForeignOwnership.Projections;

public static class AreasProjection
{
    public static AreasResponse Build(IReadOnlyList<TransactionRecord> records, string period, DateTime now)
    {
        var periodRecords = PeriodMath.FilterByLatestPeriod(records, period, now);
        var segments = new List<AreaSegmentDto>();
        for (int i = 0; i < ValueBuckets.AreaBounds.Length; i++)
        {
            var (min, max) = ValueBuckets.AreaBounds[i];
            var group = periodRecords.Where(r => ValueBuckets.InAreaBucket(r.Area, i)).ToList();
            var count = group.TxCount();
            var value = group.Sum(r => r.Value);
            segments.Add(new AreaSegmentDto
            {
                Id = i + 1,
                Range = ValueBuckets.AreaRanges[i],
                Min = min,
                Max = max,
                Transactions = count.RoundCount(),
                Value = value,
                AvgValue = count > 0 ? Math.Round(value / count, 0) : 0
            });
        }

        var areaByNationality = periodRecords
            .Where(r => !string.IsNullOrWhiteSpace(r.Nationality))
            .GroupBy(r => r.Nationality)
            .OrderByDescending(g => g.TxCount())
            .Take(10)
            .Select(g => new AreaByNationalityDto
            {
                Nationality = g.Key,
                Segments = ValueBuckets.ComputeAreaSegmentsDict(g.ToList())
            })
            .ToList();

        var areaByRegion = periodRecords
            .Where(r => !string.IsNullOrWhiteSpace(r.Region))
            .GroupBy(r => r.Region)
            .OrderByDescending(g => g.TxCount())
            .Take(10)
            .Select(g => new AreaByRegionDto
            {
                Region = g.Key,
                Segments = ValueBuckets.ComputeAreaSegmentsDict(g.ToList())
            })
            .ToList();

        return new AreasResponse { AreaSegments = segments, AreaByNationality = areaByNationality, AreaByRegion = areaByRegion };
    }
}
