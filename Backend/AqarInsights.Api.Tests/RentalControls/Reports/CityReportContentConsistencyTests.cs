using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Reports;

namespace AqarInsights.Api.Tests.RentalControls.Reports;

// Lints every city narrative against platform data, per the client review of the city report.
public class CityReportContentConsistencyTests
{
    private static readonly JsonSerializerOptions TextOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    // The gap excludes '"' so a match never spans two JSON strings.
    private static readonly Regex YieldNumber = new(@"عائد(?<gap>[^\d%""]{0,12})\(?(?<v>\d+(?:\.\d+)?)%");
    private static readonly Regex VacancyNumber = new(@"الشواغر(?<gap>[^\d%""]{0,30})\(?(?<v>\d+(?:\.\d+)?)%");
    // For "من 21% إلى 7%" / "(11% ← 5%)" the checked value is the latest one; gaps exclude +/- so a sign stays with its number.
    private static readonly Regex IncreaseNumber = new(@"الزيادة الإيجارية(?<gap>[^\d%""+-]{0,25})(?:[+-]?\d+%\s*(?:←|إلى)\s*)?\(?(?<v>[+-]?\d+(?:\.\d+)?)%");
    private static readonly Regex InflationNumber = new(@"تضخم(?<gap>[^\d%""+-]{0,20})\(?(?<v>[+-]?\d+(?:\.\d+)?)%");
    private static readonly Regex ActualRentNumber = new(@"فعلي(?<gap>[^\d%""+-]{0,20})\(?(?<v>[+-]?\d+(?:\.\d+)?)%");
    private static readonly Regex FastestRentClaim = new(@"أسرع[^""]{0,20}وطنيًا|صدارة المدن");
    private static readonly Regex RankClaim = new(@"(?:(?<ord>ثاني|ثالث|رابع|خامس)\s+)?أكبر مدينة من حيث الصفقات الإيجارية");
    private static readonly string[] Ordinals = { "", "", "ثاني", "ثالث", "رابع", "خامس" };
    private static readonly string[] NoActionPhrases = { "لا حاجة لأي إجراء", "دون إجراء", "لا حاجة لأي تدخل", "لا تدخل مطلوب", "دون تدخل" };

    public static IEnumerable<object[]> CityIds() => RealReportData.LiveRelease.ReportContent.Keys
        .Order(StringComparer.Ordinal)
        .Select(id => new object[] { id });

    [Theory, MemberData(nameof(CityIds))]
    public void Does_not_use_contractual_wording(string cityId)
        => Assert.Empty(Snippets(Text(cityId), "تعاقد"));

    [Theory, MemberData(nameof(CityIds))]
    public void Does_not_use_proactive_path_wording(string cityId)
    {
        var text = Text(cityId);
        Assert.Empty(Snippets(text, "مسار استباقي").Concat(Snippets(text, "المسار الاستباقي")).ToList());
    }

    // The yield is judged against the 6% reference ("أقل من 6%"), never a "minimum".
    [Theory, MemberData(nameof(CityIds))]
    public void Does_not_describe_yield_against_a_minimum(string cityId)
        => Assert.Empty(Snippets(Text(cityId), "الحد الأدنى"));

    // Client review (Buraidah): narrative figures must follow the imported quarter. Shared logic with the
    // import-time warnings: NarrativeAuditor.
    [Theory, MemberData(nameof(CityIds))]
    public void Prose_deal_and_index_percentages_match_platform(string cityId)
        => Assert.Empty(NarrativeAuditor.Audit(RealReportData.Aggregation.GetCity(cityId)!, Content(cityId)));

    [Theory, MemberData(nameof(CityIds))]
    public void Uses_canonical_burden_label(string cityId)
        => Assert.Empty(Snippets(Text(cityId), "العبء على ذوي"));

    // Client review: always the full terms "الشواغر الإيجارية" and "العائد الإيجاري".
    [Theory, MemberData(nameof(CityIds))]
    public void Uses_full_vacancy_and_yield_terms(string cityId)
    {
        var text = Text(cityId);
        var shortTerms = Regex.Matches(text, "الشواغر(?!\\s*الإيجارية)|العائد(?!\\s*الإيجاري)")
            .Select(m => text.Substring(Math.Max(0, m.Index - 20), Math.Min(50, text.Length - Math.Max(0, m.Index - 20))))
            .ToList();
        Assert.Empty(shortTerms);
    }

    // Client review: no unsupported "capital pressure" claims, and limit wording must follow the policy status.
    [Theory, MemberData(nameof(CityIds))]
    public void Does_not_use_capital_pressure_or_at_limit_wording(string cityId)
    {
        var text = Text(cityId);
        Assert.Empty(Snippets(text, "ضغط رأسمالي").Concat(Snippets(text, "عند الحد")).ToList());
    }

    [Theory, MemberData(nameof(CityIds))]
    public void Does_not_claim_no_action_when_platform_requires_action(string cityId)
    {
        if (Platform(cityId).OperationalStatus == "لا إجراء") return;
        var text = Text(cityId);
        Assert.Empty(NoActionPhrases.SelectMany(p => Snippets(text, p)).ToList());
    }

