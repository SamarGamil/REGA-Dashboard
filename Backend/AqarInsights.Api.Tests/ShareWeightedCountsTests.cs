using AqarInsights.Api.Models.ForeignOwnership;
using AqarInsights.Api.Services.ForeignOwnership;
using AqarInsights.Api.Services.ForeignOwnership.Projections;
using Microsoft.Extensions.Logging.Abstractions;

namespace AqarInsights.Api.Tests;

// Transaction counts are the sum of TransactionShare (1 dp), not the number of rows.
public class ShareWeightedCountsTests
{
    private static TransactionRecord Rec(string type, decimal? share, decimal value,
        string nat = "مصر", string pt = PropertyCategories.VacantLand, decimal area = 300, string district = "الملقا")
        => new()
        {
            Date = new DateTime(2026, 6, 10), Month = "2026-06", TransactionType = type, TransactionShare = share,
            Value = value, Nationality = nat, City = "الرياض", Region = "منطقة الرياض", District = district,
            PropertyType = pt, Area = area,
        };

    private static AggregationService Svc(params TransactionRecord[] records)
    {
        var svc = new AggregationService(new FakeLoader(records.ToList()), NullLogger<AggregationService>.Instance,
            new FakeFilterOptions(), new FixedTimeProvider(new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero)));
        svc.Recompute();
        return svc;
    }

    [Fact]
    public void Kpis_sum_shares_and_missing_share_counts_as_one()
    {
        var summary = Svc(
            Rec(TransactionTypes.Purchase, 0.5m, 100),
            Rec(TransactionTypes.Purchase, 0.25m, 50),
            Rec(TransactionTypes.Sale, 1m, 300),
            Rec(TransactionTypes.Gift, null, 0)).GetSummary();

        Assert.Equal(2.8m, summary.Kpi.TotalTransactions);   // 2.75 → 2.8
        Assert.Equal(0.8m, summary.Kpi.TotalPurchases);      // 0.75 → 0.8
        Assert.Equal(1m, summary.Kpi.TotalSales);
        Assert.Equal(1m, summary.Kpi.TotalGifts);
        Assert.Equal(164m, summary.Kpi.AvgTransactionValue); // 450 / 2.75 = 163.6
        var june = summary.MonthlySummary[^1];
        Assert.Equal(2.8m, june.Total);
        Assert.Equal(0.8m, june.Purchases);
    }

    [Fact]
    public void Breakdowns_are_share_weighted()
    {
        var svc = Svc(
            Rec(TransactionTypes.Purchase, 0.5m, 400_000),
            Rec(TransactionTypes.Purchase, 0.5m, 400_000),
            Rec(TransactionTypes.Sale, 0.2m, 600_000, nat: "الهند", pt: PropertyCategories.Floor, area: 800));

        var region = Assert.Single(svc.GetRegions().Regions);
        Assert.Equal(1.2m, region.Transactions);
        Assert.Equal(83m, region.PurchasePct);
        Assert.Equal(17m, region.SalePct);

        var egypt = svc.GetNationalities().Nationalities[0];
        Assert.Equal("مصر", egypt.Name);
        Assert.Equal(1m, egypt.Transactions);
        Assert.Equal(83.3m, egypt.MarketShare);
        Assert.Equal(800_000m, egypt.AvgValue);

        var props = svc.GetProperties();
        var floor = Assert.Single(props.PropertyTypes, p => p.Name == PropertyCategories.Floor);
        Assert.Equal(0.2m, floor.Transactions);
        Assert.Equal(16.7m, floor.Pct);
        Assert.Equal(3_000_000m, floor.AvgValue);
        Assert.Equal(1m, props.PropertyTypeByRegion[0].Types[PropertyCategories.VacantLand]);
        Assert.Equal(1.2m, props.PropertyTypeByMonth[^1].Types.Values.Sum());

        var areas = svc.GetAreas();
        Assert.Equal(1m, areas.AreaSegments.Single(a => a.Range == "0–500").Transactions);
        Assert.Equal(0.2m, areas.AreaSegments.Single(a => a.Range == "501–1,000").Transactions);
        Assert.Equal(0.2m, areas.AreaByRegion[0].Segments["501–1,000"]);

        var fin = svc.GetFinancial();
        Assert.Equal(1m, fin.ValueDistribution.Single(d => d.Range == "أقل من 500 ألف").Count);
        Assert.Equal(0.2m, fin.ValueDistribution.Single(d => d.Range == "500 ألف – 1 مليون").Count);
        Assert.Equal(1_166_667m, fin.Metrics.AvgValue);      // 1,400,000 / 1.2

        Assert.Equal(1.2m, Assert.Single(svc.GetStrategic().Insights.HotSpots).Transactions);
    }

    [Fact]
    public void Rankings_use_share_sum_not_row_count()
    {
        var svc = Svc(
            Rec(TransactionTypes.Purchase, 0.1m, 10, nat: "الهند", pt: PropertyCategories.Other),
            Rec(TransactionTypes.Purchase, 0.1m, 10, nat: "الهند", pt: PropertyCategories.Other),
            Rec(TransactionTypes.Purchase, 0.1m, 10, nat: "الهند", pt: PropertyCategories.Other),
            Rec(TransactionTypes.Purchase, 1m, 10, nat: "مصر", pt: PropertyCategories.Floor));

        Assert.Equal("مصر", svc.GetSummary().Kpi.TopNationality);
        Assert.Equal(PropertyCategories.Floor, svc.GetSummary().Kpi.TopPropertyCategory);
        Assert.Equal("مصر", svc.GetNationalities().Nationalities[0].Name);
        Assert.Equal(PropertyCategories.Floor, svc.GetProperties().PropertyTypes[0].Name);
    }

    [Fact]
    public void Growth_accepts_fractional_counts()
        => Assert.Equal(50m, ValueBuckets.ComputeGrowth(1.5m, 1m));
}
