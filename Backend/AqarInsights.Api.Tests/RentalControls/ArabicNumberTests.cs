using AqarInsights.Api.Services.RentalControls;

namespace AqarInsights.Api.Tests.RentalControls;

public class ArabicNumberTests
{
    [Theory]
    [InlineData("9.7%", 9.7)]
    [InlineData("+9%", 9)]
    [InlineData("33,157 (-4%)", 33157)]
    [InlineData("−4%", -4)]      // Unicode minus U+2212
    [InlineData("-6%", -6)]
    public void FirstNumber_parses_expected(string input, double expected)
        => Assert.Equal(expected, ArabicNumber.FirstNumber(input));

    [Theory]
    [InlineData("—")]
    [InlineData("-")]
    [InlineData("")]
    [InlineData(null)]
    public void FirstNumber_returns_null_for_non_numeric(string? input)
        => Assert.Null(ArabicNumber.FirstNumber(input));

    [Theory]
    [InlineData("6%", 6)]
    [InlineData("−4%", -4)]      // Unicode minus U+2212
    [InlineData("+10%", 10)]
    public void PercentInt_parses_whole_percent(string input, int expected)
        => Assert.Equal(expected, ArabicNumber.PercentInt(input));

    [Theory]
    [InlineData("9.7%")]        // not a whole integer
    [InlineData("—")]
    [InlineData(null)]
    public void PercentInt_returns_fallback_for_non_integer(string? input)
        => Assert.Equal(-1, ArabicNumber.PercentInt(input));
}
