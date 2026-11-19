using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls;
using AqarInsights.Api.Services.RentalControls.Reports;
using Xunit;

namespace AqarInsights.Api.Tests.RentalControls.Reports;

public class CityReportFactsBuilderTests
{
    private static CityData Overview() => new()
    {
        Id = "dammam", Name = "الدمام", Cagr = "2.4%", Classification = "نمو مستدام", ClassificationLabel = "sustainable",
        OperationalStatus = "يتطلب دراسة استباقية", InvestmentReturn = "5%", VacancyRate = "7%",
        ActualRentalGrowth = "+3%", RentToIncome = "12%", HousingBurden = "24%", Recommendation = "مراقبة - العائد 5%",
    };

    private static CityAnalysisData City(CityData o, params List<string>[] rentInflationRows) => new()
    {
        Kpis = CityAnalysisDeriver.Kpis(o),
        MarketMovementTable = new()
        {
            new() { Indicator = "الإيجار الفعلي للمسكن (CPI)", Change = "+5.3%", Assessment = "ارتفاع" },
            new() { Indicator = "التضخم العام (CPI)", Change = "+1.5%", Assessment = "مستقر" },
        },
        MarketAnalysisTabs = new()
        {
            new() { Id = "rent-inflation", TableColumns = new() { "الفترة", "الإيجار الفعلي للمسكن", "التضخم العام" }, TableRows = rentInflationRows.ToList() },
        },
    };

    private static List<IndicatorPanel> Panels(CityData o) => IndicatorReshaper.CurrentPanels(new OverviewData { Cities = { o } });

    private static (string, string, string, string) T(CityReportIndicatorFact f) => (f.Label, f.Value, f.Reference, f.Status);

    // Statuses are the policy status against the reference shown beside them (client review: "محايد" was ambiguous).
    [Fact]
    public void Maps_platform_values_policy_statuses_and_references()
    {
        var o = Overview();
        var f = CityReportFactsBuilder.Build(o, City(o), Panels(o));
        Assert.Equal("يتطلب دراسة استباقية", f.OperationalStatus);
        Assert.Equal("مراقبة - العائد 5%", f.Recommendation);
        Assert.Equal(("معدل النمو الإيجاري المركب", "2.4%", "6% فأقل", "نمو مستدام"), T(f.Cagr));
        Assert.Equal(("الزيادة الإيجارية", "+3%", "أكبر من 6%", "ضمن الحد"), T(f.RentalIncrease));
        Assert.Equal(("نسبة الإيجار إلى الدخل", "12%", "أكبر من 30%", "ضمن الحد"), T(f.RentToIncome));
        Assert.Equal(("العبء السكني لذوي الدخل المحدود", "24%", "أكبر من 30%", "ضمن الحد"), T(f.HousingBurden));
        Assert.Equal(("الشواغر الإيجارية", "7%", "أقل من 5%", "ضمن الحد"), T(f.Vacancy));
        Assert.Equal(("العائد الإيجاري", "5%", "أقل من 6%", "دون الحد"), T(f.InvestmentReturn));
    }

    // The workbook classes the CAGR rounded to a whole percent (6 / 7-9 / 10+), so a one-decimal CAGR needs the exact
    // cut-offs: 9.7% is "نمو متسارع" and 6.8% "نمو مرتفع".
    [Theory]
    [InlineData("6.8%", "نمو مرتفع", "elevated", "7%–9%")]
    [InlineData("9.7%", "نمو متسارع", "accelerated", "10% فأكثر")]
    public void Cagr_reference_is_the_exact_decimal_band_of_its_class(string cagr, string classification, string label, string reference)
    {
        var o = Overview();
        o.Cagr = cagr; o.Classification = classification; o.ClassificationLabel = label;
        var f = CityReportFactsBuilder.Build(o, City(o), Panels(o));
        Assert.Equal(("معدل النمو الإيجاري المركب", cagr, reference, classification), T(f.Cagr));
    }

    [Fact]
    public void Burden_above_the_reference_is_reported_as_exceeded()
    {
        var o = Overview();
        o.HousingBurden = "35%";
        var f = CityReportFactsBuilder.Build(o, City(o), Panels(o));
        Assert.Equal(("35%", "أكبر من 30%", "متجاوز"), (f.HousingBurden.Value, f.HousingBurden.Reference, f.HousingBurden.Status));
    }

    [Fact]
    public void Takes_cpi_values_from_movement_table_and_months_from_last_filled_rows()
    {
        var o = Overview();
        var f = CityReportFactsBuilder.Build(o, City(o,
            new() { "يونيو 2026", "5.35%", "1.21%" },
            new() { "يوليو 2026", "—", "1.4%" },
            new() { "أغسطس 2026", "—", "1.5%" }), Panels(o));
        Assert.Equal(("+5.3%", "ارتفاع", "يونيو 2026"), (f.ActualRentCpi.Value, f.ActualRentCpi.Status, f.ActualRentCpi.Month));
        Assert.Equal(("+1.5%", "مستقر", "أغسطس 2026"), (f.GeneralInflation.Value, f.GeneralInflation.Status, f.GeneralInflation.Month));
        // Client review: no "التغير السنوي"; the month is the indicator's period word (shown beside the name).
        Assert.Equal(("الإيجار الفعلي للمسكن CPI", "يونيو"), (f.ActualRentCpi.DisplayLabel, f.ActualRentCpi.Period));
        Assert.Equal(("التضخم العام (CPI)", "أغسطس"), (f.GeneralInflation.DisplayLabel, f.GeneralInflation.Period));
    }

    [Fact]
    public void Missing_sources_fall_back_to_dash_and_empty_month()
    {
        var f = CityReportFactsBuilder.Build(Overview(), new CityAnalysisData(), new List<IndicatorPanel>());
        Assert.Equal(("—", "—", "—", ""), (f.ActualRentCpi.Value, f.ActualRentCpi.Status, f.ActualRentCpi.Reference, f.ActualRentCpi.Month));
        Assert.Equal("الإيجار الفعلي للمسكن CPI", f.ActualRentCpi.DisplayLabel);
        Assert.Equal(("العائد الإيجاري", "5%", "—", "—"), T(f.InvestmentReturn));
    }
}
