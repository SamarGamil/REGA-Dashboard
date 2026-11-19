using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Services.RentalControls;

namespace AqarInsights.Api.Tests.RentalControls;

public class IndicatorReshaperTests
{
    private static OverviewData Sample() => new()
    {
        Cities =
        {
            new CityData { Id = "riyadh", Name = "الرياض", HousingBurden = "35%", ActualRentalGrowth = "+9%", InvestmentReturn = "5%", VacancyRate = "7%", RentToIncome = "15%" },
            new CityData { Id = "dammam", Name = "الدمام", HousingBurden = "24%", ActualRentalGrowth = "+3%", InvestmentReturn = "5%", VacancyRate = "7%", RentToIncome = "12%" },
        },
    };

    [Fact]
    public void Builds_housing_burden_panel_with_status()
    {
        var panels = IndicatorReshaper.CurrentPanels(Sample());
        var burden = panels.Single(p => p.Id == "housing-burden");
        var riyadh = burden.Cities.Single(c => c.CityId == "riyadh");
        Assert.Equal(35, riyadh.Value);
        Assert.Equal("متجاوز", riyadh.Status);
        var dammam = burden.Cities.Single(c => c.CityId == "dammam");
        Assert.Equal("ضمن الحد", dammam.Status);
    }

    [Fact]
    public void Negative_value_is_shown_not_treated_as_missing()
    {
        var overview = new OverviewData
        {
            Cities =
            {
                new CityData { Id = "riyadh", Name = "الرياض", ActualRentalGrowth = "-1%", HousingBurden = "—" },
                new CityData { Id = "dammam", Name = "الدمام", ActualRentalGrowth = "+1%", HousingBurden = "24%" },
            },
        };
        var panels = IndicatorReshaper.CurrentPanels(overview);

        var riyadh = panels.Single(p => p.Id == "rental-increase").Cities.Single(c => c.CityId == "riyadh");
        Assert.Equal(-1, riyadh.Value);
        Assert.Equal("-1%", riyadh.DisplayValue);
        Assert.NotEqual("—", riyadh.Status);

        var missing = panels.Single(p => p.Id == "housing-burden").Cities.Single(c => c.CityId == "riyadh");
        Assert.Equal(("—", "—"), (missing.DisplayValue, missing.Status));
    }

    [Fact]
    public void Sorts_by_descending_value()
    {
        var burden = IndicatorReshaper.CurrentPanels(Sample()).Single(p => p.Id == "housing-burden");
        Assert.Equal("riyadh", burden.Cities.First().CityId);
        Assert.Equal(1, burden.Cities.First().SortOrder);
    }

    [Fact]
    public void RentToIncome_and_burden_panels_use_configurable_limit()
    {
        // Dammam burden = 24: ضمن الحد at the default 30, but متجاوز once the limit drops to 20.
        var panels = IndicatorReshaper.CurrentPanels(Sample(), new RentalPolicyOptions { RentBurdenLimitPct = 20 });

        var burden = panels.Single(p => p.Id == "housing-burden");
        Assert.Contains("20%", burden.ThresholdSummary);
        Assert.Equal("متجاوز", burden.Cities.Single(c => c.CityId == "dammam").Status);

        var rti = panels.Single(p => p.Id == "rent-to-income");
        Assert.Contains("20%", rti.ThresholdRule);
    }

    [Fact]
    public void Vacancy_and_yield_panels_use_configurable_limits()
    {
        // Riyadh vacancy = 7, yield = 5.
        // Default: vacancy ضمن (>=5), yield دون (<6). Raise floors to flip both.
        var policy = new RentalPolicyOptions { VacancyLimitPct = 8, YieldActivationPct = 5 };
        var panels = IndicatorReshaper.CurrentPanels(Sample(), policy);

        var vacancy = panels.Single(p => p.Id == "vacancy");
        Assert.Contains("8%", vacancy.ThresholdSummary);
        Assert.Equal("دون الحد", vacancy.Cities.Single(c => c.CityId == "riyadh").Status); // 7 < 8

        var yield = panels.Single(p => p.Id == "investment-return");
        Assert.Contains("5%", yield.ThresholdSummary);
        Assert.Equal("ضمن الحد", yield.Cities.Single(c => c.CityId == "riyadh").Status); // 5 >= 5
    }

    [Fact]
    public void RentalIncrease_panel_uses_configurable_bands()
    {
        // Riyadh growth = +9 → متابعة by default (7–9). Drop the watch ceiling to 8 → 9 ≥ 8 → متجاوز.
        var policy = new RentalPolicyOptions { GrowthWithinMax = 6, GrowthWatchMax = 8 };
        var panels = IndicatorReshaper.CurrentPanels(Sample(), policy);

        var inc = panels.Single(p => p.Id == "rental-increase");
        Assert.Contains("8%", inc.ThresholdRule);
        Assert.Equal("متجاوز", inc.Cities.Single(c => c.CityId == "riyadh").Status);
    }
}
