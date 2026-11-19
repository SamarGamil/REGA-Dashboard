using System.Text.Json;
using System.Text.RegularExpressions;
using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Models.RentalControls.Tables;
using AqarInsights.Api.Services.RentalControls.Projections;
using AqarInsights.Api.Services.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Snapshots;
using AqarInsights.Api.Services.RentalControls.Store;
using AqarInsights.Api.Services.RentalControls.Tables;
using AqarInsights.Api.Tests.RentalControls.Reports;

namespace AqarInsights.Api.Tests.RentalControls.Snapshots;

// The client-review wording standards hold for every reportable period: the live data and each stored snapshot
// (past-period reports are regenerated with the current layout, so their narratives must follow the same rules).
public class AllPeriodsWordingStandardsTests
{
    private static readonly (string Name, Regex Pattern)[] Forbidden =
    {
        ("short vacancy term", new Regex("الشواغر(?!\\s*الإيجارية)")),
        ("short yield term", new Regex("العائد(?!\\s*الإيجاري)")),
        ("band word as status", new Regex("\\((محايد|مستقر)\\)")),
        ("capital-pressure claim", new Regex("ضغط رأسمالي")),
        ("activation claim", new Regex("تم تفعيل الضوابط")),
        ("'at the limit' wording", new Regex("عند الحد")),
        ("yield 'minimum' wording", new Regex("الحد الأدنى")),
        ("non-canonical burden label", new Regex("العبء على ذوي|(?<![لا])لدخل المحدود")),
        ("contractual wording", new Regex("تعاقد")),
        ("proactive-path wording", new Regex("مسار استباقي|المسار الاستباقي")),
    };

    private static readonly Regex ImpressionClaim = new("الانطباع العام للرصد الإعلامي (?<s>[^\\s،.]+)");
    private static readonly Regex NoTension = new("لا مؤشرات احتقان|لا إشارات احتقان|دون انطباعات حادة");

    public static IEnumerable<object[]> Periods()
    {
        yield return new object[] { ReleaseIds.Live };
        foreach (var release in RealReportData.Repository.GetReleases().Where(r => r.Kind != ReleaseKinds.Live))
            yield return new object[] { release.Id };
    }

    private static MappedRelease Release(string id) => id == ReleaseIds.Live
        ? RealReportData.LiveRelease
        : ReleaseMapper.FromRows(RealReportData.Repository.LoadReleaseAsync(id).GetAwaiter().GetResult()!);

    [Theory, MemberData(nameof(Periods))]
    public void Narratives_follow_the_wording_standards(string period)
    {
        var violations = new List<string>();
        foreach (var (cityId, content) in Release(period).ReportContent)
            foreach (var text in Rendered(content))
                foreach (var (name, pattern) in Forbidden)
                    if (pattern.IsMatch(text))
                        violations.Add($"{cityId} [{name}]: {text}");
        Assert.Empty(violations);
    }

    // Media bullets carry no percentages and never contradict the media sentiment the period's report shows
    // (resolved through the period service, so a quarter is checked against its quarter-end month).
    [Theory, MemberData(nameof(ReportPeriods))]
    public void Media_bullets_match_the_report_sentiment(string period)
    {
        var data = RealReportData.Periods.ResolveAsync(period).GetAwaiter().GetResult()!;
        var violations = new List<string>();
        foreach (var city in data.Aggregation.GetOverview().Cities)
        {
            var media = data.Aggregation.GetCity(city.Id)?.MediaMonitoring;
            if (media is not { Available: true }) continue; // the report shows "no media data" instead of bullets
            var sentiment = string.IsNullOrWhiteSpace(media.OverallSentiment) ? "محايد" : media.OverallSentiment.Trim();
            foreach (var bullet in Shown(data, city.Id).MediaBullets)
            {
                if (bullet.Contains('%')) violations.Add($"{city.Id} [percentage]: {bullet}");
                if (ImpressionClaim.Match(bullet) is { Success: true } claim && claim.Groups["s"].Value != sentiment)
                    violations.Add($"{city.Id} [claims {claim.Groups["s"].Value}, report shows {sentiment}]: {bullet}");
                if (sentiment is "سلبي" or "متباين" && NoTension.IsMatch(bullet))
                    violations.Add($"{city.Id} ['no tension' with {sentiment}]: {bullet}");
            }
        }
        Assert.Empty(violations);
    }

