using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Raw;
using AqarInsights.Api.Services.RentalControls.Projections;

namespace AqarInsights.Api.Tests.RentalControls;

public class IndicatorsProjectionTests
{
    [Fact]
    public void PublicVacancy_shows_its_latest_month_only_as_the_period_label()
    {
        var overview = new OverviewData
        {
            Cities = new List<CityData>
            {
                new() { Id = "riyadh", Name = "الرياض" },
                new() { Id = "jeddah", Name = "جدة" },
            },
        };
        var series = new List<IndicatorSeriesPoint>
        {
            new() { Indicator = "publicVacancy", CityId = "riyadh", Year = 2026, Period = 8, Value = 1.4 },
            new() { Indicator = "publicVacancy", CityId = "jeddah", Year = 2026, Period = 9, Value = 2.1 },
            new() { Indicator = "publicVacancy", CityId = "jeddah", Year = 2025, Period = 12, Value = 2.0 },
        };

        var result = IndicatorsProjection.Build(overview, series, new List<RawSupplyDemandGapCity>(), new Dictionary<string, CityRawData>());

        var panel = result.CurrentPeriodIndicators.Single(p => p.Id == "public-vacancy");
        Assert.Equal("", panel.ThresholdSummary);
        Assert.Equal("سبتمبر 2026", panel.PeriodLabel);
        // Riyadh's latest value is August's: it is not shown under September.
        Assert.Equal("—", panel.Cities.Single(c => c.CityId == "riyadh").DisplayValue);
    }

    [Fact]
    public void Build_ReturnsCompleteIndicatorsData()
    {
        // Arrange: minimal overview with 1 city
        var overview = new OverviewData
        {
            Cities = new List<CityData>
            {
                new()
                {
                    Id = "riyadh",
                    Name = "الرياض",
                    ActualRentalGrowth = "8%",
                    VacancyRate = "3%",
                    InvestmentReturn = "7%",
                    RentToIncome = "25%",
                    HousingBurden = "35%",
                },
            },
        };

        // Determinants series now only feeds public-vacancy (panel) and rental-vacancy (heatmap).
        var series = new List<IndicatorSeriesPoint>
        {
            new() { Indicator = "publicVacancy", CityId = "riyadh", Year = 2024, Period = 3, Value = 4.2 },
            new() { Indicator = "rentalChange", CityId = "riyadh", Year = 2024, Period = 2, Value = 8.5 },
        };

        // Burden now comes from the per-city sheet (latest quarter per year wins).
        var cityRaw = new Dictionary<string, CityRawData>
        {
            ["riyadh"] = new CityRawData
            {
                CityId = "riyadh",
                BurdenSeries = new()
                {
                    new() { Period = "2024-Q1", Values = new() { ["العبء"] = 32.5 } },
                    new() { Period = "2024-Q2", Values = new() { ["العبء"] = 35.0 } }, // latest 2024 wins
                    new() { Period = "2023-Q4", Values = new() { ["العبء"] = 30.0 } },
                },
            },
        };

        // Act
        var result = IndicatorsProjection.Build(overview, series, new List<RawSupplyDemandGapCity>(), cityRaw);

        // Assert: 6 current panels (5 from IndicatorReshaper + public-vacancy)
        Assert.Equal(6, result.CurrentPeriodIndicators.Count);
        Assert.Contains(result.CurrentPeriodIndicators, p => p.Id == "rental-increase");
        Assert.Contains(result.CurrentPeriodIndicators, p => p.Id == "vacancy");
        Assert.Contains(result.CurrentPeriodIndicators, p => p.Id == "investment-return");
        Assert.Contains(result.CurrentPeriodIndicators, p => p.Id == "rent-to-income");
        Assert.Contains(result.CurrentPeriodIndicators, p => p.Id == "housing-burden");
        Assert.Contains(result.CurrentPeriodIndicators, p => p.Id == "public-vacancy");

        // public-vacancy panel should have the latest publicVacancy data
        var publicVacancy = result.CurrentPeriodIndicators.Single(p => p.Id == "public-vacancy");
        Assert.Single(publicVacancy.Cities);
        Assert.Equal("الرياض", publicVacancy.Cities[0].CityName);
        Assert.Equal("4.2%", publicVacancy.Cities[0].DisplayValue);

        // Historical indicators: should have 5 (one for each historical config)
        Assert.Equal(5, result.HistoricalIndicators.Count);
        Assert.Contains(result.HistoricalIndicators, h => h.Id == "rental-increase-hist");
        Assert.Contains(result.HistoricalIndicators, h => h.Id == "vacancy-hist");
        Assert.Contains(result.HistoricalIndicators, h => h.Id == "investment-return-hist");
        Assert.Contains(result.HistoricalIndicators, h => h.Id == "rent-to-income-hist");
        Assert.Contains(result.HistoricalIndicators, h => h.Id == "housing-burden-hist");

        // housing-burden-hist should pick the latest period (period=2, value=35.0) for 2024
        var burdenHist = result.HistoricalIndicators.Single(h => h.Id == "housing-burden-hist");
        Assert.Single(burdenHist.Cities);
        var burdenCity = burdenHist.Cities[0];
        Assert.Equal("الرياض", burdenCity.CityName);
        Assert.Equal("35%", burdenCity.DisplayValues["2024"]);
        Assert.Equal("30%", burdenCity.DisplayValues["2023"]);
        Assert.Equal("—", burdenCity.DisplayValues["2025"]); // missing year
        Assert.Equal("—", burdenCity.DisplayValues["2026"]);

        Assert.Empty(result.SupplyDemandGap);

        // CurrentToHistoricalMap
        Assert.Equal(5, result.CurrentToHistoricalMap.Count);
        Assert.Equal("rental-increase-hist", result.CurrentToHistoricalMap["rental-increase"]);
        Assert.Equal("vacancy-hist", result.CurrentToHistoricalMap["vacancy"]);
        Assert.Equal("investment-return-hist", result.CurrentToHistoricalMap["investment-return"]);
        Assert.Equal("rent-to-income-hist", result.CurrentToHistoricalMap["rent-to-income"]);
        Assert.Equal("housing-burden-hist", result.CurrentToHistoricalMap["housing-burden"]);
    }

