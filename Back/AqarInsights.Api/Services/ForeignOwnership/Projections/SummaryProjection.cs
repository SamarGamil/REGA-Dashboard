using AqarInsights.Api.Models;
using AqarInsights.Api.Models.ForeignOwnership;

namespace AqarInsights.Api.Services.ForeignOwnership.Projections;

public static class SummaryProjection
{
    public static SummaryResponse Build(IReadOnlyList<TransactionRecord> records, string period, DateTime now)
    {
        // KPIs reflect the current (latest) period; the trend charts below still span the last 6 periods.
        var kpiRecords = PeriodMath.FilterByLatestPeriod(records, period, now);
        var totalTransactions = kpiRecords.TxCount();
        var totalValue = kpiRecords.Sum(r => r.Value);
        var purchases = kpiRecords.TxCount(r => r.TransactionType == TransactionTypes.Purchase);
        var sales = kpiRecords.TxCount(r => r.TransactionType == TransactionTypes.Sale);
        var gifts = kpiRecords.TxCount(r => r.TransactionType == TransactionTypes.Gift);

        var grouped = records
            .Where(r => !string.IsNullOrEmpty(PeriodMath.GetPeriodGroupKey(r, period)))
            .GroupBy(r => PeriodMath.GetPeriodGroupKey(r, period))
            .OrderBy(g => g.Key)
            .Select(g => new MonthlySummaryDto
            {
                Month = g.Key,
                Label = PeriodMath.GetPeriodLabel(g.Key, period),
                Purchases = g.TxCount(r => r.TransactionType == TransactionTypes.Purchase).RoundCount(),
                Sales = g.TxCount(r => r.TransactionType == TransactionTypes.Sale).RoundCount(),
                Gifts = g.TxCount(r => r.TransactionType == TransactionTypes.Gift).RoundCount(),
                Total = g.TxCount().RoundCount(),
                Value = g.Sum(r => r.Value)
            })
            .ToList();

        var last6Keys = PeriodMath.GetLast6PeriodKeys(period, now);
        var groupedDict = grouped.ToDictionary(g => g.Month, g => g);
        var last6 = last6Keys.Select(key => groupedDict.TryGetValue(key, out var existing)
            ? existing
            : new MonthlySummaryDto { Month = key, Label = PeriodMath.GetPeriodLabel(key, period) })
            .ToList();

        var growthRates = new List<GrowthRateDto>();
        for (int i = 1; i < last6.Count; i++)
        {
            var prev = last6[i - 1].Total;
            var curr = last6[i].Total;
            // Null only when there's no prior baseline to divide by.
            decimal? rate = prev == 0 ? null : Math.Round((curr - prev) / prev * 100, 1);
            growthRates.Add(new GrowthRateDto { Month = last6[i].Month, Label = last6[i].Label, Rate = rate });
        }

        decimal? growthRate = null;
        if (last6.Count >= 2)
        {
            var prev = last6[^2].Total;
            var curr = last6[^1].Total;
            growthRate = prev == 0 ? null : Math.Round((curr - prev) / prev * 100, 1);
        }

        var topNationality = kpiRecords.Where(r => !string.IsNullOrWhiteSpace(r.Nationality))
            .GroupBy(r => r.Nationality).OrderByDescending(g => g.TxCount()).FirstOrDefault()?.Key ?? "";
        var topMarket = kpiRecords.Where(r => !string.IsNullOrWhiteSpace(r.City))
            .GroupBy(r => r.City).OrderByDescending(g => g.TxCount()).FirstOrDefault()?.Key ?? "";
        var topProperty = kpiRecords.GroupBy(r => r.PropertyType).OrderByDescending(g => g.TxCount()).FirstOrDefault()?.Key ?? "";

        return new SummaryResponse
        {
            Kpi = new KpiSummaryDto
            {
                TotalTransactions = totalTransactions.RoundCount(),
                TotalValue = totalValue,
                TotalPurchases = purchases.RoundCount(),
                TotalSales = sales.RoundCount(),
                TotalGifts = gifts.RoundCount(),
                GrowthRate = growthRate,
                AvgTransactionValue = totalTransactions > 0 ? Math.Round(totalValue / totalTransactions, 0) : 0,
                TopNationality = topNationality,
                TopMarket = topMarket,
                TopPropertyCategory = topProperty
            },
            MonthlySummary = last6,
            GrowthRates = growthRates
        };
    }
}
