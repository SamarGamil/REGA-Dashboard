using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Projections;

namespace AqarInsights.Api.Services.RentalControls.Reports;

public static class CityReportFactsBuilder
{
    private const string Dash = "—";
    private const string ReferencePrefix = "الحد المرجعي: ";
    private const string RentCpiRow = "الإيجار الفعلي للمسكن (CPI)";
    private const string InflationRow = "التضخم العام (CPI)";
    private const string RentColumn = "الإيجار الفعلي للمسكن";
    private const string InflationColumn = "التضخم العام";

    public static CityReportFacts Build(CityData overviewCity, CityAnalysisData city, IReadOnlyList<IndicatorPanel> currentPanels)
    {
        var tab = city.MarketAnalysisTabs.FirstOrDefault(t => t.Id == "rent-inflation");

        // Status is the policy status against the reference shown next to it (ضمن الحد / متابعة / دون الحد / متجاوز).
        CityReportIndicatorFact Panel(string label, string value, string panelId) => new()
        {
            Label = label,
            Value = Or(value),
            Reference = PanelReference(currentPanels, panelId),
            Status = PanelStatus(currentPanels, panelId, overviewCity.Id),
            Frequency = ReportFrequency.Label(PanelPeriods.FrequencyOf(panelId)),
            Period = ReportPeriodText.Word(currentPanels.FirstOrDefault(p => p.Id == panelId)?.PeriodLabel),
        };

        CityReportIndicatorFact Cpi(string label, string row, string column)
        {
            var movement = city.MarketMovementTable.FirstOrDefault(m => m.Indicator == row);
            var value = Or(movement?.Change);
            // The general-inflation row shows the platform's monthly-series value for the month, not the movement panel's rounded one.
            if (row == InflationRow && LastFilledValue(tab, column) is { } seriesValue)
                value = ArabicNumber.FirstNumber(seriesValue) > 0 && !seriesValue.StartsWith('+') ? $"+{seriesValue}" : seriesValue;
            // A dashed value (no figure for the period's month) has no month either.
            var month = value == Dash ? "" : LastFilledMonth(tab, column);
            return new()
            {
                Label = label,
                Value = value,
                Status = Or(movement?.Assessment),
                Month = month,
                Period = ReportPeriodText.Word(month),
                Frequency = ReportFrequency.Monthly,
            };
        }

        return new CityReportFacts
        {
            OperationalStatus = overviewCity.OperationalStatus,
            Classification = overviewCity.Classification,
            ClassificationLabel = overviewCity.ClassificationLabel,
            Recommendation = overviewCity.Recommendation,
            Cagr = new()
            {
                Label = "معدل النمو الإيجاري المركب",
                Value = Or(overviewCity.Cagr),
                Reference = Or(CagrBand(overviewCity.ClassificationLabel)),
                Status = Or(overviewCity.Classification),
            },
            RentalIncrease = Panel("الزيادة الإيجارية", overviewCity.ActualRentalGrowth, "rental-increase"),
            RentToIncome = Panel("نسبة الإيجار إلى الدخل", overviewCity.RentToIncome, "rent-to-income"),
            HousingBurden = Panel("العبء السكني لذوي الدخل المحدود", overviewCity.HousingBurden, "housing-burden"),
            Vacancy = Panel("الشواغر الإيجارية", VacancyCard(city) ?? overviewCity.VacancyRate, "vacancy"),
            InvestmentReturn = Panel("العائد الإيجاري", overviewCity.InvestmentReturn, "investment-return"),
            ActualRentCpi = Cpi("الإيجار الفعلي للمسكن CPI", RentCpiRow, RentColumn),
            GeneralInflation = Cpi("التضخم العام (CPI)", InflationRow, InflationColumn),
        };
    }

    private static string Or(string? value) => string.IsNullOrWhiteSpace(value) ? Dash : value;

    // The month of the city's latest actual-rent (CPI) figure, as the platform shows it ("سبتمبر 2026").
    public static string RentCpiMonth(CityAnalysisData city)
        => LastFilledMonth(city.MarketAnalysisTabs.FirstOrDefault(t => t.Id == "rent-inflation"), RentColumn);

    // The city's monthly actual-rent figures that have a value, oldest first.
    public static List<double> RentCpiValues(CityAnalysisData city)
    {
        var tab = city.MarketAnalysisTabs.FirstOrDefault(t => t.Id == "rent-inflation");
        var index = tab?.TableColumns.IndexOf(RentColumn) ?? -1;
        if (tab is null || index < 0) return new();
        return tab.TableRows.Where(r => r.Count > index)
            .Select(r => ArabicNumber.FirstNumber(r[index])).OfType<double>().ToList();
    }

    // The workbook classes the CAGR rounded to a whole percent (6 / 7-9 / 10+); the report shows one decimal, so its
    // reference states the exact cut-offs (the portal legend keeps the whole-number bands).
    private static string? CagrBand(string classificationLabel) => classificationLabel switch
    {
        "sustainable" => "6% فأقل",
        "elevated" => "7%–9%",
        "accelerated" => "10% فأكثر",
        _ => null,
    };

    // The city page's vacancy card: the series value for the period's month (or "—"), as the portal shows it.
    private static string? VacancyCard(CityAnalysisData city)
        => city.Kpis.FirstOrDefault(k => k.Label == "الشواغر الإيجارية")?.Value;

    private static string PanelReference(IReadOnlyList<IndicatorPanel> panels, string id)
    {
        var summary = panels.FirstOrDefault(p => p.Id == id)?.ThresholdSummary;
        if (string.IsNullOrWhiteSpace(summary)) return Dash;
        return summary.StartsWith(ReferencePrefix, StringComparison.Ordinal) ? summary[ReferencePrefix.Length..] : summary;
    }

    private static string PanelStatus(IReadOnlyList<IndicatorPanel> panels, string id, string cityId) =>
        Or(panels.FirstOrDefault(p => p.Id == id)?.Cities.FirstOrDefault(c => c.CityId == cityId)?.Status);

    private static string LastFilledMonth(MarketAnalysisTab? tab, string column)
    {
        if (tab is null) return "";
        var index = tab.TableColumns.IndexOf(column);
        if (index < 0) return "";
        var row = tab.TableRows.LastOrDefault(r => r.Count > index && !string.IsNullOrWhiteSpace(r[index]) && r[index] != Dash);
        return row is { Count: > 0 } ? row[0] : "";
    }

    // The cell text ("1.21%") of the column's latest filled row; null when the series has none.
    private static string? LastFilledValue(MarketAnalysisTab? tab, string column)
    {
        var index = tab?.TableColumns.IndexOf(column) ?? -1;
        if (tab is null || index < 0) return null;
        var row = tab.TableRows.LastOrDefault(r => r.Count > index && !string.IsNullOrWhiteSpace(r[index]) && r[index] != Dash);
        return row?[index].Trim();
    }
}