    [Fact]
    public void Build_SourcesRentToIncomeRoiAndRentalChange_FromCitySheets()
    {
        var overview = new OverviewData
        {
            Cities = new List<CityData>
            {
                new()
                {
                    Id = "riyadh", Name = "الرياض",
                    ActualRentalGrowth = "8%", VacancyRate = "3%", InvestmentReturn = "7%",
                    RentToIncome = "25%", HousingBurden = "35%",
                },
            },
        };

        // Determinants series no longer feeds the heatmaps; the saudi-filtered rentToIncome
        // point must be IGNORED (rent-to-income now comes from the per-city sheet).
        var series = new List<IndicatorSeriesPoint>
        {
            new() { Indicator = "rentToIncome", CityId = "riyadh", Year = 2023, Period = 4, Value = 7.7, Nationality = "saudi" },
        };

        var cityRaw = new Dictionary<string, CityRawData>
        {
            ["riyadh"] = new CityRawData
            {
                CityId = "riyadh",
                AffordabilitySeries = new()
                {
                    new() { Period = "2022-Q1", Values = new() { ["النسبة"] = 11.59 } }, // outside window
                    new() { Period = "2023-Q1", Values = new() { ["النسبة"] = 12.73 } },
                    new() { Period = "2023-Q4", Values = new() { ["النسبة"] = 13.69 } }, // latest 2023 wins
                    new() { Period = "2024-Q2", Values = new() { ["النسبة"] = 15.07 } },
                },
                RoiSeries = new()
                {
                    new() { Period = "2024-Q3", Values = new() { ["العائد"] = 4.9 } },
                },
                IncreaseSeries = new()
                {
                    new() { Period = "2024-Q1", Values = new() { ["القيمة"] = 9 } },
                },
                BurdenSeries = new()
                {
                    new() { Period = "2024-Q2", Values = new() { ["العبء"] = 35.0 } },
                },
            },
        };

        var result = IndicatorsProjection.Build(overview, series, new List<RawSupplyDemandGapCity>(), cityRaw);

        // rent-to-income-hist from affordabilitySeries, NOT the saudi determinants value
        var rti = result.HistoricalIndicators.Single(h => h.Id == "rent-to-income-hist");
        var rtiRow = rti.Cities.Single(c => c.CityName == "الرياض");
        Assert.Equal("13.69%", rtiRow.DisplayValues["2023"]);
        Assert.Equal("15.07%", rtiRow.DisplayValues["2024"]);
        Assert.Equal("—", rtiRow.DisplayValues["2025"]);
        Assert.NotEqual(7.7, rtiRow.Values["2023"]);

        // investment-return-hist from roiSeries
        var roi = result.HistoricalIndicators.Single(h => h.Id == "investment-return-hist");
        Assert.Equal("4.9%", roi.Cities.Single().DisplayValues["2024"]);

        // rental-increase-hist from increaseSeries (integer formats without decimals)
        var inc = result.HistoricalIndicators.Single(h => h.Id == "rental-increase-hist");
        Assert.Equal("9%", inc.Cities.Single().DisplayValues["2024"]);

        // burden now sourced from the per-city BurdenSeries
        var burden = result.HistoricalIndicators.Single(h => h.Id == "housing-burden-hist");
        Assert.Equal("35%", burden.Cities.Single().DisplayValues["2024"]);
    }

    [Fact]
    public void Build_SourcesVacancyHist_FromDeterminantsSeries_LatestMonthPerYear()
    {
        var overview = new OverviewData
        {
            Cities = new List<CityData>
            {
                new()
                {
                    Id = "riyadh", Name = "الرياض",
                    ActualRentalGrowth = "8%", VacancyRate = "3%", InvestmentReturn = "7%",
                    RentToIncome = "25%", HousingBurden = "35%",
                },
            },
        };

        // Rental-vacancy series (whole-percent values), monthly across 2025 and 2026.
        var series = new List<IndicatorSeriesPoint>
        {
            new() { Indicator = "vacancy", CityId = "riyadh", Year = 2025, Period = 4, Value = 7 },
            new() { Indicator = "vacancy", CityId = "riyadh", Year = 2025, Period = 12, Value = 8 }, // latest 2025 wins
            new() { Indicator = "vacancy", CityId = "riyadh", Year = 2026, Period = 6, Value = 7 },
        };

        var result = IndicatorsProjection.Build(overview, series, new List<RawSupplyDemandGapCity>(), new Dictionary<string, CityRawData>());

        var vac = result.HistoricalIndicators.Single(h => h.Id == "vacancy-hist");
        var row = vac.Cities.Single(c => c.CityName == "الرياض");
        Assert.Equal("—", row.DisplayValues["2023"]);
        Assert.Equal("—", row.DisplayValues["2024"]);
        Assert.Equal("8%", row.DisplayValues["2025"]);
        Assert.Equal("7%", row.DisplayValues["2026"]);
    }
}