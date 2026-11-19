using ClosedXML.Excel;
using Xunit;
using AqarInsights.Api.Services.RentalControls.Reading;

namespace AqarInsights.Api.Tests.RentalControls;

public class CityReaderTests
{
    private static readonly string[] SalesCoreHeaders = { "الفترة", "شقة", "فيلا", "دور", "دوبلكس", "أرض" };

    private static IXLWorksheet AddSalesSheet(XLWorkbook wb, string[] headers, params object[][] rows)
    {
        wb.AddWorksheet("لوحة المؤشرات");
        var sheet = wb.AddWorksheet("الصفقات البيعية");
        for (var c = 0; c < headers.Length; c++) sheet.Cell(4, c + 2).Value = headers[c];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                sheet.Cell(5 + r, c + 2).Value = XLCellValue.FromObject(rows[r][c]);
        return sheet;
    }

    [Fact]
    public void Read_SalesSeries_ReadsOptionalTypes_SkipsDashCells()
    {
        using var wb = new XLWorkbook();
        AddSalesSheet(wb, SalesCoreHeaders.Concat(new[] { "أخرى", "عمارة", "الإجمالي" }).ToArray(),
            new object[] { "2024-Q4", 10, 20, 30, 40, 50, "-", "-", 150 },
            new object[] { "2026-Q2", 3299, 2035, 1652, 950, 3769, 722, 6, 12433 });

        var result = CityReader.Read("riyadh", wb);

        Assert.Equal(2, result.SalesSeries.Count);
        Assert.False(result.SalesSeries[0].Values.ContainsKey("أخرى"));
        Assert.Equal(150, result.SalesSeries[0].Values["الإجمالي"]);
        Assert.Equal(722, result.SalesSeries[1].Values["أخرى"]);
        Assert.Equal(6, result.SalesSeries[1].Values["عمارة"]);
    }

    [Fact]
    public void Read_SalesSeries_WithoutOptionalTypes_StillReadsCoreColumns()
    {
        using var wb = new XLWorkbook();
        AddSalesSheet(wb, SalesCoreHeaders.Append("الإجمالي").ToArray(),
            new object[] { "2026-Q2", 3299, 2035, 1652, 950, 3769, 11705 });

        var result = CityReader.Read("riyadh", wb);

        Assert.Single(result.SalesSeries);
        Assert.Equal(11705, result.SalesSeries[0].Values["الإجمالي"]);
        Assert.Equal(3769, result.SalesSeries[0].Values["أرض"]);
    }

    [Fact]
    public void Read_ExtractsSheet1BulletsAndMovementRows()
    {
        // Arrange: in-memory workbook with sheet 1 markers (like real city workbooks)
        using var wb = new XLWorkbook();
        var sheet1 = wb.AddWorksheet("لوحة المؤشرات - الرياض");
        
        // Market movement table
        sheet1.Cell(5, 2).Value = "لوحة مؤشرات السوق العقاري";
        sheet1.Cell(7, 2).Value = "الطلب على الإيجار";
        sheet1.Cell(7, 3).Value = "مرتفع";
        sheet1.Cell(7, 4).Value = "↑12%";
        sheet1.Cell(7, 5).Value = "إيجابي";
        
        // Executive summary bullets
        sheet1.Cell(12, 2).Value = "أبرز الاستنتاجات";
        sheet1.Cell(13, 2).Value = "ارتفاع في الطلب\nانخفاض في المعروض\nاستقرار في الأسعار";
        
        // Act
        var result = CityReader.Read("riyadh", wb);
        
        // Assert
        Assert.Equal("riyadh", result.CityId);
        Assert.Equal(3, result.Bullets.Count);
        Assert.Contains("ارتفاع في الطلب", result.Bullets);
        Assert.Single(result.MovementRows);
        Assert.Equal("الطلب على الإيجار", result.MovementRows[0].Indicator);
        Assert.Equal("مرتفع", result.MovementRows[0].Level);
        Assert.Equal("↑12%", result.MovementRows[0].Change);
        Assert.Equal("إيجابي", result.MovementRows[0].Assessment);
    }

