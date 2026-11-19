using System.Text.RegularExpressions;

namespace AqarInsights.Api.Services.RentalControls.Reports;

/// <summary>The platform's full indicator names, applied to the report narrative wherever the workbook text abbreviates them.</summary>
public static class ReportTerminology
{
    // Word start + optional attached prefix (و/ف + ب/ك/ل, لل, ال); group 1 is the prefix.
    private const string Pfx = @"(?<![\p{L}])((?:و|ف)?(?:لل|(?:ب|ك|ل)?ال|ب|ك|ل)?)";
    // Same, but the definite article is mandatory (the prefix itself carries «ال»).
    private const string DefPfx = @"(?<![\p{L}])((?:و|ف)?(?:لل|(?:ب|ك|ل)?ال))";
    private const string WordEnd = @"(?![\p{L}])";

    // «ال» or «لل» (ل + ال) at the end of the prefix makes the term definite.
    private static bool Def(Match m) => m.Groups[1].Value.EndsWith("ال", StringComparison.Ordinal) || m.Groups[1].Value.EndsWith("لل", StringComparison.Ordinal);
    private static string Sakani(Match m) => m.Groups[1].Value + "عبء" + (Def(m) ? " السكني" : " سكني");

    private static readonly (Regex Pattern, MatchEvaluator Replace)[] Rules =
    {
        // "عبء ذوي الدخل المحدود" / "العبء ذوي الدخل المحدود"
        (Rx(Pfx + @"عبء\s+ذوي\s+الدخل\s+المحدود"), m => Sakani(m) + " لذوي الدخل المحدود"),
        // the income qualifier is already there: only "سكني" is missing
        (Rx(Pfx + @"عبء" + WordEnd + @"(?=\s+(?:لذوي|للدخل))"), Sakani),
        (Rx(Pfx + @"عبء" + WordEnd + @"(?!\s+(?:ال)?سكني)"), m => Sakani(m) + " لذوي الدخل المحدود"),
        // "العبء السكني" without the income qualifier
        (Rx(Pfx + @"عبء\s+(?:ال)?سكني" + WordEnd + @"(?!\s+(?:لذوي|للدخل))"), m => m.Value + " لذوي الدخل المحدود"),
        // the workbook's shorter form of the same name
        (Rx(@"(?<=عبء\s+(?:ال)?سكني)\s+للدخل\s+المحدود"), _ => " لذوي الدخل المحدود"),
        (Rx(Pfx + @"عائد\s+(?:من|على)\s+(?:ال)?استثمار"), m => m.Groups[1].Value + "عائد" + (Def(m) ? " الإيجاري" : " إيجاري")),
        (Rx(Pfx + @"عائد" + WordEnd + @"(?!\s+إيجاري|\s+الإيجاري)"), m => m.Groups[1].Value + "عائد" + (Def(m) ? " الإيجاري" : " إيجاري")),
        (Rx(DefPfx + @"شواغر" + WordEnd + @"(?!\s+الإيجارية)"), m => m.Groups[1].Value + "شواغر الإيجارية"),
        (Rx(@"(?<![\p{L}])شواغر" + WordEnd + @"(?!\s+(?:ال)?إيجارية)"), _ => "شواغر إيجارية"),
        (Rx(DefPfx + @"حد المرجعي(?! للمراقبة)"), m => m.Groups[1].Value + "حد المرجعي للمراقبة"),
        // pronoun / plural forms of the same reference: "دون حده المرجعي", "فوق حده", "عند حدودها المرجعية"
        (Rx(@"حده المرجعي(?! للمراقبة)"), _ => "الحد المرجعي للمراقبة"),
        (Rx(@"(?<=(?:فوق|دون|ضمن|عند|تحت|قرب)\s)حده" + WordEnd), _ => "الحد المرجعي للمراقبة"),
        (Rx(@"حدودها المرجعية"), _ => "الحد المرجعي للمراقبة"),
        // the platform's name of the compound growth indicator
        (Rx(@"(?:ال)?معدل السنوي المركب"), _ => "معدل النمو الإيجاري المركب"),
        // the rental-increase indicator written "الزيادة" alone
        (Rx(@"(?<![\p{L}])الزيادة(?=\s+(?:نطاق|إلى\s+نطاق|وصولًا))"), _ => "الزيادة الإيجارية"),
        // "وزيادة عند حافة النطاق" / "زيادة ضمن النطاق": the same indicator without its name
        (Rx(@"(?<![\p{L}])و?زيادة(?=\s+(?:عند|ضمن)\s+(?:حافة\s+)?(?:ال)?نطاق)"), m => m.Value.StartsWith('و') ? "والزيادة الإيجارية" : "الزيادة الإيجارية"),
        // the rank is by rental deals on the platform: say so
        (Rx(@"(ثاني|ثالث|رابع|خامس|سادس|سابع|ثامن|تاسع|عاشر)\s+أكبر\s+سوق\s+إيجاري"), m => m.Groups[1].Value + " أكبر مدينة من حيث الصفقات الإيجارية"),
        // "الإيجار الفعلي للمسكن (CPI)" → without the parentheses (the name is written "… CPI")
        (Rx(@"((?:ال)?إيجار (?:ال)?فعلي\s+للمسكن)\s*\(CPI\)"), m => m.Groups[1].Value + " CPI"),
        // "فعلي" alone for the actual rent (not the adverb «فعليًا»)
        (Rx(@"(?<!إيجار\s)(?<![\p{L}])فعلي" + WordEnd + @"(?![\u064B-\u0652])"), _ => "الإيجار الفعلي للمسكن CPI"),
        (Rx(@"((?:ال)?إيجار (?:ال)?فعلي)\s*\(CPI\)"), m => m.Groups[1].Value + " للمسكن CPI"),
        (Rx(@"((?:ال)?إيجار (?:ال)?فعلي)(?:\s+للمسكن)?(?!\s*(?:للمسكن\s*)?\(?CPI)"), m => m.Groups[1].Value + " للمسكن CPI"),
    };

    private static Regex Rx(string pattern) => new(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Apply(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        foreach (var (pattern, replace) in Rules) text = pattern.Replace(text, replace);
        // The platform writes "الإيجاري" with the hamza.
        return text.Replace("الايجار", "الإيجار", StringComparison.Ordinal);
    }

    public static List<string> Apply(IEnumerable<string> texts) => texts.Select(Apply).ToList();
}
