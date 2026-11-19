using AqarInsights.Api.Models.RentalControls.Raw;
using AqarInsights.Api.Models.RentalControls.Tables;
using AqarInsights.Api.Services.RentalControls.Snapshots;
using AqarInsights.Api.Services.RentalControls.Tables;

namespace AqarInsights.Api.Tests.RentalControls.Tables;

public class MonthlyMovementTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    private static MappedRelease Data(params RawMovementRow[] rows) => MappedRelease.Empty with
    {
        Cities = [new CityRecord { Id = "riyadh", Name = "الرياض" }],
        CityRaw = new Dictionary<string, CityRawData> { ["riyadh"] = new() { CityId = "riyadh", MovementRows = rows.ToList() } },
    };

    private static RawMovementRow Row(string indicator, string level) => new() { Indicator = indicator, Level = level, Change = "+1.0%", Assessment = "ارتفاع" };

    [Fact]
    public void Captures_the_months_latest_month_rows_and_skips_rows_dated_another_month()
    {
        var data = Data(
            Row("عدد الصفقات الإيجارية", "مدينة - \u200E2026-Q3"),
            Row("الإيجار الفعلي للمسكن (CPI)", "مدينة - \u200E2026-08"),
            Row("التضخم العام (CPI)", "مدينة"),
            Row("التغير الإيجاري (شهري)", "مدينة - \u200E2026-09"));

        var (rows, skipped) = MonthlyMovement.Capture(data, new YearMonth(2026, 8), At);

        Assert.Equal(new[] { (1, "الإيجار الفعلي للمسكن (CPI)"), (2, "التضخم العام (CPI)") }, rows.Select(r => (r.Seq, r.Indicator)));
        Assert.All(rows, r => Assert.Equal(("2026-08", "riyadh", At), (r.Month, r.CityId, r.ImportedAt)));
        Assert.Contains("التغير الإيجاري (شهري)", Assert.Single(skipped));
    }

    [Fact]
    public void Upsert_replaces_only_that_month_and_ForMonth_groups_by_city()
    {
        MonthlyMovementRow R(string month, string city, string change) => new(month, city, 0, "التضخم العام (CPI)", "مدينة", change, "—", At);
        var table = MonthlyMovement.Upsert([R("2026-06", "riyadh", "+1%"), R("2026-08", "riyadh", "+2%")], new YearMonth(2026, 8), [R("2026-08", "abha", "+3%")]);

        Assert.Equal(new[] { "2026-06 riyadh", "2026-08 abha" }, table.Select(r => $"{r.Month} {r.CityId}"));
        Assert.Equal("+3%", MonthlyMovement.ForMonth(table, new YearMonth(2026, 8))!["abha"].Single().Change);
        Assert.Null(MonthlyMovement.ForMonth(table, new YearMonth(2026, 7)));
    }
}