    // Product deal / index percentages quoted in the narrative equal that period's data.
    [Theory, MemberData(nameof(ReportPeriods))]
    public void Market_figures_match_the_period_data(string period)
    {
        // Product deal / index percentages quoted in the narrative equal that period's data (client review:
        // Riyadh August still quoted the Q2 product figures above a Q3 table).
        var data = RealReportData.Periods.ResolveAsync(period).GetAwaiter().GetResult()!;
        var findings = data.Aggregation.GetOverview().Cities
            .SelectMany(c => NarrativeAuditor.Audit(data.Aggregation.GetCity(c.Id)!, data.Content.Get(c.Id)!).Select(f => $"{c.Id}: {f}"))
            .ToList();
        Assert.True(findings.Count == 0, $"{findings.Count} stale figure(s):\n" + string.Join("\n", findings));
    }

    // Rental-yield figures quoted in the narrative equal the yield the report shows, decimal included (client review,
    // Jeddah Q3: the narrative said 5% while the source is 5.33% and the recommendation line said 5.3%).
    [Theory, MemberData(nameof(ReportPeriods))]
    public void Yield_figures_match_the_report(string period)
    {
        var data = RealReportData.Periods.ResolveAsync(period).GetAwaiter().GetResult()!;
        var agg = data.Aggregation;
        var violations = new List<string>();
        foreach (var city in agg.GetOverview().Cities)
        {
            var shown = CityReportFactsBuilder.Build(city, agg.GetCity(city.Id)!, agg.GetIndicators().CurrentPeriodIndicators).InvestmentReturn.Value;
            foreach (var text in Rendered(data.Content.Get(city.Id)!))
                foreach (Match m in YieldNumber.Matches(text))
                    if (m.Groups["v"].Value + "%" != shown)
                        violations.Add($"{city.Id}: \"{m.Value}\" vs report {shown} in: {text}");
        }
        Assert.True(violations.Count == 0, $"{violations.Count} yield figure(s):\n" + string.Join("\n", violations));
    }

    // "العائد الإيجاري (5.3%)", "العائد 5.3%", and with a describing word in between ("المنخفض (4.2%)", "فوق الحد (12.1%)",
    // "الثابت دون الحد (4.2%)") — never the reference ("أقل من 6%").
    private static readonly Regex YieldNumber = new(
        "عائد(?: الإيجاري)?(?: (?:الصاعد|المنخفض|المرتفع|الثابت|الاستثماري|الأدنى وطنيًا|فوق الحد|دون الحد|نحو الحد المرجعي|تحسن إلى))*\\s*\\(?(?<v>\\d+(?:\\.\\d+)?)%");

    // Vacancy, rental-increase and actual-rent (CPI) figures in the narrative equal the report's (the live-data test
    // CityReportContentConsistencyTests, applied to every stored period).
    [Theory, MemberData(nameof(ReportPeriods))]
    public void Indicator_figures_match_the_report(string period)
    {
        var data = RealReportData.Periods.ResolveAsync(period).GetAwaiter().GetResult()!;
        var agg = data.Aggregation;
        var violations = new List<string>();
        foreach (var city in agg.GetOverview().Cities)
        {
            var f = CityReportFactsBuilder.Build(city, agg.GetCity(city.Id)!, agg.GetIndicators().CurrentPeriodIndicators);
            foreach (var text in Rendered(Shown(data, city.Id)))
            {
                // The narrative quotes the overview (quarter) vacancy; the report card shows the period month's series
                // value, which may differ or be "—".
                Check(VacancyNumber, text, city.VacancyRate, "vacancy");
                Check(IncreaseNumber, text, f.RentalIncrease.Value.TrimStart('+'), "rental increase");
                Check(ActualRentNumber, text, f.ActualRentCpi.Value.TrimStart('+'), "actual rent");
            }

            void Check(Regex pattern, string text, string shown, string what)
            {
                foreach (Match m in pattern.Matches(text))
                    if (m.Groups["v"].Value.TrimStart('+') + "%" != shown)
                        violations.Add($"{city.Id} {what}: \"{m.Value}\" vs report {shown} in: {text}");
            }
        }
        Assert.True(violations.Count == 0, $"{violations.Count} figure(s):\n" + string.Join("\n", violations));
    }

