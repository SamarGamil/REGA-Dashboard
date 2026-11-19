using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Reports;
using Xunit;

namespace AqarInsights.Api.Tests.RentalControls.Reports;

public class NationalReportRendererTests
{
    // Counts pages in the actual GeneratePdf output (the endpoint path), which
    // paginates independently of GenerateImages.
    private static int PdfPageCount(byte[] pdf)
    {
        var txt = System.Text.Encoding.GetEncoding(28591).GetString(pdf);
        return Regex.Matches(txt, @"/Type\s*/Page(?![s])").Count;
    }
    private static NationalReportModel Model()
    {
        var data = new MarketAnalysisData
        {
            LastQuarter = "الربع الثاني 2026",
            Cities = new()
            {
                new() { Name = "الرياض", Classification = "نمو متسارع", Cagr = 9.7, HousingBurden = 35, InvestmentReturn = 5.1, ActualRentalGrowth = 5.9 },
                new() { Name = "جدة", Classification = "نمو مستدام", Cagr = 4.6, HousingBurden = 35, InvestmentReturn = 5.5, ActualRentalGrowth = 0.9 },
            },
        };
        return NationalReportModelBuilder.Build(data, new DateOnly(2026, 8, 23), new NationalReportOptions());
    }

    [Fact]
    public void Word_render_produces_valid_docx_with_title_and_city()
    {
        var bytes = NationalReportWordRenderer.Render(Model());
        Assert.NotEmpty(bytes);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var text = doc.MainDocumentPart!.Document.Body!.InnerText;
        Assert.Contains("الملخص التنفيذي", text);
        Assert.Contains("مؤشرات الضوابط الإيجارية", text);
        Assert.Contains("دلالة الألوان", text);
        Assert.Contains("الرياض", text);
    }

    [Fact]
    public void Pdf_render_produces_a_single_page_pdf()
    {
        var bytes = NationalReportPdfRenderer.Render(Model());
        Assert.NotEmpty(bytes);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal(1, PdfPageCount(bytes));
    }

    [Fact]
    public void Pdf_render_is_single_page_for_full_14_city_report()
    {
        var data = new MarketAnalysisData
        {
            LastQuarter = "الربع الثاني 2026",
            Cities = new()
            {
                new() { Name = "الرياض", Classification = "نمو متسارع", OperationalStatus = "الضوابط مفعلة", Cagr = 9.7, HousingBurden = 35, InvestmentReturn = 5.1, ActualRentalGrowth = 5.9 },
                new() { Name = "جدة", Classification = "نمو مستدام", OperationalStatus = "يتطلب دراسة استباقية", Cagr = 4.6, HousingBurden = 35, InvestmentReturn = 5.5, ActualRentalGrowth = 0.9 },
                new() { Name = "الدمام", Classification = "نمو مرتفع", OperationalStatus = "يتطلب دراسة استباقية", Cagr = 7.2, HousingBurden = 32, InvestmentReturn = 5.3, ActualRentalGrowth = 3.8 },
                new() { Name = "مكة المكرمة", Classification = "نمو مستدام", OperationalStatus = "يتطلب دراسة استباقية", Cagr = 5.1, HousingBurden = 38, InvestmentReturn = 4.9, ActualRentalGrowth = 2.1 },
                new() { Name = "المدينة المنورة", Classification = "نمو متسارع", OperationalStatus = "يتطلب دراسة استباقية", Cagr = 8.3, HousingBurden = 33, InvestmentReturn = 5.2, ActualRentalGrowth = 4.7 },
                new() { Name = "الطائف", Classification = "نمو مستدام", OperationalStatus = "يتطلب دراسة استباقية", Cagr = 4.2, HousingBurden = 29, InvestmentReturn = 5.0, ActualRentalGrowth = 1.5 },
                new() { Name = "تبوك", Classification = "نمو مرتفع", OperationalStatus = "يتطلب دراسة استباقية", Cagr = 6.8, HousingBurden = 31, InvestmentReturn = 5.4, ActualRentalGrowth = 3.2 },
                new() { Name = "الخبر", Classification = "نمو مستدام", OperationalStatus = "يتطلب دراسة استباقية", Cagr = 4.9, HousingBurden = 34, InvestmentReturn = 5.1, ActualRentalGrowth = 1.8 },
                new() { Name = "خميس مشيط", Classification = "نمو متسارع", OperationalStatus = "يتطلب دراسة استباقية", Cagr = 8.9, HousingBurden = 30, InvestmentReturn = 5.6, ActualRentalGrowth = 5.2 },
                new() { Name = "بريدة", Classification = "نمو مرتفع", OperationalStatus = "يتطلب دراسة استباقية", Cagr = 6.5, HousingBurden = 28, InvestmentReturn = 5.3, ActualRentalGrowth = 2.9 },
                new() { Name = "حائل", Classification = "نمو مستدام", OperationalStatus = "يتطلب دراسة تفصيلية", Cagr = 3.8, HousingBurden = 27, InvestmentReturn = 4.8, ActualRentalGrowth = 1.2 },
                new() { Name = "نجران", Classification = "نمو مرتفع", OperationalStatus = "يتطلب دراسة استباقية", Cagr = 7.5, HousingBurden = 29, InvestmentReturn = 5.5, ActualRentalGrowth = 3.5 },
                new() { Name = "ينبع", Classification = "نمو مستدام", OperationalStatus = "يتطلب دراسة استباقية", Cagr = 4.4, HousingBurden = 30, InvestmentReturn = null, ActualRentalGrowth = null },
                new() { Name = "القصيم", Classification = "نمو متسارع", OperationalStatus = "يتطلب دراسة استباقية", Cagr = 8.1, HousingBurden = 31, InvestmentReturn = 5.4, ActualRentalGrowth = 4.3 },
            },
        };
        var model = NationalReportModelBuilder.Build(data, new DateOnly(2026, 8, 23), new NationalReportOptions());

        var bytes = NationalReportPdfRenderer.Render(model);
        Assert.NotEmpty(bytes);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal(1, PdfPageCount(bytes));
    }
}
