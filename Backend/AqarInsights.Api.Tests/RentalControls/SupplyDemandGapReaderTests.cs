using ClosedXML.Excel;
using AqarInsights.Api.Models;
using AqarInsights.Api.Services.RentalControls.Reading;

namespace AqarInsights.Api.Tests.RentalControls;

public class SupplyDemandGapReaderTests
{
    // Mirrors one sheet of the client's 10Gap.xlsx (Riyadh), trimmed to three monthly columns.
    // Annual block: header row 3 (B..F), years in column A from row 4, "dif" row ends it.
    // Monthly block: labels in column H, month numbers 1..3 in J3..L3, title "Monthly Demand 2026" in J2.
    private static void WriteCitySheet(IXLWorksheet ws, bool withAnnual = true)
    {
        ws.Cell("B1").Value = 0.1;
        ws.Cell("J2").Value = "Monthly Demand 2026";
        ws.Cell("J3").Value = 1; ws.Cell("K3").Value = 2; ws.Cell("L3").Value = 3;
        if (withAnnual)
        {
            foreach (var (col, head) in new[] { ("B", "Demand Exp"), ("C", "Supply Exp"), ("D", "Suppy Act"), ("E", "Gap Exp."), ("F", "Gap Act.") })
                ws.Cell($"{col}3").Value = head;
            ws.Cell("A4").Value = 2025; ws.Cell("B4").Value = 2441532.222; ws.Cell("C4").Value = 2291204;
            ws.Cell("D4").Value = 2301878; ws.Cell("E4").Value = -150328.222; ws.Cell("F4").Value = -139654.222;
            ws.Cell("A5").Value = 2026; ws.Cell("B5").Value = 2530424.444; ws.Cell("C5").Value = 2340870;
            ws.Cell("E5").Value = -189554.444;
            ws.Cell("A6").Value = "dif"; ws.Cell("B6").Value = 88892.2;
        }
        Row(ws, 4, "Monthly Demand", 7407.68, 2448939.907, 2456347.592, 2463755.277);
        Row(ws, 5, "Monthly Supply", 4138.83, 2295342.833, 2299481.666, 2303620.5);
        Row(ws, 6, "Gap Exp", null, -153597.074, -156865.925, -160134.777);
        Row(ws, 7, "Monthly Supply ACt", 2301878, 2308512, 2318581, null);
        Row(ws, 8, "Gap Act", null, -140427.907, -137766.592, null);
    }

    private static void Row(IXLWorksheet ws, int r, string label, double? i, double? j, double? k, double? l)
    {
        ws.Cell(r, 8).Value = label;
        var col = 9;
        foreach (var v in new[] { i, j, k, l })
        {
            if (v is { } d) ws.Cell(r, col).Value = d;
            col++;
        }
    }

    private static (List<AqarInsights.Api.Models.RentalControls.Raw.RawSupplyDemandGapCity> Cities, List<ConversionWarning> Warnings) ReadOne(string sheetName, bool withAnnual = true)
    {
        using var wb = new XLWorkbook();
        WriteCitySheet(wb.AddWorksheet(sheetName), withAnnual);
        var warnings = new List<ConversionWarning>();
        return (SupplyDemandGapReader.Read(wb, warnings), warnings);
    }

    [Fact]
    public void Reads_annual_rows_rounded_until_the_dif_row()
    {
        var (cities, warnings) = ReadOne("Riyadh ");

        var city = Assert.Single(cities);
        Assert.Equal("riyadh", city.CityId);
        Assert.Empty(warnings);
        Assert.Equal(2, city.Annual.Count);
        var y2025 = city.Annual[0];
        Assert.Equal(2025, y2025.Year);
        Assert.Equal(2441532L, y2025.DemandExp);
        Assert.Equal(2291204L, y2025.SupplyExp);
        Assert.Equal((long?)2301878, y2025.SupplyAct);
        Assert.Equal(-150328L, y2025.GapExp);
        Assert.Equal((long?)-139654, y2025.GapAct);
        var y2026 = city.Annual[1];
        Assert.Equal(-189554L, y2026.GapExp);
        Assert.Null(y2026.SupplyAct);
        Assert.Null(y2026.GapAct);
    }

