using AqarInsights.Api.Utilities;

namespace AqarInsights.Api.Tests;

public class NationalityNormalizerTests
{
    [Theory]
    [InlineData("مصري", "مصر")]
    [InlineData("كندي", "كندا")]
    [InlineData("قبايل نازحه الحليفه", "قبائل نازحة")]
    [InlineData("فلسطيني بوثيقةلبناني", "فلسطيني بوثيقة لبناني")]
    [InlineData("فلسطيني بوثيقة لبناني", "فلسطيني بوثيقة لبناني")]
    [InlineData("روسيا الاتحادية", "روسيا")]
    [InlineData("ميانمار/جوازبنجلا دش", "ميانمار/جواز بنجلاديش")]
    [InlineData("حصر ن 1", NationalityNormalizer.Unspecified)]
    [InlineData("لا يوجد", NationalityNormalizer.Unspecified)]
    [InlineData("", NationalityNormalizer.Unspecified)]
    [InlineData("أوكرانيا", "اوكرانيا")]
    [InlineData("Mixed", "شركة مختلطة")]
    [InlineData("Foreign", "شركة اجنبية")]
    [InlineData("GULF", "شركة خليجية")]
    [InlineData("شركة أجنبيه", "شركة اجنبية")]
    public void Canonicalize_collapses_variants(string raw, string expected)
        => Assert.Equal(expected, NationalityNormalizer.Canonicalize(raw));
}
