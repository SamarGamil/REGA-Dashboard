using System.Text.Encodings.Web;
using System.Text.Json;
using AqarInsights.Api.Models.RentalControls.Raw;
using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Models.RentalControls.Tables;
using AqarInsights.Api.Services.RentalControls.Tables;

namespace AqarInsights.Api.Tests.RentalControls.Tables;

internal static class TableTestData
{
    public static readonly ReleaseRow LiveRow = new(ReleaseIds.Live, ReleaseKinds.Live, "", "", "", default, null);

    private static readonly JsonSerializerOptions Options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string Json(object? value) => JsonSerializer.Serialize(value, value?.GetType() ?? typeof(object), Options);

    // One city with every kind of data the tables hold.
    public static MappedRelease Small(double? roi = 0.05, bool withMedia = true, long? supplyAct = 120)
    {
        var city = new CityRecord
        {
            Id = "riyadh", Name = "الرياض", Cagr = 0.09, Path = "مرتفع", Vacancy = 0.07, InvestmentReturn = roi,
            RentalIncrease = -0.01, RentToIncome = 0.15, Burden = 0.35, Status = "", AssessmentLine1 = "a", AssessmentLine2 = "b",
        };
        var raw = new CityRawData
        {
            CityId = "riyadh",
            Bullets = ["نقطة"],
            MovementRows = [new RawMovementRow { Indicator = "التضخم العام (CPI)", Level = "مرتفع", Change = "+2.2%", Assessment = "متابعة" }],
            Media = withMedia
                ? new List<RawMediaMonitoring>
                {
                    new()
                    {
                        Period = "2026-06", Available = true, OverallSentiment = "محايد", PositivePct = 30, NeutralPct = 50,
                        NegativePct = 20, DiscussionSharePct = 12.5, Topics = ["موضوع"], PositiveImpressions = ["+"],
                        NeutralImpressions = [], NegativeImpressions = ["-"],
                    },
                }
                : null,
            Series =
            [
                new CitySeriesPoint { Series = SeriesTypes.RentIndex, Period = "2026-06", Key = "الرقم القياسي", Value = 104.3, Change = "+1.2%" },
                new CitySeriesPoint { Series = SeriesTypes.Rentals, Period = "2026-Q2", Key = "شقة", Value = 1200 },
            ],
        };
        var gap = new RawSupplyDemandGapCity
        {
            CityId = "riyadh",
            VacancyAssumptionPct = 0.1,
            Annual = [new RawGapAnnualRow { Year = 2026, DemandExp = 1000, SupplyExp = 900, SupplyAct = supplyAct, GapExp = -100, GapAct = supplyAct is null ? null : (long?)-80 }],
            Monthly = [new RawGapMonthlyRow { Period = "2026-06", DemandExp = 80, SupplyExp = 75, GapExp = -5 }],
        };
        var content = new CityReportContent
        {
            CityId = "riyadh",
            ExecutiveBullets = ["تنفيذي"],
            MarketBullets = ["سوق"],
            ProductTable = [new CityReportProductRow { Product = "شقة", RentalDeals = "+5%", SalesDeals = "-3%", FisherIndex = "101" }],
            CityAveragesLine = "متوسط",
            AssessmentAxes = [new CityReportAxis { Axis = "الطلب", Status = "مرتفع" }],
            WatchList = ["مراقبة"],
        };
        return new MappedRelease(
            [city],
            [
                new IndicatorSeriesPoint { Indicator = "roi", CityId = "riyadh", Year = 2026, Period = 2, Value = 5.1, UnitType = "شقة" },
                new IndicatorSeriesPoint { Indicator = "vacancy", CityId = "riyadh", Year = 2026, Period = 6, Value = 7 },
            ],
            [gap],
            new Dictionary<string, CityRawData> { ["riyadh"] = raw },
            new Dictionary<string, CityReportContent>(StringComparer.OrdinalIgnoreCase) { ["riyadh"] = content });
    }
}
