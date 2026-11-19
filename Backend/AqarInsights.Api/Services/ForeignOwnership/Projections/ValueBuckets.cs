using AqarInsights.Api.Models;
using AqarInsights.Api.Models.ForeignOwnership;

namespace AqarInsights.Api.Services.ForeignOwnership.Projections;

// Area/value bucket definitions and per-bucket helpers shared by the dashboard projections.
public static class ValueBuckets
{
    public static readonly string[] AreaRanges = ["0–500", "501–1,000", "1,001–2,000", "2,001–5,000", "5,001–10,000", "10,000+"];
    // Contiguous (min, max] cut-points (first bucket includes 0) so no area value can fall between buckets.
    public static readonly (decimal Min, decimal Max)[] AreaBounds = [(0, 500), (500, 1000), (1000, 2000), (2000, 5000), (5000, 10000), (10000, decimal.MaxValue)];

    public static readonly string[] ValueRanges = ["أقل من 500 ألف", "500 ألف – 1 مليون", "1 – 2 مليون", "2 – 3 مليون", "3 – 5 مليون", "5 – 10 مليون", "أكثر من 10 مليون"];
    public static readonly (decimal Min, decimal Max)[] ValueBounds = [(0, 500_000), (500_000, 1_000_000), (1_000_000, 2_000_000), (2_000_000, 3_000_000), (3_000_000, 5_000_000), (5_000_000, 10_000_000), (10_000_000, decimal.MaxValue)];

    // First bucket is inclusive of its lower edge (captures area 0); the rest are (min, max].
    public static bool InAreaBucket(decimal area, int i)
    {
        var (min, max) = AreaBounds[i];
        return i == 0 ? area >= min && area <= max : area > min && area <= max;
    }

    public static Dictionary<string, decimal> ComputeAreaSegmentsDict(IReadOnlyList<TransactionRecord> records)
    {
        var dict = new Dictionary<string, decimal>();
        for (int i = 0; i < AreaBounds.Length; i++)
        {
            var bucket = i;
            dict[AreaRanges[i]] = records.TxCount(r => InAreaBucket(r.Area, bucket)).RoundCount();
        }
        return dict;
    }

    public static decimal ComputeGrowth(decimal current, decimal previous)
        => previous > 0 ? Math.Round((current - previous) / previous * 100, 1) : (current > 0 ? 100 : 0);
}
