using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Projections;
using AqarInsights.Api.Services.RentalControls.Reports;
using AqarInsights.Api.Tests.RentalControls.Reports;

namespace AqarInsights.Api.Tests.RentalControls.Snapshots;

// Every reportable period: the national report prints, for each city, the same figures as that city's own report
// (client review: a figure shown two ways — 5% vs 5.3%, +4% vs +4.0% — reads as a contradiction).
public class NationalCityConsistencyTests
{
    public static IEnumerable<object[]> Periods()
    {
        var periods = RealReportData.Periods.GetPeriods();
        return periods.Monthly.Concat(periods.Quarterly).Select(p => new object[] { p.Id });
    }

    [Theory, MemberData(nameof(Periods))]
    public void National_rows_match_the_city_reports(string period)
    {
        var data = RealReportData.Periods.ResolveAsync(period).GetAwaiter().GetResult()!;
        var agg = data.Aggregation;
        var national = NationalReportModelBuilder.Build(agg.GetMarketAnalysis(), new DateOnly(2026, 10, 29), new NationalReportOptions(),
            data.Label, data.IsMonthly, NationalReportService.ContextFor(data));
        var panels = PeriodValueMask.Apply(agg.GetIndicators(), data.Scope).CurrentPeriodIndicators;
        var mismatches = new List<string>();

        foreach (var overviewCity in agg.GetOverview().Cities)
        {
            // The city report exactly as CityReportService builds it for the period.
            var city = PeriodValueMask.Apply(agg.GetCity(overviewCity.Id)!, data.Scope);
            var facts = CityReportFactsBuilder.Build(overviewCity, city, panels);
            var model = CityReportModelBuilder.Build(city, facts,
                CityReportModelBuilder.ForPeriod(data.Content.Get(overviewCity.Id)!, data.Scope, MonthlyCpiStatement.ForView(city, data.Scope)),
                new DateOnly(2026, 10, 29), data.Label, data.IsMonthly);
            var row = national.Rows.Single(r => r.CityName == overviewCity.Name);

            var cityYield = model.KpiCards.Single(k => k.Label.StartsWith("العائد الإيجاري")).ValueText;
            var cityRent = model.IndicatorRows.Single(r => r.Indicator.StartsWith("الإيجار الفعلي للمسكن CPI")).Value;
            var cityBurden = model.IndicatorRows.Single(r => r.Indicator == "العبء السكني لذوي الدخل المحدود").Value;
            Check(overviewCity.Id, "yield", row.YieldText, cityYield);
            Check(overviewCity.Id, "yield (supply table)", cityYield, model.SupplyInvestment.Single(s => s.Label.StartsWith("العائد الإيجاري")).Value);
            Check(overviewCity.Id, "rent CPI", row.ActualRentText, cityRent);
            Check(overviewCity.Id, "burden", row.BurdenText, cityBurden);

            // Client review (national, August 2026, note 1): one figure, one colour — the national cell takes the colour
            // the city report gives the figure (burden 35% red «متجاوز», actual rent red «ارتفاع»); a missing figure grey.
            string ColorOf(string value, string cityColor) => value == "—" ? "#6B7280" : cityColor;
            Check(overviewCity.Id, "burden colour", row.BurdenColorHex,
                ColorOf(cityBurden, model.KpiCards.Single(k => k.Label == "العبء السكني لذوي الدخل المحدود").ColorHex));
            Check(overviewCity.Id, "rent CPI colour", row.RentColorHex,
                ColorOf(cityRent, model.KpiCards.Single(k => k.Label.StartsWith("الإيجار الفعلي للمسكن CPI")).ColorHex));
            Check(overviewCity.Id, "yield colour", row.YieldColorHex,
                ColorOf(cityYield, model.KpiCards.Single(k => k.Label.StartsWith("العائد الإيجاري")).ColorHex));
            foreach (var (indicator, text, color) in new[]
            {
                ("الزيادة الإيجارية", row.RentalIncreaseText, row.RentalIncreaseColorHex),
                ("نسبة الإيجار إلى الدخل", row.RentToIncomeText, row.RentToIncomeColorHex),
            })
            {
                var cityRow = model.IndicatorRows.Single(r => r.Indicator == indicator);
                Check(overviewCity.Id, indicator, text, cityRow.Value);
                Check(overviewCity.Id, indicator + " colour", color, ColorOf(cityRow.Value, cityRow.StatusColorHex));
            }
        }

        // KPI cards name a city and quote its figure as the city report does.
        foreach (var card in national.KpiCards.Where(k => k.CityName != "—"))
        {
            var city = agg.GetOverview().Cities.Single(c => c.Name == card.CityName);
            var expected = card.Label switch
            {
                "أدنى عائد إيجاري" => city.InvestmentReturn,
                "أعلى معدل نمو إيجاري مركب" => city.Cagr,
                "أعلى عبء سكني لذوي الدخل المحدود" => city.HousingBurden,
                _ => national.Rows.Single(r => r.CityName == card.CityName).ActualRentText,
            };
            Check(card.CityName, card.Label, card.ValueText, expected);
        }

        Assert.True(mismatches.Count == 0, $"{period}:\n" + string.Join("\n", mismatches));

        void Check(string city, string what, string national, string cityReport)
        {
            if (national != cityReport) mismatches.Add($"{city} {what}: national \"{national}\" vs city \"{cityReport}\"");
        }
    }

    // The summary's "below the yield reference" count is judged on the displayed decimals (Buraidah 5.8%).
    [Fact]
    public void National_summary_counts_the_yields_below_the_reference_with_their_decimals()
    {
        var agg = RealReportData.Aggregation;
        var national = NationalReportModelBuilder.Build(agg.GetMarketAnalysis(), new DateOnly(2026, 10, 29), new NationalReportOptions());
        var below = agg.GetOverview().Cities.Count(c => double.TryParse(c.InvestmentReturn.TrimEnd('%'), out var v) && v < 6);

        Assert.Equal(10, below);
        Assert.Contains($"يقل العائد الإيجاري عن الحد المرجعي للمراقبة (6%) في {below} مدن", national.SummaryLines[1]);
        Assert.Equal(("4.1%", "المدينة المنورة"), (national.KpiCards.Single(k => k.Label == "أدنى عائد إيجاري").ValueText,
            national.KpiCards.Single(k => k.Label == "أدنى عائد إيجاري").CityName));
    }
}
