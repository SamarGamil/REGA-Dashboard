using AqarInsights.Api.Models;
using AqarInsights.Api.Models.ForeignOwnership;

namespace AqarInsights.Api.Services.ForeignOwnership.Projections;

public static class FinancialProjection
{
    public static FinancialResponse Build(IReadOnlyList<TransactionRecord> records, string period, DateTime now)
    {
        // Metrics + value distribution reflect the current (latest) period; the trend below spans 6 periods.
        var currentRecords = PeriodMath.FilterByLatestPeriod(records, period, now);
        var valued = currentRecords.Where(r => r.Value > 0).ToList();
        var values = valued.Select(r => r.Value).OrderBy(v => v).ToList();
        var valuedCount = valued.TxCount();

        decimal median = 0;
        if (values.Count > 0)
        {
            var mid = values.Count / 2;
            median = values.Count % 2 == 0 ? (values[mid - 1] + values[mid]) / 2 : values[mid];
        }

        var distribution = new List<TransactionValueDistributionDto>();
        for (int i = 0; i < ValueBuckets.ValueBounds.Length; i++)
        {
            var (min, max) = ValueBuckets.ValueBounds[i];
            // Inclusive lower, exclusive upper — except for last bucket which is inclusive both
            var group = i == ValueBuckets.ValueBounds.Length - 1
                ? currentRecords.Where(r => r.Value >= min).ToList()
                : currentRecords.Where(r => r.Value >= min && r.Value < max).ToList();
            var count = group.TxCount();
            distribution.Add(new TransactionValueDistributionDto
            {
                Range = ValueBuckets.ValueRanges[i],
                Count = count.RoundCount(),
                AvgValue = count > 0 ? Math.Round(group.Sum(r => r.Value) / count, 0) : 0
            });
        }

        var monthlySummaryGrouped = records
            .Where(r => !string.IsNullOrEmpty(PeriodMath.GetPeriodGroupKey(r, period)))
            .GroupBy(r => PeriodMath.GetPeriodGroupKey(r, period))
            .ToDictionary(
                g => g.Key,
                g => new MonthlySummaryDto
                {
                    Month = g.Key,
                    Label = PeriodMath.GetPeriodLabel(g.Key, period),
                    Total = g.TxCount().RoundCount(),
                    Value = g.Sum(r => r.Value)
                });

        var last6Keys = PeriodMath.GetLast6PeriodKeys(period, now);
        var monthlySummary = last6Keys.Select(key => monthlySummaryGrouped.TryGetValue(key, out var existing)
            ? existing
            : new MonthlySummaryDto { Month = key, Label = PeriodMath.GetPeriodLabel(key, period) })
            .ToList();

        var firstData = monthlySummary.FindIndex(m => m.Total > 0);
        var growthRates = new List<GrowthRateDto>();
        for (int i = 1; i < monthlySummary.Count; i++)
        {
            var prev = monthlySummary[i - 1].Value;
            var curr = monthlySummary[i].Value;
            // Null only when there is no positive prior-period value to divide by.
            decimal? rate = prev <= 0 ? null : Math.Round((curr - prev) / prev * 100, 1);
            growthRates.Add(new GrowthRateDto { Month = monthlySummary[i].Month, Label = monthlySummary[i].Label, Rate = rate });
        }

        return new FinancialResponse
        {
            Metrics = new FinancialMetricsDto
            {
                TotalValue = values.Sum(),
                AvgValue = valuedCount > 0 ? Math.Round(values.Sum() / valuedCount, 0) : 0,
                MedianValue = median,
                MaxValue = values.Count > 0 ? values[^1] : 0,
                MinValue = values.Count > 0 ? values[0] : 0
            },
            ValueDistribution = distribution,
            MonthlySummary = monthlySummary,
            GrowthRates = growthRates
        };
    }
}
