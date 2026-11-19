using AqarInsights.Api.Services.RentalControls;

namespace AqarInsights.Api.Tests.RentalControls;

public class RentalControlsMappingsTests
{
    [Theory]
    [InlineData("الرياض", "riyadh")]
    [InlineData("المدينة المنورة", "madinah")]
    [InlineData("مكة المكرمة", "makkah")]
    public void CityId_maps_known_cities(string name, string expected)
        => Assert.Equal(expected, RentalControlsMappings.CityId(name));

    [Theory]
    [InlineData("متسارع", "نمو متسارع", "accelerated")]
    [InlineData("مرتفع", "نمو مرتفع", "elevated")]
    [InlineData("استباقي", "نمو مستدام", "sustainable")]
    public void Classification_maps_masar(string masar, string cls, string label)
    {
        var (c, l) = RentalControlsMappings.Classification(masar);
        Assert.Equal(cls, c);
        Assert.Equal(label, l);
    }

    [Theory]
    [InlineData("الضوابط الايجارية مفعلة", "الضوابط مفعلة")]
    [InlineData("التوصية: دراسة تفصيلية ورصد إعلامي للمدينة", "يتطلب دراسة تفصيلية")]
    [InlineData("التوصية: دراسة استباقية ورصد إعلامي للمدينة", "يتطلب دراسة استباقية")]
    [InlineData("ضمن الحدود", "لا إجراء")]
    public void OperationalStatus_maps_assessment(string line1, string expected)
        => Assert.Equal(expected, RentalControlsMappings.OperationalStatus(line1));

    [Theory]
    [InlineData(0.07, false, 0, "7%")]
    [InlineData(0.09, true, 0, "+9%")]
    [InlineData(0.0968836, false, 1, "9.7%")]
    [InlineData(-0.0054, true, 0, "-1%")]
    [InlineData(-0.0054, true, 1, "-0.5%")]
    [InlineData(-0.001, true, 0, "0%")]
    [InlineData(0.0, true, 0, "0%")]
    [InlineData(-0.001, false, 0, "0%")]
    public void Percent_formats(double v, bool plus, int dec, string expected)
        => Assert.Equal(expected, RentalControlsMappings.Percent(v, plus, dec));

    [Fact]
    public void Percent_passes_through_dash()
        => Assert.Equal("—", RentalControlsMappings.Percent("—"));
}