    [Fact]
    public void Read_ExtractsRentalsSeries()
    {
        // Arrange: workbook with rentals sheet (الصفقات الإيجارية)
        using var wb = new XLWorkbook();
        wb.AddWorksheet("لوحة المؤشرات");
        var rentals = wb.AddWorksheet("الصفقات الإيجارية");
        
        // Header row 4
        rentals.Cell(4, 2).Value = "الفترة";
        rentals.Cell(4, 3).Value = "شقة";
        rentals.Cell(4, 4).Value = "دور";
        rentals.Cell(4, 5).Value = "فيلا";
        rentals.Cell(4, 6).Value = "دوبلاكس";
        rentals.Cell(4, 7).Value = "استديو";
        rentals.Cell(4, 8).Value = "الإجمالي";
        
        // Data
        rentals.Cell(5, 2).Value = "2024 Q1";
        rentals.Cell(5, 3).Value = 1200;
        rentals.Cell(5, 4).Value = 300;
        rentals.Cell(5, 5).Value = 450;
        rentals.Cell(5, 6).Value = 150;
        rentals.Cell(5, 7).Value = 200;
        rentals.Cell(5, 8).Value = 2300;
        
        rentals.Cell(6, 2).Value = "2024 Q2";
        rentals.Cell(6, 3).Value = 1250;
        rentals.Cell(6, 8).Value = 2400;
        
        // Act
        var result = CityReader.Read("test", wb);
        
        // Assert
        Assert.Equal(2, result.RentalsSeries.Count);
        Assert.Equal("2024 Q1", result.RentalsSeries[0].Period);
        Assert.Equal(1200, result.RentalsSeries[0].Values["شقة"]);
        Assert.Equal(300, result.RentalsSeries[0].Values["دور"]);
        Assert.Equal(2300, result.RentalsSeries[0].Values["الإجمالي"]);
        Assert.Equal("2024 Q2", result.RentalsSeries[1].Period);
        Assert.Equal(1250, result.RentalsSeries[1].Values["شقة"]);
    }

    [Fact]
    public void Read_ExtractsRentIndexSeriesWithChange()
    {
        // Arrange: workbook with CPI sheet (مؤشرات CPI)
        using var wb = new XLWorkbook();
        wb.AddWorksheet("لوحة المؤشرات");
        var cpi = wb.AddWorksheet("مؤشرات CPI");
        
        // Header row 4
        cpi.Cell(4, 2).Value = "الشهر";
        cpi.Cell(4, 3).Value = "الرقم القياسي";
        cpi.Cell(4, 5).Value = "التغير السنوي";
        
        // Data
        cpi.Cell(5, 2).Value = "2024-01";
        cpi.Cell(5, 3).Value = 105.3;
        cpi.Cell(5, 5).Value = "↑3.2%";
        
        cpi.Cell(6, 2).Value = "2024-02";
        cpi.Cell(6, 3).Value = 106.1;
        cpi.Cell(6, 5).Value = "↑3.5%";
        
        // Act
        var result = CityReader.Read("test", wb);
        
        // Assert
        Assert.Equal(2, result.RentIndexSeries.Count);
        Assert.Equal("2024-01", result.RentIndexSeries[0].Period);
        Assert.Equal(105.3, result.RentIndexSeries[0].Values["الرقم القياسي"]);
        Assert.Equal("↑3.2%", result.RentIndexSeries[0].Change);
        Assert.Equal("2024-02", result.RentIndexSeries[1].Period);
        Assert.Equal(106.1, result.RentIndexSeries[1].Values["الرقم القياسي"]);
        Assert.Equal("↑3.5%", result.RentIndexSeries[1].Change);
    }

    [Fact]
    public void Read_StopsRentIndexAtSecondCpiBlock()
    {
        // The CPI sheet stacks a general-inflation block (percentage text) below a
        // blank separator; only the first (rent-index) block must be extracted.
        using var wb = new XLWorkbook();
        wb.AddWorksheet("لوحة المؤشرات");
        var cpi = wb.AddWorksheet("مؤشرات CPI");

        cpi.Cell(4, 2).Value = "الشهر";
        cpi.Cell(4, 3).Value = "الرقم القياسي";
        cpi.Cell(4, 5).Value = "التغير السنوي";

        cpi.Cell(5, 2).Value = "2024-01";
        cpi.Cell(5, 3).Value = 133.6;
        cpi.Cell(5, 5).Value = "↑9.9%";
        cpi.Cell(6, 2).Value = "2024-02";
        cpi.Cell(6, 3).Value = 134.9;
        cpi.Cell(6, 5).Value = "↑10.3%";

        // Row 7 blank separator, then a second block whose "index" column holds percentages.
        cpi.Cell(8, 2).Value = "التضخم العام (CPI)";
        cpi.Cell(9, 2).Value = "الشهر";
        cpi.Cell(9, 3).Value = "التغير السنوي";
        cpi.Cell(10, 2).Value = "2024-01";
        cpi.Cell(10, 3).Value = "+1.95%";
        cpi.Cell(11, 2).Value = "2024-02";
        cpi.Cell(11, 3).Value = "+2.10%";

        var result = CityReader.Read("test", wb);

        Assert.Equal(2, result.RentIndexSeries.Count);
        Assert.Equal(133.6, result.RentIndexSeries[0].Values["الرقم القياسي"]);
        Assert.Equal(134.9, result.RentIndexSeries[^1].Values["الرقم القياسي"]);
    }

