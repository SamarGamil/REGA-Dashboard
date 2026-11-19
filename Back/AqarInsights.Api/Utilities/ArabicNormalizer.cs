using System.Text.RegularExpressions;

namespace AqarInsights.Api.Utilities;

public static partial class ArabicNormalizer
{
    // Ta Marbuta ة and Ha ه
    private const char TaMarbuta = '\u0629'; // ة
    private const char Ha = '\u0647';        // ه

    // Alef variants
    private const char AlefMadda = '\u0622';    // آ
    private const char AlefHamzaAbove = '\u0623'; // أ
    private const char AlefHamzaBelow = '\u0625'; // إ
    private const char BareAlef = '\u0627';       // ا

    // Alef Maqsura ى → Ya ي
    private const char AlefMaqsura = '\u0649'; // ى
    private const char Ya = '\u064A';          // ي

    [GeneratedRegex(@"[\u064B-\u065F\u0670]")]
    private static partial Regex TashkeelRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        var result = text;

        // Remove diacritics (tashkeel)
        result = TashkeelRegex().Replace(result, "");

        // Normalize Alef variants → bare Alef
        result = result
            .Replace(AlefMadda, BareAlef)
            .Replace(AlefHamzaAbove, BareAlef)
            .Replace(AlefHamzaBelow, BareAlef);

        // Normalize Alef Maqsura → Ya
        result = result.Replace(AlefMaqsura, Ya);

        // Normalize Ha at end of word → Ta Marbuta
        // Match ه when followed by whitespace, end of string, or non-Arabic char
        var chars = result.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (chars[i] == Ha)
            {
                bool isEndOfWord = i == chars.Length - 1
                    || !IsArabicLetter(chars[i + 1]);

                if (isEndOfWord && i > 0 && IsArabicLetter(chars[i - 1]))
                {
                    chars[i] = TaMarbuta;
                }
            }
        }
        result = new string(chars);

        // Trim and collapse whitespace
        result = WhitespaceRegex().Replace(result.Trim(), " ");

        return result;
    }

    private static bool IsArabicLetter(char c)
    {
        // Arabic Unicode block: 0600-06FF (excluding diacritics 064B-065F)
        return c >= '\u0621' && c <= '\u064A' && c != TaMarbuta - 1; // basic Arabic letters
    }
}
