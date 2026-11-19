using ClosedXML.Excel;
using AqarInsights.Api.Services.RentalControls.Reading;
using AqarInsights.Api.Models.RentalControls.Raw;

namespace AqarInsights.Api.Tests.RentalControls;

public class OverviewReaderTests
{
    [Fact]
    public void Read_Q3Headers_MapsRenamedColumns_AndDerivesStatus()
    {
        using var wb = new XLWorkbook();
        var sheet = wb.AddWorksheet("لوحة الضوابط");
        var headers = new[]
        {
            "المدينة", "المعدل السنوي المركب لأسعار الإيجار (2022-2026)", "المسار\n6\n7-9\n10+",
            "الشواغر الايجارية\nأقل من 5%", "العائد من الاستثمار\nأقل من 6%", "التغير الإيجاري",
            "نسبة الإيجار إلى الدخل\nأكثر من 30%", "العبء السكني للدخل المحدود\nأكثر من 30%",
            "التقييم العام", "تفاصيل المدينة",
        };
        for (var c = 0; c < headers.Length; c++) sheet.Cell(3, c + 2).Value = headers[c];
        sheet.Cell(4, 2).Value = "الرياض";
        sheet.Cell(4, 3).Value = 0.0888;
        sheet.Cell(4, 4).Value = "مرتفع";
        sheet.Cell(4, 5).Value = 0.07;
        sheet.Cell(4, 6).Value = 0.05;
        sheet.Cell(4, 7).Value = -0.01;
        sheet.Cell(4, 8).Value = 0.15;
        sheet.Cell(4, 9).Value = 0.35;
        sheet.Cell(4, 10).Value = "\u200fالضوابط الايجارية مفعلة\n\u200fمراقبة";

        var riyadh = Assert.Single(OverviewReader.Read(sheet));

        Assert.Equal(0.0888, riyadh.Cagr, precision: 5);
        Assert.Equal("مرتفع", riyadh.Path);
        Assert.Equal(-0.01, riyadh.RentalIncrease, precision: 5);
        Assert.Equal(0.15, riyadh.RentToIncome, precision: 5);
        Assert.Equal(0.35, riyadh.Burden, precision: 5);
        Assert.Equal("", riyadh.Status);
        Assert.Equal("الضوابط الايجارية مفعلة", riyadh.AssessmentLine1);
        Assert.Equal("مراقبة", riyadh.AssessmentLine2);
    }

    [Fact]
    public void Read_ParsesRiyadhRow_WithRawValues()
    {
        // Arrange: Create in-memory workbook with header + Riyadh row
        using var wb = new XLWorkbook();
        var sheet = wb.AddWorksheet("Overview");
        
        // Header row (row 1)
        sheet.Cell(1, 1).Value = "المدينة";
        sheet.Cell(1, 2).Value = "الكيجر";
        sheet.Cell(1, 3).Value = "المسار";
        sheet.Cell(1, 4).Value = "الشواغر";
        sheet.Cell(1, 5).Value = "العائد";
        sheet.Cell(1, 6).Value = "الزيادة";
        sheet.Cell(1, 7).Value = "إيجار للدخل";
        sheet.Cell(1, 8).Value = "العبء";
        sheet.Cell(1, 9).Value = "التقييم العام";
        
        // Riyadh data row (row 2)
        sheet.Cell(2, 1).Value = "الرياض";
        sheet.Cell(2, 2).Value = 0.097;
        sheet.Cell(2, 3).Value = "متسارع";
        sheet.Cell(2, 4).Value = 0.07;
        sheet.Cell(2, 5).Value = 0.048;
        sheet.Cell(2, 6).Value = 0.12;
        sheet.Cell(2, 7).Value = 0.32;
        sheet.Cell(2, 8).Value = 0.28;
        sheet.Cell(2, 9).Value = "الضوابط مفعلة\nيستمر بمراقبة إضافية";
        
        // Act
        var cities = OverviewReader.Read(sheet);
        
        // Assert
        Assert.Single(cities);
        var riyadh = cities[0];
        Assert.Equal("riyadh", riyadh.Id);
        Assert.Equal("الرياض", riyadh.Name);
        Assert.Equal(0.097, riyadh.Cagr, precision: 5);
        Assert.Equal("متسارع", riyadh.Path);
        Assert.Equal(0.07, riyadh.Vacancy, precision: 5);
        Assert.NotNull(riyadh.InvestmentReturn);
        Assert.Equal(0.048, riyadh.InvestmentReturn.Value, precision: 5);
        Assert.Equal(0.12, riyadh.RentalIncrease, precision: 5);
        Assert.Equal(0.32, riyadh.RentToIncome, precision: 5);
        Assert.Equal(0.28, riyadh.Burden, precision: 5);
        Assert.Contains("مفعلة", riyadh.AssessmentLine1);
        Assert.NotEmpty(riyadh.AssessmentLine2);
        Assert.Contains("مراقبة", riyadh.AssessmentLine2);
    }
    