    [Fact]
    public void Read_EmptyRentIndexBlock_DoesNotReadInflationBlock()
    {
        // Some cities (أبها, جازان) have no rent-index data: row 5 is empty and the
        // التضخم العام block follows immediately. Its percentages must NOT be read as index.
        using var wb = new XLWorkbook();
        wb.AddWorksheet("لوحة المؤشرات");
        var cpi = wb.AddWorksheet("مؤشرات CPI");

        cpi.Cell(4, 2).Value = "الشهر";
        cpi.Cell(4, 3).Value = "الرقم القياسي";
        cpi.Cell(4, 5).Value = "التغير السنوي";

        // Row 5 empty (no rent-index data), then the inflation block title + percentages.
        cpi.Cell(6, 2).Value = "التضخم العام (CPI) - المدينة";
        cpi.Cell(7, 2).Value = "الشهر";
        cpi.Cell(7, 3).Value = "التغير السنوي";
        cpi.Cell(8, 2).Value = "2022-01";
        cpi.Cell(8, 3).Value = "+2.02%";
        cpi.Cell(9, 2).Value = "2022-02";
        cpi.Cell(9, 3).Value = "+1.84%";

        var result = CityReader.Read("test", wb);

        Assert.Empty(result.RentIndexSeries);
    }

    [Fact]
    public void Read_extracts_rent_vs_inflation_series_from_cpi_sheet()
    {
        using var wb = new XLWorkbook();
        wb.AddWorksheet("لوحة المؤشرات");
        var ws = wb.AddWorksheet("مؤشرات CPI");
        // rent block B..E
        ws.Cell(4, 2).Value = "الشهر";
        ws.Cell(4, 3).Value = "الرقم القياسي";
        ws.Cell(4, 4).Value = "التغير الشهري";
        ws.Cell(4, 5).Value = "التغير السنوي";
        // inflation block I..J (col 9,10)
        ws.Cell(3, 9).Value = "التضخم العام (CPI)";
        ws.Cell(4, 9).Value = "الشهر";
        ws.Cell(4, 10).Value = "التغير السنوي";
        ws.Cell(5, 2).Value = "2022-01";
        ws.Cell(5, 3).Value = 85.7;
        ws.Cell(5, 4).Value = "+0.7%";
        ws.Cell(5, 5).Value = "+4.56%";
        ws.Cell(5, 9).Value = "2022-01";
        ws.Cell(5, 10).Value = "+2.93%";

        var raw = CityReader.Read("riyadh", wb);

        var pt = Assert.Single(raw.RentVsInflationSeries);
        Assert.Equal("2022-01", pt.Period);
        Assert.Equal(4.56, pt.Values["الإيجار الفعلي"], 2);
        Assert.Equal(2.93, pt.Values["التضخم العام"], 2);
    }