    [Theory, MemberData(nameof(CityIds))]
    public void Yield_numbers_match_platform(string cityId)
        => Assert.Empty(Mismatches(YieldNumber, Text(cityId), Platform(cityId).InvestmentReturn));

    [Theory, MemberData(nameof(CityIds))]
    public void Vacancy_numbers_match_platform(string cityId)
        => Assert.Empty(Mismatches(VacancyNumber, Text(cityId), Platform(cityId).VacancyRate));

    [Theory, MemberData(nameof(CityIds))]
    public void Rental_increase_numbers_match_platform(string cityId)
        => Assert.Empty(Mismatches(IncreaseNumber, Text(cityId), Platform(cityId).ActualRentalGrowth.TrimStart('+')));

    [Theory, MemberData(nameof(CityIds))]
    public void Inflation_and_actual_rent_numbers_match_platform(string cityId)
    {
        var text = Text(cityId);
        var facts = Facts(cityId);
        // The report shows the series' two decimals; the narrative quotes the same figure to one.
        var inflation = double.Parse(facts.GeneralInflation.Value.TrimStart('+').TrimEnd('%'), System.Globalization.CultureInfo.InvariantCulture);
        Assert.Empty(Mismatches(InflationNumber, text, Math.Round(inflation, 1, MidpointRounding.AwayFromZero).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%")
            .Concat(Mismatches(ActualRentNumber, text, facts.ActualRentCpi.Value)).ToList());
    }

    [Theory, MemberData(nameof(CityIds))]
    public void Fastest_actual_rent_claims_match_platform(string cityId)
    {
        var cities = RealReportData.Aggregation.GetMarketAnalysis().Cities;
        var fastest = cities.MaxBy(c => c.ActualRentCpi ?? double.MinValue)!.Id;
        if (cityId == fastest) return;
        Assert.Empty(FastestRentClaim.Matches(Text(cityId)).Select(m => $"{m.Value} (fastest on platform: {fastest})").ToList());
    }

    [Theory, MemberData(nameof(CityIds))]
    public void Does_not_recommend_detailed_study_unless_platform_does(string cityId)
    {
        if (Platform(cityId).OperationalStatus == "يتطلب دراسة تفصيلية") return;
        Assert.Empty(Snippets(Text(cityId), "دراسة تفصيلية"));
    }

    [Theory, MemberData(nameof(CityIds))]
    public void Media_bullets_carry_no_percentages(string cityId)
        => Assert.Empty(Content(cityId).MediaBullets.Where(b => b.Contains('%')).ToList());

    [Theory, MemberData(nameof(CityIds))]
    public void Rental_market_rank_claims_match_platform(string cityId)
    {
        var cities = RealReportData.Aggregation.GetMarketAnalysis().Cities;
        var own = cities.Single(c => c.Id == cityId).RentalTransactionCount;
        var rank = 1 + cities.Count(c => c.RentalTransactionCount > own);
        var wrong = RankClaim.Matches(Text(cityId))
            .Where(m => (m.Groups["ord"].Success ? Array.IndexOf(Ordinals, m.Groups["ord"].Value) : 1) != rank)
            .Select(m => $"{m.Value} (platform rank {rank})")
            .ToList();
        Assert.Empty(wrong);
    }

    private static CityReportContent Content(string cityId)
        => RealReportData.LiveContent.Get(cityId)
           ?? throw new InvalidOperationException($"No city-report content for '{cityId}'");

    private static string Text(string cityId) => JsonSerializer.Serialize(Content(cityId), TextOptions);

    private static CityData Platform(string cityId)
        => RealReportData.Aggregation.GetOverview().Cities.Single(c => c.Id == cityId);

    private static CityReportFacts Facts(string cityId)
    {
        var agg = RealReportData.Aggregation;
        return CityReportFactsBuilder.Build(Platform(cityId), agg.GetCity(cityId)!, agg.GetIndicators().CurrentPeriodIndicators);
    }

    // "…context…" around each occurrence, so a failure names the phrase to edit.
    private static List<string> Snippets(string text, string needle)
    {
        var found = new List<string>();
        for (var i = text.IndexOf(needle, StringComparison.Ordinal); i >= 0; i = text.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
        {
            var start = Math.Max(0, i - 30);
            found.Add(text[start..Math.Min(text.Length, i + needle.Length + 30)]);
        }
        return found;
    }

    // Numbers after a label that differ from the platform value; "أقل من 6%"-style references are skipped.
    private static List<string> Mismatches(Regex pattern, string text, string platformValue)
        => pattern.Matches(text)
            .Where(m => !m.Groups["gap"].Value.Contains("أقل من") && !m.Groups["gap"].Value.Contains("أكبر من"))
            .Where(m => m.Groups["v"].Value.TrimStart('+') + "%" != platformValue.TrimStart('+'))
            .Select(m => $"{m.Value} (platform {platformValue})")
            .ToList();
}
