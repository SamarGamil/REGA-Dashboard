using AqarInsights.Api.Models.RentalControls.Raw;
using AqarInsights.Api.Services.RentalControls.Projections;

namespace AqarInsights.Api.Tests.RentalControls;

public class SupplyDemandGapProjectionTests
{
    private static readonly Dictionary<string, string> Names = new()
    {
        ["riyadh"] = "الرياض", ["jeddah"] = "جدة", ["dammam"] = "الدمام", ["makkah"] = "مكة المكرمة", ["madinah"] = "المدينة المنورة",
    };

    // Riyadh figures from the client's workbook (Jul/Aug actual, Sep forecast).
    private static RawSupplyDemandGapCity Riyadh() => new()
    {
        CityId = "riyadh",
        VacancyAssumptionPct = 10,
        Annual =
        {
            new RawGapAnnualRow { Year = 2026, DemandExp = 2530424, SupplyExp = 2340870, GapExp = -189554 },
            new RawGapAnnualRow { Year = 2025, DemandExp = 2441532, SupplyExp = 2291204, SupplyAct = 2301878, GapExp = -150328, GapAct = -139654 },
        },
        Monthly =
        {
            new RawGapMonthlyRow { Period = "2026-07", DemandExp = 2493386, SupplyExp = 2320176, SupplyAct = 2360234, GapExp = -173210, GapAct = -133152 },
            new RawGapMonthlyRow { Period = "2026-08", DemandExp = 2500794, SupplyExp = 2324315, SupplyAct = 2369796, GapExp = -176479, GapAct = -130998 },
            new RawGapMonthlyRow { Period = "2026-09", DemandExp = 2508201, SupplyExp = 2328454, GapExp = -179748 },
        },
    };

    [Fact]
    public void Kpis_use_the_first_two_years_and_the_latest_actual_month()
    {
        var city = Assert.Single(SupplyDemandGapProjection.Build(new[] { Riyadh() }, Names));

        Assert.Equal("الرياض", city.CityName);
        Assert.Equal(10.0, city.VacancyAssumptionPct);
        Assert.Equal(2025, city.Kpis.FirstYear!.Year);
        Assert.Equal(-150328L, city.Kpis.FirstYear.Gap);
        Assert.Equal((long?)-139654, city.Kpis.FirstYear.ActualGap);
        Assert.Equal(2026, city.Kpis.SecondYear!.Year);
        Assert.Equal(-189554L, city.Kpis.SecondYear.Gap);
        Assert.Null(city.Kpis.SecondYear.ActualGap);
        Assert.Equal("2026-08", city.LastActualPeriod);
        Assert.Equal("أغسطس 2026", city.Kpis.MonthExpected!.Label);
        Assert.Equal(-176479L, city.Kpis.MonthExpected.Gap);
        Assert.Equal(-130998L, city.Kpis.MonthActual!.Gap);
    }

    [Fact]
    public void Annual_series_is_ordered_and_uses_actuals_where_the_year_has_them()
    {
        var city = Assert.Single(SupplyDemandGapProjection.Build(new[] { Riyadh() }, Names));

        Assert.Equal(new[] { 2025, 2026 }, city.Annual.Select(a => a.Year));
        Assert.True(city.Annual[0].IsActual);
        Assert.Equal(2301878L, city.Annual[0].Supply);
        Assert.Equal(2441532L, city.Annual[0].Demand);
        Assert.Equal(-139654L, city.Annual[0].Gap);
        Assert.Equal(-150328L, city.Annual[0].ExpectedGap);
        Assert.False(city.Annual[1].IsActual);
        Assert.Equal(2340870L, city.Annual[1].Supply);
        Assert.Equal(-189554L, city.Annual[1].Gap);
        Assert.Equal(-189554L, city.Annual[1].ExpectedGap);
    }

    [Fact]
    public void Monthly_series_uses_actuals_up_to_the_last_actual_month_then_forecasts()
    {
        var monthly = Assert.Single(SupplyDemandGapProjection.Build(new[] { Riyadh() }, Names)).Monthly;

        Assert.Equal(new[] { true, true, false }, monthly.Select(m => m.IsActual));
        Assert.Equal(2369796L, monthly[1].Supply);
        Assert.Equal(-130998L, monthly[1].Gap);
        Assert.Equal(-176479L, monthly[1].ExpectedGap);
        Assert.Equal(2328454L, monthly[2].Supply);
        Assert.Equal(-179748L, monthly[2].Gap);
        Assert.Equal(2508201L, monthly[2].Demand);
        Assert.Equal("سبتمبر 2026", monthly[2].Label);
    }

    [Fact]
    public void No_actuals_gives_null_month_kpis()
    {
        var raw = Riyadh();
        raw.Monthly = raw.Monthly.Select(m => m with { SupplyAct = null, GapAct = null }).ToList();

        var city = Assert.Single(SupplyDemandGapProjection.Build(new[] { raw }, Names));

        Assert.Null(city.LastActualPeriod);
        Assert.Null(city.Kpis.MonthExpected);
        Assert.Null(city.Kpis.MonthActual);
        Assert.All(city.Monthly, m => Assert.False(m.IsActual));
    }

    [Fact]
    public void Cities_follow_the_fixed_order_and_unknown_ids_are_dropped()
    {
        var gap = new[]
        {
            new RawSupplyDemandGapCity { CityId = "madinah", Annual = { new RawGapAnnualRow { Year = 2025 } } },
            new RawSupplyDemandGapCity { CityId = "tabuk", Annual = { new RawGapAnnualRow { Year = 2025 } } },
            Riyadh(),
        };

        var cities = SupplyDemandGapProjection.Build(gap, Names);

        Assert.Equal(new[] { "riyadh", "madinah" }, cities.Select(c => c.CityId));
    }
}
