using ClosedXML.Excel;
using AqarInsights.Api.Services.RentalControls.Reading;

namespace AqarInsights.Api.Tests.RentalControls;

public class SheetReaderTests
{
    private static IXLWorksheet Build()
    {
        var ws = new XLWorkbook().AddWorksheet("العبء الايجاري");
        // header row 1 in a shuffled order
        ws.Cell(1, 1).Value = "المدينة"; ws.Cell(1, 2).Value = "العبء السكني"; ws.Cell(1, 3).Value = "السنة";
        return ws;
    }

    [Fact]
    public void Resolves_columns_by_header_regardless_of_order()
    {
        var r = SheetReader.ForHeader(Build(), 1);
        Assert.Equal(3, r.Col("السنة"));
        Assert.Equal(1, r.Col("المدينة"));
        Assert.Equal(2, r.Col("العبء"));
    }

    [Fact]
    public void Throws_on_missing_required_header()
        => Assert.Throws<RentalControlsReadException>(() => SheetReader.ForHeader(Build(), 1).Col("الجنسية"));

    [Fact]
    public void Sheet_resolves_by_name_contains()
    {
        var wb = new XLWorkbook(); wb.AddWorksheet("العبء الايجاري");
        Assert.Equal("العبء الايجاري", SheetReader.Sheet(wb, "العبء").Name);
        Assert.Throws<RentalControlsReadException>(() => SheetReader.Sheet(wb, "غير موجود"));
    }
}