    [Fact]
    public void Read_HandlesNullInvestmentReturn_WhenCellIsDash()
    {
        using var wb = new XLWorkbook();
        var sheet = wb.AddWorksheet("Overview");
        
        // Header
        sheet.Cell(1, 1).Value = "المدينة";
        sheet.Cell(1, 2).Value = "الكيجر";
        sheet.Cell(1, 3).Value = "المسار";
        sheet.Cell(1, 4).Value = "الشواغر";
        sheet.Cell(1, 5).Value = "العائد";
        sheet.Cell(1, 6).Value = "الزيادة";
        sheet.Cell(1, 7).Value = "إيجار للدخل";
        sheet.Cell(1, 8).Value = "العبء";
        sheet.Cell(1, 9).Value = "التقييم العام";
        
        // Data with dash for InvestmentReturn
        sheet.Cell(2, 1).Value = "مكة المكرمة";
        sheet.Cell(2, 2).Value = 0.05;
        sheet.Cell(2, 3).Value = "مرتفع";
        sheet.Cell(2, 4).Value = 0.08;
        sheet.Cell(2, 5).Value = "—";
        sheet.Cell(2, 6).Value = 0.10;
        sheet.Cell(2, 7).Value = 0.30;
        sheet.Cell(2, 8).Value = 0.25;
        sheet.Cell(2, 9).Value = "يتطلب دراسة تفصيلية";
        
        var cities = OverviewReader.Read(sheet);
        
        Assert.Single(cities);
        Assert.Null(cities[0].InvestmentReturn);
    }
    
    [Fact]
    public void Read_StopsAtFirstBlankCityName()
    {
        using var wb = new XLWorkbook();
        var sheet = wb.AddWorksheet("Overview");
        
        // Header
        sheet.Cell(1, 1).Value = "المدينة";
        sheet.Cell(1, 2).Value = "الكيجر";
        sheet.Cell(1, 3).Value = "المسار";
        sheet.Cell(1, 4).Value = "الشواغر";
        sheet.Cell(1, 5).Value = "العائد";
        sheet.Cell(1, 6).Value = "الزيادة";
        sheet.Cell(1, 7).Value = "إيجار للدخل";
        sheet.Cell(1, 8).Value = "العبء";
        sheet.Cell(1, 9).Value = "التقييم العام";
        
        // Two data rows
        sheet.Cell(2, 1).Value = "الرياض";
        sheet.Cell(2, 2).Value = 0.097;
        sheet.Cell(2, 3).Value = "متسارع";
        sheet.Cell(2, 4).Value = 0.07;
        sheet.Cell(2, 5).Value = 0.048;
        sheet.Cell(2, 6).Value = 0.12;
        sheet.Cell(2, 7).Value = 0.32;
        sheet.Cell(2, 8).Value = 0.28;
        sheet.Cell(2, 9).Value = "الضوابط مفعلة";
        
        sheet.Cell(3, 1).Value = "جدة";
        sheet.Cell(3, 2).Value = 0.08;
        sheet.Cell(3, 3).Value = "مرتفع";
        sheet.Cell(3, 4).Value = 0.09;
        sheet.Cell(3, 5).Value = 0.05;
        sheet.Cell(3, 6).Value = 0.11;
        sheet.Cell(3, 7).Value = 0.30;
        sheet.Cell(3, 8).Value = 0.26;
        sheet.Cell(3, 9).Value = "يتطلب دراسة";
        
        // Blank row (should stop here)
        sheet.Cell(4, 1).Value = "";
        
        // Another row that should be ignored
        sheet.Cell(5, 1).Value = "الدمام";
        
        var cities = OverviewReader.Read(sheet);
        
        Assert.Equal(2, cities.Count);
        Assert.Equal("riyadh", cities[0].Id);
        Assert.Equal("jeddah", cities[1].Id);
    }
}
