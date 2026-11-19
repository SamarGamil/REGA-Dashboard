using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Reports;
using Xunit;

namespace AqarInsights.Api.Tests.RentalControls.Reports;

public class ReportThresholdsTests
{
    private static CityReportIndicatorFact F(string label, string reference) => new() { Label = label, Reference = reference };

    private static readonly IReadOnlyList<ReportThresholds.Indicator> Refs = ReportThresholds.From(new CityReportFacts
    {
        Cagr = F("معدل النمو الإيجاري المركب", "7%–9%"),
        RentalIncrease = F("الزيادة الإيجارية", "أكبر من 6%"),
        RentToIncome = F("نسبة الإيجار إلى الدخل", "أكبر من 30%"),
        HousingBurden = F("العبء السكني لذوي الدخل المحدود", "أكبر من 30%"),
        Vacancy = F("الشواغر الإيجارية", "أقل من 5%"),
        InvestmentReturn = F("العائد الإيجاري", "أقل من 6%"),
        ActualRentCpi = F("الإيجار الفعلي للمسكن CPI", "—"),
        GeneralInflation = F("التضخم العام (CPI)", "—"),
    });

    private static string Apply(string text, string? fallback = null) => ReportThresholds.Apply(text, Refs, fallback);

    [Theory]
    // Client review (Riyadh, August 2026), notes 8, 13, 15 and 17.
    [InlineData("العائد الإيجاري (5.1%) دون الحد.", "العائد الإيجاري (5.1%) دون الحد المرجعي للمراقبة (6%).")]
    [InlineData("نسبة الإيجار إلى الدخل ضمن الحد (15%).", "نسبة الإيجار إلى الدخل (15%) ضمن الحد المرجعي للمراقبة (30%).")]
    [InlineData("معدل النمو الإيجاري المركب 8.9% (نمو مرتفع) والعبء السكني لذوي الدخل المحدود 35% يتجاوز الحد.",
        "معدل النمو الإيجاري المركب 8.9% (نمو مرتفع) والعبء السكني لذوي الدخل المحدود 35% يتجاوز الحد المرجعي للمراقبة (30%).")]
    [InlineData("مسار معدل النمو الإيجاري المركب (حد 7-9%).", "مسار معدل النمو الإيجاري المركب (الحد المرجعي للمراقبة 7%–9%).")]
    // Each «الحد» takes the threshold of the indicator named closest before it.
    [InlineData("العائد الإيجاري (4.1%) دون الحد، والعبء السكني لذوي الدخل المحدود (32%) فوق الحد بهامش ضيق.",
        "العائد الإيجاري (4.1%) دون الحد المرجعي للمراقبة (6%)، والعبء السكني لذوي الدخل المحدود (32%) فوق الحد المرجعي للمراقبة (30%) بهامش ضيق.")]
    [InlineData("العبء السكني لذوي الدخل المحدود (36%) فوق الحد وارتد صعودًا (34% ← 36%).",
        "العبء السكني لذوي الدخل المحدود (36%) فوق الحد المرجعي للمراقبة (30%) وارتد صعودًا (34% ← 36%).")]
    [InlineData("استقرار العائد الإيجاري فوق الحد (12.1%).", "استقرار العائد الإيجاري (12.1%) فوق الحد المرجعي للمراقبة (6%).")]
    [InlineData("مراجعة فورية لتجاوز العبء السكني لذوي الدخل المحدود الحد (31%).",
        "مراجعة فورية لتجاوز العبء السكني لذوي الدخل المحدود (31%) الحد المرجعي للمراقبة (30%).")]
    [InlineData("عودة العبء السكني لذوي الدخل المحدود إلى الحد أو دونه (31% حاليًا).",
        "عودة العبء السكني لذوي الدخل المحدود إلى الحد المرجعي للمراقبة (30%) أو دونه (31% حاليًا).")]
    // The threshold already quoted later in the clause is not repeated.
    [InlineData("استقرار بلا هامش: العبء السكني لذوي الدخل المحدود على الحد بالضبط (30%) مع أطول موجة — أي زخم جديد.",
        "استقرار بلا هامش: العبء السكني لذوي الدخل المحدود على الحد المرجعي للمراقبة بالضبط (30%) مع أطول موجة — أي زخم جديد.")]
    // A label that already carries its threshold is left as is.
    [InlineData("العائد الإيجاري (4.5%) دون الحد المرجعي للمراقبة (6%).", "العائد الإيجاري (4.5%) دون الحد المرجعي للمراقبة (6%).")]
    // A colon followed by prose is not a threshold.
    [InlineData("والعائد الإيجاري (8.5%) فوق الحد المرجعي: وفرة المعروض.", "والعائد الإيجاري (8.5%) فوق الحد المرجعي للمراقبة (6%): وفرة المعروض.")]
    // A year closing the previous sentence ends it: its indicator does not lend its threshold to the next one.
    [InlineData("معدل النمو الإيجاري المركب 8.9% خلال 2022-2026. المؤشرات عند الحد بالضبط.",
        "معدل النمو الإيجاري المركب 8.9% خلال 2022-2026. المؤشرات عند الحد المرجعي للمراقبة بالضبط.")]
    // A threshold written inside the parentheses follows the platform's reference of the named indicator.
    [InlineData("مسار الزيادة الإيجارية (حد 10%).", "مسار الزيادة الإيجارية (الحد المرجعي للمراقبة 6%).")]
    [InlineData("العبء السكني لذوي الدخل المحدود (35%) يتجاوز الحد المرجعي للمراقبة.",
        "العبء السكني لذوي الدخل المحدود (35%) يتجاوز الحد المرجعي للمراقبة (30%).")]
    // Inside parentheses the threshold is not wrapped again.
    [InlineData("الشواغر الإيجارية 7% (ضمن الحد) والعائد الإيجاري 5.4% (دون الحد).",
        "الشواغر الإيجارية 7% (ضمن الحد المرجعي للمراقبة 5%) والعائد الإيجاري 5.4% (دون الحد المرجعي للمراقبة 6%).")]
    // Several indicators: the plural reference, without a single threshold.
    [InlineData("الزيادة الإيجارية والعبء السكني لذوي الدخل المحدود ضمن الحدود ومستقران.",
        "الزيادة الإيجارية والعبء السكني لذوي الدخل المحدود ضمن الحدود المرجعية للمراقبة ومستقران.")]
    // No indicator named in the sentence: the label only.
    [InlineData("العبء السكني لذوي الدخل المحدود مرتفع (35%). المؤشرات عند الحد المرجعي للمراقبة بالضبط.",
        "العبء السكني لذوي الدخل المحدود مرتفع (35%). المؤشرات عند الحد المرجعي للمراقبة بالضبط.")]
    // Not a reference: «الحد من» (curbing), «إلى حد كبير».
    [InlineData("تسهم الضوابط في الحد من ارتفاع الإيجارات إلى حد كبير.", "تسهم الضوابط في الحد من ارتفاع الإيجارات إلى حد كبير.")]
    // An indicator without a reference (CPI) gets the label only.
    [InlineData("الإيجار الفعلي للمسكن CPI فوق الحد.", "الإيجار الفعلي للمسكن CPI فوق الحد المرجعي للمراقبة.")]
    public void Every_reference_reads_the_monitoring_label_with_the_indicator_threshold(string text, string expected)
    {
        Assert.Equal(expected, Apply(text));
        Assert.Equal(expected, Apply(expected));
    }