    [Fact]
    public void A_value_that_excel_shows_as_x_point_5_rounds_up_despite_binary_noise()
    {
        // Jeddah's March demand is a formula whose double is 1411910.4999999995; Excel shows (and means) 1411910.5.
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Riyadh ");
        WriteCitySheet(ws);
        ws.Cell("J4").Value = 1411910.4999999995;
        ws.Cell("K4").Value = -824181.4999999999;

        var monthly = Assert.Single(SupplyDemandGapReader.Read(wb, new List<ConversionWarning>())).Monthly;

        Assert.Equal((1411911L, -824182L), (monthly[0].DemandExp, monthly[1].DemandExp));
    }

    [Fact]
    public void Reads_monthly_rows_with_expected_and_actual_values()
    {
        var (cities, _) = ReadOne("Riyadh ");

        var monthly = Assert.Single(cities).Monthly;
        Assert.Equal(new[] { "2026-01", "2026-02", "2026-03" }, monthly.Select(m => m.Period));
        Assert.Equal(2448940L, monthly[0].DemandExp);
        Assert.Equal(2295343L, monthly[0].SupplyExp);
        Assert.Equal((long?)2308512, monthly[0].SupplyAct);
        Assert.Equal(-153597L, monthly[0].GapExp);
        Assert.Equal((long?)-140428, monthly[0].GapAct);
        Assert.Null(monthly[2].SupplyAct);
        Assert.Null(monthly[2].GapAct);
        Assert.Equal(-160135L, monthly[2].GapExp);
    }

    [Fact]
    public void Reads_the_vacancy_assumption_as_a_percentage()
    {
        var (cities, _) = ReadOne("jeddah");

        var city = Assert.Single(cities);
        Assert.Equal("jeddah", city.CityId);
        Assert.Equal(10.0, city.VacancyAssumptionPct);
    }

    [Fact]
    public void Unknown_sheet_is_skipped_with_a_warning()
    {
        var (cities, warnings) = ReadOne("Tabuk");

        Assert.Empty(cities);
        var w = Assert.Single(warnings);
        Assert.Equal("supply-demand-gap/Tabuk", w.Sheet);
        Assert.Contains("unknown city sheet", w.Issue);
    }

    [Fact]
    public void Missing_annual_block_warns_but_keeps_the_monthly_block()
    {
        var (cities, warnings) = ReadOne("Dammam", withAnnual: false);

        var city = Assert.Single(cities);
        Assert.Empty(city.Annual);
        Assert.Equal(3, city.Monthly.Count);
        Assert.Contains(warnings, w => w.Sheet == "supply-demand-gap/Dammam" && w.Issue.Contains("annual block not found"));
    }

    [Fact]
    public void Formula_cells_are_read_from_their_calculated_values()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Makkah");
        WriteCitySheet(ws);
        ws.Cell("B4").FormulaA1 = "2441532.222";
        ws.Cell("E4").FormulaA1 = "C4-B4";
        wb.RecalculateAllFormulas(); // as Excel does before saving

        var city = Assert.Single(SupplyDemandGapReader.Read(wb, new List<ConversionWarning>()));

        Assert.Equal(2441532L, city.Annual[0].DemandExp);
        Assert.Equal(-150328L, city.Annual[0].GapExp);
    }

    [Fact]
    public void Missing_monthly_block_warns_but_keeps_the_annual_block()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Madinah");
        WriteCitySheet(ws);
        ws.Cell("J2").Clear();

        var warnings = new List<ConversionWarning>();
        var city = Assert.Single(SupplyDemandGapReader.Read(wb, warnings));

        Assert.Empty(city.Monthly);
        Assert.Equal(2, city.Annual.Count);
        Assert.Contains(warnings, w => w.Issue.Contains("monthly block not found"));
    }

    [Fact]
    public void Annual_header_without_supply_exp_warns_and_skips_the_annual_block()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Riyadh");
        WriteCitySheet(ws);
        ws.Cell("C3").Clear();

        var warnings = new List<ConversionWarning>();
        var city = Assert.Single(SupplyDemandGapReader.Read(wb, warnings));

        Assert.Empty(city.Annual);
        Assert.Contains(warnings, w => w.Issue.Contains("missing 'Supply Exp'"));
    }

    [Fact]
    public void Vacancy_assumption_typed_as_a_whole_percentage_is_accepted()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Riyadh");
        WriteCitySheet(ws);
        ws.Cell("B1").Value = 10;

        Assert.Equal(10.0, Assert.Single(SupplyDemandGapReader.Read(wb, new List<ConversionWarning>())).VacancyAssumptionPct);
    }
}