    [Fact]
    public void Read_extracts_rent_vs_inflation_from_stacked_cpi_blocks()
    {
        // Most cities (e.g. عرعر) stack the general-inflation block BELOW the rent block
        // instead of side-by-side, so header row 4 carries only one "التغير السنوي".
        using var wb = new XLWorkbook();
        wb.AddWorksheet("لوحة المؤشرات");
        var ws = wb.AddWorksheet("مؤشرات CPI");
        // rent block on header row 4 (single change column)
        ws.Cell(4, 2).Value = "الشهر";
        ws.Cell(4, 3).Value = "الرقم القياسي";
        ws.Cell(4, 5).Value = "التغير السنوي";
        ws.Cell(5, 2).Value = "2022-01";
        ws.Cell(5, 3).Value = 101.6;
        ws.Cell(5, 5).Value = "+4.56%";
        ws.Cell(6, 2).Value = "2022-02";
        ws.Cell(6, 3).Value = 101.8;
        ws.Cell(6, 5).Value = "+4.79%";
        // blank separator row 7, then the stacked inflation block
        ws.Cell(8, 2).Value = "التضخم العام (CPI)";
        ws.Cell(9, 2).Value = "الشهر";
        ws.Cell(9, 3).Value = "التغير السنوي";
        ws.Cell(10, 2).Value = "2022-01";
        ws.Cell(10, 3).Value = "+2.93%";
        ws.Cell(11, 2).Value = "2022-02";
        ws.Cell(11, 3).Value = "+3.16%";

        var raw = CityReader.Read("arar", wb);

        Assert.Equal(2, raw.RentVsInflationSeries.Count);
        var jan = raw.RentVsInflationSeries.Single(p => p.Period == "2022-01");
        Assert.Equal(4.56, jan.Values["الإيجار الفعلي"], 2);
        Assert.Equal(2.93, jan.Values["التضخم العام"], 2);
        var feb = raw.RentVsInflationSeries.Single(p => p.Period == "2022-02");
        Assert.Equal(4.79, feb.Values["الإيجار الفعلي"], 2);
        Assert.Equal(3.16, feb.Values["التضخم العام"], 2);
    }

    [Fact]
    public void Read_ExtractsAffordabilitySeries()
    {
        // Arrange: workbook with affordability sheet (القدرة الإيجارية)
        using var wb = new XLWorkbook();
        wb.AddWorksheet("لوحة المؤشرات");
        var afford = wb.AddWorksheet("القدرة الإيجارية");
        
        // Header row 4
        afford.Cell(4, 2).Value = "الفترة";
        afford.Cell(4, 3).Value = "النسبة";
        
        // Data
        afford.Cell(5, 2).Value = "2024 Q1";
        afford.Cell(5, 3).Value = 28.5;
        
        afford.Cell(6, 2).Value = "2024 Q2";
        afford.Cell(6, 3).Value = 29.1;
        
        // Act
        var result = CityReader.Read("test", wb);
        
        // Assert
        Assert.Equal(2, result.AffordabilitySeries.Count);
        Assert.Equal("2024 Q1", result.AffordabilitySeries[0].Period);
        Assert.Equal(28.5, result.AffordabilitySeries[0].Values["النسبة"]);
        Assert.Equal("2024 Q2", result.AffordabilitySeries[1].Period);
        Assert.Equal(29.1, result.AffordabilitySeries[1].Values["النسبة"]);
    }

    [Fact]
    public void Read_ExtractsBurdenSeries_FromSecondSubTableOnAffordabilitySheet()
    {
        // The affordability sheet stacks two sub-tables: نسبة الإيجار إلى الدخل (النسبة)
        // on top and العبء السكني للدخل المحدود (العبء) below it.
        using var wb = new XLWorkbook();
        wb.AddWorksheet("لوحة المؤشرات");
        var afford = wb.AddWorksheet("القدرة الإيجارية");

        // Rent-to-income sub-table (header row 4)
        afford.Cell(4, 2).Value = "الفترة";
        afford.Cell(4, 3).Value = "النسبة";
        afford.Cell(5, 2).Value = "2026-Q1";
        afford.Cell(5, 3).Value = 15.98;
        afford.Cell(6, 2).Value = "2026-Q2";
        afford.Cell(6, 3).Value = 15.38;

        // Burden sub-table lower down (header row 9)
        afford.Cell(9, 2).Value = "الفترة";
        afford.Cell(9, 3).Value = "العبء";
        afford.Cell(10, 2).Value = "2025-Q4";
        afford.Cell(10, 3).Value = 36;
        afford.Cell(11, 2).Value = "2026-Q1";
        afford.Cell(11, 3).Value = 36;
        afford.Cell(12, 2).Value = "2026-Q2";
        afford.Cell(12, 3).Value = 35;

        var result = CityReader.Read("riyadh", wb);

        Assert.Equal(3, result.BurdenSeries.Count);
        Assert.Equal("2025-Q4", result.BurdenSeries[0].Period);
        Assert.Equal(36, result.BurdenSeries[0].Values["العبء"]);
        Assert.Equal("2026-Q2", result.BurdenSeries[2].Period);
        Assert.Equal(35, result.BurdenSeries[2].Values["العبء"]);
    }
}