    [Theory]
    // An assessment axis names no indicator: it takes the axis indicator's threshold, and a value written where the
    // threshold belongs is kept after it.
    [InlineData("دون الحد (4.2%)", "6%", "دون الحد المرجعي للمراقبة (6%) عند 4.2%")]
    [InlineData("فوق الحد المرجعي (8.5%)", "6%", "فوق الحد المرجعي للمراقبة (6%) عند 8.5%")]
    [InlineData("فوق الحد ويرتد صعودًا", "30%", "فوق الحد المرجعي للمراقبة (30%) ويرتد صعودًا")]
    [InlineData("متوازن", "5%", "متوازن")]
    public void Axis_status_uses_the_fallback_threshold(string text, string fallback, string expected)
    {
        Assert.Equal(expected, Apply(text, fallback));
        Assert.Equal(expected, Apply(expected, fallback));
    }

    // The compound growth reference is a classification band: it keeps its qualifier, as the KPI card prints it.
    [Theory]
    [InlineData("10% فأكثر", "مسار معدل النمو الإيجاري المركب (الحد المرجعي للمراقبة 7%–9%).", "مسار معدل النمو الإيجاري المركب (الحد المرجعي للمراقبة 10% فأكثر).")]
    [InlineData("7%–9%", "مسار معدل النمو الإيجاري المركب (حد 7-9%).", "مسار معدل النمو الإيجاري المركب (الحد المرجعي للمراقبة 7%–9%).")]
    public void Compound_growth_reference_is_its_band(string band, string text, string expected)
    {
        var refs = ReportThresholds.From(new CityReportFacts { Cagr = F("معدل النمو الإيجاري المركب", band) });
        Assert.Equal(expected, ReportThresholds.Apply(text, refs));
        Assert.Equal(expected, ReportThresholds.Apply(expected, refs));
    }

    [Theory]
    [InlineData("أكبر من 30%", "30%")]
    [InlineData("أقل من 6%", "6%")]
    [InlineData("7%–9%", "7%–9%")]
    [InlineData("6% فأقل", "6%")]
    [InlineData("10% فأكثر", "10%")]
    [InlineData("≤6%", "6%")]
    [InlineData("> 30%", "30%")]
    [InlineData("—", "")]
    [InlineData("", "")]
    public void Threshold_is_the_reference_figure(string reference, string expected)
        => Assert.Equal(expected, ReportThresholds.Threshold(reference));

    [Theory]
    [InlineData("ضمن الحد", "ضمن الحد المرجعي للمراقبة")]
    [InlineData("دون الحد", "دون الحد المرجعي للمراقبة")]
    [InlineData("متجاوز", "متجاوز")]
    [InlineData("نمو مرتفع", "نمو مرتفع")]
    public void Status_words_name_the_monitoring_reference(string status, string expected)
        => Assert.Equal(expected, ReportThresholds.StatusText(status));
}
