using System.Text.RegularExpressions;
using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Projections;
using AqarInsights.Api.Services.RentalControls.Reports;
using AqarInsights.Api.Tests.RentalControls.Reports;

namespace AqarInsights.Api.Tests.RentalControls.Snapshots;

// Client review (Riyadh and national reports, August 2026), checked on every period's city reports as generated:
// each reference reads «الحد المرجعي للمراقبة» (with its threshold), the yield shows one status everywhere, and a
// point change names the period it is measured against.
public class ReportReferenceStandardsTests
{
    private static readonly DateOnly Today = new(2026, 10, 1);

    public static IEnumerable<object[]> PeriodIds() => PeriodGoldenTests.PeriodIds();

    // «الحد» / «للحد» not followed by «المرجعي للمراقبة» (but not «الحد من»), "(حد …)", and «الحدود» without its label.
    private static readonly Regex BareReference = new(
        @"(?<![\p{L}])(?:و|ف)?(?:ب|ك)?(?:ال|لل)حد(?![\p{L}])(?! المرجعي للمراقبة)(?!\s+من(?![\p{L}]))|\(حد\s|(?<![\p{L}])(?:و|ف)?(?:ب|ك|ل)?الحدود(?![\p{L}])(?! المرجعية للمراقبة)");
    // "بتراجع 0.8 نقطة" not followed by the period it is measured against ("عن الربع السابق").
    private static readonly Regex PointChange = new(@"(?:بتراجع|بارتفاع|بانخفاض|بزيادة)\s+\d+(?:\.\d+)?\s+(?:نقطة|نقاط|نقطتين)(?:\s+مئوية)?(?!\s+(?:مئوية\s+)?عن(?![\p{L}]))");

    private static async Task<List<(string CityId, CityReportModel Report)>> Reports(string periodId)
    {
        var data = (await RealReportData.Periods.ResolveAsync(periodId == "default" ? null : periodId))!;
        var agg = data.Aggregation;
        var indicators = PeriodValueMask.Apply(agg.GetIndicators(), data.Scope);
        var reports = new List<(string, CityReportModel)>();
        foreach (var item in agg.GetCityIndex())
        {
            if (agg.GetCity(item.CityId) is not { } source) continue;
            var city = PeriodValueMask.Apply(source, data.Scope);
            var content = data.Content.Get(item.CityId);
            var overviewCity = agg.GetOverview().Cities.FirstOrDefault(c => c.Id == item.CityId);
            if (content is null || overviewCity is null) continue;
            var facts = CityReportFactsBuilder.Build(overviewCity, city, indicators.CurrentPeriodIndicators);
            reports.Add((item.CityId, CityReportModelBuilder.Build(city, facts,
                CityReportModelBuilder.ForPeriod(content, data.Scope, MonthlyCpiStatement.ForView(city, data.Scope)), Today, data.Label, data.IsMonthly)));
        }
        return reports;
    }

    // Every text a city report prints.
    private static IEnumerable<string> Texts(CityReportModel m) => m.ExecutiveBullets
        .Concat(m.IndicatorsBullets).Concat(m.MarketBullets).Concat(m.MediaBullets).Concat(m.ConclusionBullets)
        .Concat(m.RecommendationBullets).Concat(m.WatchList).Append(m.CityAveragesLine)
        .Concat(m.KpiCards.SelectMany(c => new[] { c.Label, c.Note, c.Reference }))
        .Concat(m.IndicatorRows.SelectMany(r => new[] { r.Indicator, r.Status }))
        .Concat(m.SupplyInvestment.SelectMany(s => new[] { s.Label, s.Note, s.Reference }))
        .Concat(m.Axes.SelectMany(a => new[] { a.Axis, a.Status }));

    [Theory, MemberData(nameof(PeriodIds))]
    public async Task Every_reference_is_the_monitoring_reference(string periodId)
    {
        var violations = (await Reports(periodId))
            .SelectMany(r => Texts(r.Report).Where(t => BareReference.IsMatch(t ?? "") || RepeatedThreshold.IsMatch(t ?? "")).Select(t => $"{r.CityId}: {t}"))
            .ToList();
        Assert.True(violations.Count == 0, $"{violations.Count} bare or repeated reference(s):\n" + string.Join("\n", violations));
    }

    // "الحد المرجعي للمراقبة (30%) بالضبط (30%)": the same threshold quoted twice within a few words; and a label followed
    // by a colon and prose instead of its threshold.
    private static readonly Regex RepeatedThreshold = new(@"الحد المرجعي للمراقبة \((?<t>[^()]+)\)(?:\s+[^\s()]+){0,3}\s+\(\k<t>\)|الحد المرجعي للمراقبة:(?!\s*(?:[<>≤≥]|أقل من|أكبر من|أكثر من|\d))");

    [Theory, MemberData(nameof(PeriodIds))]
    public async Task Actual_rent_shows_one_colour_in_every_place(string periodId)
    {
        var violations = new List<string>();
        foreach (var (cityId, m) in await Reports(periodId))
        {
            var card = m.KpiCards.Single(c => c.Label.StartsWith("الإيجار الفعلي")).ColorHex;
            var row = m.IndicatorRows.Single(r => r.Indicator.StartsWith("الإيجار الفعلي")).StatusColorHex;
            var supply = m.SupplyInvestment.Single(s => s.Label.StartsWith("الإيجار الفعلي")).ColorHex;
            if (card != row || card != supply) violations.Add($"{cityId}: card {card}, table {row}, supply {supply}");
        }
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    [Theory, MemberData(nameof(PeriodIds))]
    public async Task Yield_shows_one_status_in_every_place(string periodId)
    {
        var violations = new List<string>();
        foreach (var (cityId, m) in await Reports(periodId))
        {
            var card = m.KpiCards.Single(c => c.Label == "العائد الإيجاري");
            var supply = m.SupplyInvestment.Single(s => s.Label == "العائد الإيجاري");
            if ((card.Note, card.ColorHex) != (supply.Note, supply.ColorHex))
                violations.Add($"{cityId}: card «{card.Note}» vs supply «{supply.Note}»");
            var axis = m.Axes.FirstOrDefault(a => a.Axis == "الاستثمار")?.Status ?? "";
            if (supply.Note.StartsWith("دون ", StringComparison.Ordinal) && !axis.Contains(supply.Note, StringComparison.Ordinal))
                violations.Add($"{cityId}: supply «{supply.Note}» vs axis «{axis}»");
        }
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    [Theory, MemberData(nameof(PeriodIds))]
    public async Task Point_changes_name_their_period_and_stay_out_of_monthly_reports(string periodId)
    {
        var monthly = Regex.IsMatch(periodId, @"^\d{4}-\d{2}$");
        var violations = (await Reports(periodId))
            .SelectMany(r => Texts(r.Report)
                .Where(t => monthly ? AnyQuarterChange.IsMatch(t ?? "") : PointChange.IsMatch(t ?? ""))
                .Select(t => $"{r.CityId}: {t}"))
            .ToList();
        Assert.True(violations.Count == 0,
            $"{violations.Count} point change(s) {(monthly ? "in a monthly report" : "without a period")}:\n" + string.Join("\n", violations));
    }

    private static readonly Regex AnyQuarterChange = new(@"(?:بتراجع|بارتفاع|بانخفاض|بزيادة)\s+\d+(?:\.\d+)?\s+(?:نقطة|نقاط|نقطتين)|دون\s+تغير\s+عن\s+الربع|الربع\s+(?:السابق|الماضي)|في\s+الربع\s+(?:الأول|الثاني|الثالث|الرابع)[^.،]*←");
}
