using AqarInsights.Api.Models.ForeignOwnership;

namespace AqarInsights.Api.Services.ForeignOwnership.Projections;

// Transaction "counts" are the sum of TransactionShare. Ratios/averages use the unrounded sums;
// only the published count fields are rounded (1 dp).
public static class ShareWeighting
{
    public static decimal TxCount(this IEnumerable<TransactionRecord> records)
        => records.Sum(r => r.Weight);

    public static decimal TxCount(this IEnumerable<TransactionRecord> records, Func<TransactionRecord, bool> predicate)
        => records.Where(predicate).Sum(r => r.Weight);

    public static decimal RoundCount(this decimal count)
        => Math.Round(count, 1, MidpointRounding.AwayFromZero);
}
