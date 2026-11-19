using AqarInsights.Api.Models;
using AqarInsights.Api.Models.ForeignOwnership;
using AqarInsights.Api.Services;
using AqarInsights.Api.Services.ForeignOwnership;
using Microsoft.Extensions.Logging.Abstractions;

namespace AqarInsights.Api.Tests;

public class AggregationServiceTests
{
    [Fact]
    public void Summary_kpis_cover_latest_period_anchored_to_data_not_wall_clock()
    {
        var records = new List<TransactionRecord>
        {
            new() { Date = new DateTime(2026, 6, 10), Month = "2026-06", TransactionType = TransactionTypes.Purchase, Value = 100, Nationality = "اليمن", City = "الرياض", Region = "منطقة الرياض", PropertyType = "شقة" },
            new() { Date = new DateTime(2026, 6, 20), Month = "2026-06", TransactionType = TransactionTypes.Sale, Value = 200, Nationality = "اليمن", City = "الرياض", Region = "منطقة الرياض", PropertyType = "شقة" },
            new() { Date = new DateTime(2026, 5, 5), Month = "2026-05", TransactionType = TransactionTypes.Purchase, Value = 50, Nationality = "سوريا", City = "جدة", Region = "منطقة مكة", PropertyType = "فيلا" },
        };

        // Wall-clock is August 2026 (no data there); the latest data period is June.
        var svc = new AggregationService(
            new FakeLoader(records),
            NullLogger<AggregationService>.Instance,
            new FakeFilterOptions(),
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 27, 0, 0, 0, TimeSpan.Zero)));
        svc.Recompute();

        var kpi = svc.GetSummary().Kpi;

        // KPIs cover only the latest period (June); the May record is excluded from the scalar totals.
        Assert.Equal(2, kpi.TotalTransactions);
        Assert.Equal(300m, kpi.TotalValue);
        Assert.Equal("اليمن", kpi.TopNationality);
    }

    [Fact]
    public void City_filter_scopes_to_the_latest_period()
    {
        var records = new List<TransactionRecord>
        {
            // Global latest month is June (from الرياض) — the current period for the KPIs.
            new() { Date = new DateTime(2026, 6, 10), Month = "2026-06", TransactionType = TransactionTypes.Purchase, Value = 100, Nationality = "اليمن", City = "الرياض", Region = "منطقة الرياض", PropertyType = "شقة" },
            new() { Date = new DateTime(2026, 6, 20), Month = "2026-06", TransactionType = TransactionTypes.Sale, Value = 200, Nationality = "اليمن", City = "الرياض", Region = "منطقة الرياض", PropertyType = "شقة" },
            // القويعية only has a March transaction — outside the latest (June) period.
            new() { Date = new DateTime(2026, 3, 5), Month = "2026-03", TransactionType = TransactionTypes.Purchase, Value = 50, Nationality = "سوريا", City = "القويعية", Region = "منطقة الرياض", PropertyType = "فيلا" },
        };
        var svc = new AggregationService(new FakeLoader(records), NullLogger<AggregationService>.Instance,
            new FakeFilterOptions(), new FixedTimeProvider(new DateTimeOffset(2026, 8, 27, 0, 0, 0, TimeSpan.Zero)));
        svc.Recompute();

        var summary = svc.GetSummary(new DashboardFilterParams { City = "القويعية" });

        // KPIs reflect the current period (June), so القويعية's March transaction is not counted,
        // while the trend chart still spans the last 6 periods (ending at June).
        Assert.Equal(0, summary.Kpi.TotalTransactions);
        Assert.Equal("2026-06", summary.MonthlySummary[^1].Month);
        Assert.Equal(1, summary.MonthlySummary.First(m => m.Month == "2026-03").Total);
    }

    [Fact]
    public void Strategic_indices_for_evenly_split_regions_and_nationalities()
    {
        // 2 regions × 2 records, 2 nationalities evenly split → normalized HHI is 0.
        var records = new List<TransactionRecord>
        {
            new() { Date = new DateTime(2026, 6, 1), Month = "2026-06", Region = "أ", Nationality = "اليمن", City = "الرياض", TransactionType = TransactionTypes.Purchase, Value = 10 },
            new() { Date = new DateTime(2026, 6, 1), Month = "2026-06", Region = "أ", Nationality = "سوريا", City = "الرياض", TransactionType = TransactionTypes.Purchase, Value = 10 },
            new() { Date = new DateTime(2026, 6, 1), Month = "2026-06", Region = "ب", Nationality = "اليمن", City = "جدة", TransactionType = TransactionTypes.Purchase, Value = 10 },
            new() { Date = new DateTime(2026, 6, 1), Month = "2026-06", Region = "ب", Nationality = "سوريا", City = "جدة", TransactionType = TransactionTypes.Purchase, Value = 10 },
        };
        var svc = new AggregationService(new FakeLoader(records), NullLogger<AggregationService>.Instance,
            new FakeFilterOptions(), new FixedTimeProvider(new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero)));
        svc.Recompute();

        var s = svc.GetStrategic().Insights;
        Assert.Equal(0m, s.ConcentrationIndex);
        Assert.Equal(100m, s.GeographicSpreadIndex);
        Assert.Equal(50m, s.NationalityDiversityIndex);
    }

    [Fact]
    public void Strategic_single_region_is_max_concentration()
    {
        var records = new List<TransactionRecord>
        {
            new() { Date = new DateTime(2026, 6, 1), Month = "2026-06", Region = "أ", Nationality = "اليمن", City = "الرياض", TransactionType = TransactionTypes.Purchase, Value = 10 },
            new() { Date = new DateTime(2026, 6, 1), Month = "2026-06", Region = "أ", Nationality = "اليمن", City = "الرياض", TransactionType = TransactionTypes.Purchase, Value = 10 },
        };
        var svc = new AggregationService(new FakeLoader(records), NullLogger<AggregationService>.Instance,
            new FakeFilterOptions(), new FixedTimeProvider(new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero)));
        svc.Recompute();

        var s = svc.GetStrategic().Insights;
        Assert.Equal(100m, s.ConcentrationIndex);
        Assert.Equal(0m, s.GeographicSpreadIndex);
    }

    [Fact]
    public void OwnerType_filter_narrows_to_matching_records()
    {
        var records = new List<TransactionRecord>
        {
            new() { Date = new DateTime(2026, 6, 10), Month = "2026-06", TransactionType = TransactionTypes.Purchase, Value = 100, Nationality = "اليمن", City = "الرياض", Region = "منطقة الرياض", PropertyType = "شقة", OwnerType = OwnerTypes.Individual },
            new() { Date = new DateTime(2026, 6, 11), Month = "2026-06", TransactionType = TransactionTypes.Purchase, Value = 500, Nationality = "غير محدد", City = "الرياض", Region = "منطقة الرياض", PropertyType = "أرض", OwnerType = OwnerTypes.Company },
        };
        var svc = new AggregationService(new FakeLoader(records), NullLogger<AggregationService>.Instance,
            new FakeFilterOptions(), new FixedTimeProvider(new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero)));
        svc.Recompute();

        Assert.Equal(2, svc.GetSummary().Kpi.TotalTransactions);
        var company = svc.GetSummary(new DashboardFilterParams { OwnerType = OwnerTypes.Company }).Kpi;
        Assert.Equal(1, company.TotalTransactions);
        Assert.Equal(500m, company.TotalValue);
    }

    [Fact]
    public void OwnerType_counts_as_a_filter()
    {
        Assert.True(new DashboardFilterParams { OwnerType = OwnerTypes.Individual }.HasFilters());
    }
}
