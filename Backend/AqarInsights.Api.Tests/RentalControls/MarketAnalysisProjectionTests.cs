using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Raw;
using AqarInsights.Api.Services.RentalControls.Projections;
using Xunit;

namespace AqarInsights.Api.Tests.RentalControls;

public class MarketAnalysisProjectionTests
{
    private static (OverviewData, Dictionary<string, CityRawData>) Sample()
    {
        var overview = new OverviewData();
        overview.Cities.Add(new CityData
        {
            Id = "riyadh", Name = "الرياض", OperationalStatus = "الضوابط مفعلة", Classification = "نمو متسارع",
            Cagr = "9.7%", VacancyRate = "7%", InvestmentReturn = "5%", ActualRentalGrowth = "+9%",
            RentToIncome = "15%", HousingBurden = "35%",
        });
        overview.Cities.Add(new CityData
        {
            Id = "abha", Name = "أبها", OperationalStatus = "لا إجراء", Classification = "نمو مستدام",
            Cagr = "1%", VacancyRate = "8%", InvestmentReturn = "—", ActualRentalGrowth = "+5%",
            RentToIncome = "13%", HousingBurden = "34%",
        });

        var raw = new Dictionary<string, CityRawData>
        {
            ["riyadh"] = new CityRawData
            {
                CityId = "riyadh",
                MovementRows = new()
                {
                    new() { Indicator = "متوسط قيمة الصفقة الإيجارية (ريال/وحدة)", Change = "33,157 (-4%)" },
                    new() { Indicator = "متوسط قيمة الصفقة البيعية (ريال)", Change = "1,282,372 (-6%)" },
                    new() { Indicator = "الإيجار الفعلي للمسكن (CPI)", Change = "+5.9%" },
                    new() { Indicator = "التضخم العام (CPI)", Change = "+2.2%" },
                },
                RentalsSeries = new() { new() { Period = "2026-Q2", Values = new() { ["الإجمالي"] = 229746 } } },
                SalesSeries = new() { new() { Period = "2026-Q2", Values = new() { ["الإجمالي"] = 11705 } } },
                RentIndexSeries = new() { new() { Period = "2026-06", Values = new() { ["الرقم القياسي"] = 142.1 } } },
                RentIndexByTypeSeries = new() { new() { Period = "2026-Q2", Values = new() { ["شقة"] = 1.33, ["دور"] = 1.42, ["فيلا"] = 1.43 } } },
            },
            // abha: no raw → transaction/CPI fields null
        };
        return (overview, raw);
    }

    [Fact]
    public void Build_MapsRealFields_AndPlaceholdersForMissing()
    {
        var (overview, raw) = Sample();

        var result = MarketAnalysisProjection.Build(overview, raw);

        Assert.Equal(2, result.Cities.Count);
        var r = result.Cities.Single(c => c.Id == "riyadh");
        Assert.Equal("الرياض", r.Name);
        Assert.Equal("الضوابط مفعلة", r.OperationalStatus);
        Assert.Equal(9.7, r.Cagr);
        Assert.Equal(7, r.VacancyRate);
        Assert.Equal(5, r.InvestmentReturn);
        Assert.Equal(15, r.RentToIncome);
        Assert.Equal(35, r.HousingBurden);
        Assert.Equal(9, r.RentalIncrease);                        // from overview ActualRentalGrowth "+9%"
        Assert.Equal(229.7, r.RentalTransactionCount!.Value, 1);  // 229746 / 1000
        Assert.Equal(11.7, r.SalesTransactionCount!.Value, 1);
        Assert.Equal(33157, r.AvgRentalTransactionValue);
        Assert.Equal(1282372, r.AvgSalesTransactionValue);
        Assert.Equal(5.9, r.ActualRentalGrowth);                  // movement CPI change
        Assert.Equal(2.2, r.GeneralInflationCpi);
        Assert.Equal(142.1, r.ActualRentCpi);
        Assert.Equal(133, r.ApartmentRentIndex);                  // 1.33 * 100
        Assert.Equal(142, r.FloorRentIndex);
        Assert.Equal(143, r.VillaRentIndex);

        var a = result.Cities.Single(c => c.Id == "abha");
        Assert.Null(a.InvestmentReturn);                          // "—"
        Assert.Null(a.RentalTransactionCount);                    // no raw
        Assert.Null(a.AvgRentalTransactionValue);
        Assert.Null(a.ActualRentCpi);

        Assert.Equal(9, result.Indicators.Count);
        Assert.NotEmpty(result.Recommendations);
        Assert.Equal("الربع الثاني 2026", result.LastQuarter);
    }
}