    private static readonly Regex VacancyNumber = new("الشواغر(?<gap>[^\\d%\"]{0,30})\\(?(?<v>\\d+(?:\\.\\d+)?)%");
    private static readonly Regex IncreaseNumber = new("الزيادة الإيجارية(?<gap>[^\\d%\"+-]{0,25})(?:[+-]?\\d+%\\s*(?:←|إلى)\\s*)?\\(?(?<v>[+-]?\\d+(?:\\.\\d+)?)%");
    private static readonly Regex ActualRentNumber = new("فعلي(?<gap>[^\\d%\"+-]{0,20})\\(?(?<v>[+-]?\\d+(?:\\.\\d+)?)%");

    // Inflation figures quoted in the narrative equal the general-inflation value the report shows.
    [Theory, MemberData(nameof(ReportPeriods))]
    public void Inflation_figures_match_the_report(string period)
    {
        var data = RealReportData.Periods.ResolveAsync(period).GetAwaiter().GetResult()!;
        var agg = data.Aggregation;
        var violations = new List<string>();
        foreach (var city in agg.GetOverview().Cities)
        {
            var facts = CityReportFactsBuilder.Build(city, agg.GetCity(city.Id)!, agg.GetIndicators().CurrentPeriodIndicators);
            var shown = double.Parse(facts.GeneralInflation.Value.TrimStart('+').TrimEnd('%'), System.Globalization.CultureInfo.InvariantCulture);
            foreach (var text in Rendered(Shown(data, city.Id)))
                foreach (Match m in InflationNumber.Matches(text))
                    // The report shows the series' two decimals; the narrative quotes it to one.
                    if (Math.Abs(double.Parse(m.Groups["v"].Value.TrimStart('+'), System.Globalization.CultureInfo.InvariantCulture) - shown) > 0.0501)
                        violations.Add($"{city.Id}: \"{m.Value}\" vs report {facts.GeneralInflation.Value}");
        }
        Assert.Empty(violations);
    }

    private static readonly Regex InflationNumber = new("تضخم(?<gap>[^\\d%\"+-]{0,20})\\(?(?<v>[+-]?\\d+(?:\\.\\d+)?)%");

    // The narrative as the period's report shows it (text written for a month outside the period is masked).
    private static CityReportContent Shown(RentalControlsPeriodData data, string cityId)
        => CityReportModelBuilder.ForPeriod(data.Content.Get(cityId)!, data.Scope,
            MonthlyCpiStatement.ForView(data.Aggregation.GetCity(cityId)!, data.Scope));

    // Periods whose narrative is shown unfiltered (quarter views and the text month); other months render a subset of
    // their quarter's narrative (checked under the quarter) without its monthly statements.
    public static IEnumerable<object[]> ReportPeriods()
    {
        var periods = RealReportData.Periods.GetPeriods();
        return periods.Monthly.Concat(periods.Quarterly)
            .Where(p => RealReportData.Periods.ResolveAsync(p.Id).GetAwaiter().GetResult()!.Scope is var s
                && (s.Month is null || s.Month == s.TextMonth))
            .Select(p => new object[] { p.Id });
    }

    // Only the fields the report renders (old snapshots may carry unused legacy fields).
    private static IEnumerable<string> Rendered(CityReportContent c) => c.ExecutiveBullets
        .Concat(c.IndicatorsBullets).Concat(c.MarketBullets).Concat(c.MediaBullets).Concat(c.ConclusionBullets)
        .Concat(c.RecommendationBullets).Concat(c.WatchList).Concat(c.AssessmentAxes.Select(a => a.Status))
        .Append(c.CityAveragesLine ?? "");
}
