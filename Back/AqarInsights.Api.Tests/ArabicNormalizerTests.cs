using AqarInsights.Api.Utilities;

namespace AqarInsights.Api.Tests;

public class ArabicNormalizerTests
{
    [Theory]
    [InlineData("المدينة المنوره", "المدينة المنورة")]
    [InlineData("المدينة المنورة", "المدينة المنورة")]
    [InlineData("مكه المكرمه", "مكة المكرمة")]
    [InlineData("مكة المكرمة", "مكة المكرمة")]
    public void Normalize_TaMarbuta_ReplacesHaAtEndOfWord(string input, string expected)
    {
        Assert.Equal(expected, ArabicNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("إبراهيم", "ابراهيم")]
    [InlineData("أحمد", "احمد")]
    [InlineData("آخر", "اخر")]
    [InlineData("ابراهيم", "ابراهيم")]
    public void Normalize_AlefVariants_NormalizesToBareAlef(string input, string expected)
    {
        Assert.Equal(expected, ArabicNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("مستشفى", "مستشفي")]
    [InlineData("موسى", "موسي")]
    [InlineData("مستشفي", "مستشفي")]
    public void Normalize_AlefMaqsura_NormalizesToYa(string input, string expected)
    {
        Assert.Equal(expected, ArabicNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("مَدِينَة", "مدينة")]
    public void Normalize_Tashkeel_RemovesDiacritics(string input, string expected)
    {
        Assert.Equal(expected, ArabicNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("  المدينة   المنوره  ", "المدينة المنورة")]
    [InlineData("المدينة\tالمنوره", "المدينة المنورة")]
    public void Normalize_Whitespace_TrimsAndCollapses(string input, string expected)
    {
        Assert.Equal(expected, ArabicNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void Normalize_EmptyOrNull_ReturnsEmpty(string? input)
    {
        Assert.Equal("", ArabicNormalizer.Normalize(input));
    }

    [Fact]
    public void Normalize_HaInsideWord_DoesNotReplace()
    {
        // ه inside a word (e.g., "شهر" = month) should NOT become ة
        var result = ArabicNormalizer.Normalize("شهر");
        Assert.Equal("شهر", result);
    }

    [Fact]
    public void Normalize_CombinedIssues_NormalizesAll()
    {
        // Multiple issues in one string
        var result = ArabicNormalizer.Normalize("  إمارة مكه المكرمه  ");
        Assert.Equal("امارة مكة المكرمة", result);
    }
}
