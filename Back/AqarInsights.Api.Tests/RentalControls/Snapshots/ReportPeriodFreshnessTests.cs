using System.Text.RegularExpressions;
using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Projections;
using AqarInsights.Api.Services.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Snapshots;
using AqarInsights.Api.Tests.RentalControls.Reports;

namespace AqarInsights.Api.Tests.RentalControls.Snapshots;

// Every report shows the data of its selected period, never an earlier (stale) or later one: CPI of the report month
// (a quarter: its last month), media of the report month (a quarter: a month within it), the deal tables of the
// period's quarter, and a narrative that names no later month or quarter.
public class ReportPeriodFreshnessTests
{
    private static readonly string[] MonthNames =
        { "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو", "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر" };
    private static readonly string[] QuarterNames = { "الأول", "الثاني", "الثالث", "الرابع" };
    private static readonly Regex MonthMention = new(@"(?<m>يناير|فبراير|مارس|أبريل|مايو|يونيو|يوليو|أغسطس|سبتمبر|أكتوبر|نوفمبر|ديسمبر)\s+(?<y>\d{4})");
    private static readonly Regex QuarterMention = new(@"الربع\s+(?<q>الأول|الثاني|الثالث|الرابع)\s+(?<y>\d{4})");

    public static IEnumerable<object[]> Periods()
    {
        var periods = RealReportData.Periods.GetPeriods();
        return periods.Monthly.Concat(periods.Quarterly).Select(p => new object[] { p.Id });
    }

    [Theory, MemberData(nameof(Periods))]
    public void Report_values_belong_to_the_selected_period(string period)
    {
        var data = RealReportData.Periods.ResolveAsync(period).GetAwaiter().GetResult()!;
        var agg = data.Aggregation;
        var (first, last) = YearMonth.TryParse(period, out var month)
            ? (month, month)
            : YearQuarter.TryParse(period, out var q) ? (q.FirstMonth, q.LastMonth) : throw new ArgumentException(period);
        // A month past the newest quarter (its quarterly workbooks have not arrived) shows the newest quarter's tables.
        var current = YearQuarter.TryParse(RealReportData.Periods.GetPeriods().Current, out var cq) ? cq : last.Quarter;
        var quarter = last.Quarter <= current ? last.Quarter : current;
        bool InPeriod(YearMonth m) => m >= first && m <= last;
        var violations = new List<string>();

        var panels = PeriodValueMask.Apply(agg.GetIndicators(), data.Scope).CurrentPeriodIndicators;
        foreach (var overview in agg.GetOverview().Cities)
        {
            // The city report exactly as CityReportService builds it for the period.
            var city = PeriodValueMask.Apply(agg.GetCity(overview.Id)!, data.Scope);
            var facts = CityReportFactsBuilder.Build(overview, city, panels);
            var model = CityReportModelBuilder.Build(city, facts,
                CityReportModelBuilder.ForPeriod(data.Content.Get(overview.Id)!, data.Scope, MonthlyCpiStatement.ForView(city, data.Scope)),
                new DateOnly(2026, 10, 29), data.Label, data.IsMonthly);

            foreach (var cpi in new[] { facts.ActualRentCpi, facts.GeneralInflation })
                if (cpi.Month != "" && !(ParseMonthLabel(cpi.Month) is { } m && InPeriod(m)))
                    violations.Add($"{overview.Id}: {cpi.Label} is from {cpi.Month}");

            if (model.MediaAvailable && YearMonth.TryParse(city.MediaMonitoring.Period, out var media) && !InPeriod(media))
                violations.Add($"{overview.Id}: media is from {city.MediaMonitoring.Period}");

            foreach (var tabId in new[] { "rentals", "sales" })
                if (city.MarketAnalysisTabs.FirstOrDefault(t => t.Id == tabId)?.Categories.LastOrDefault() is { } latest && latest != quarter.Id)
                    violations.Add($"{overview.Id}: {tabId} table ends at {latest}");

            foreach (var text in Rendered(model))
            {
                foreach (Match mm in MonthMention.Matches(text))
                    if (new YearMonth(int.Parse(mm.Groups["y"].Value), Array.IndexOf(MonthNames, mm.Groups["m"].Value) + 1) > last)
                        violations.Add($"{overview.Id}: names a later month in: {text}");
                foreach (Match qm in QuarterMention.Matches(text))
                    if (new YearQuarter(int.Parse(qm.Groups["y"].Value), Array.IndexOf(QuarterNames, qm.Groups["q"].Value) + 1).CompareTo(quarter) > 0)
                        violations.Add($"{overview.Id}: names a later quarter in: {text}");
            }
        }

        Assert.True(violations.Count == 0, $"{period}: {violations.Count} value(s) outside the period:\n" + string.Join("\n", violations));
    }

    private static YearMonth? ParseMonthLabel(string label)
    {
        var parts = label.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var index = parts.Length == 2 ? Array.IndexOf(MonthNames, parts[0]) : -1;
        return index >= 0 && int.TryParse(parts[1], out var year) ? new YearMonth(year, index + 1) : null;
    }

    private static IEnumerable<string> Rendered(CityReportModel m) => m.ExecutiveBullets
        .Concat(m.IndicatorsBullets).Concat(m.MarketBullets).Append(m.CityAveragesLine).Concat(m.MediaBullets)
        .Concat(m.ConclusionBullets).Concat(m.Axes.Select(a => a.Status)).Concat(m.RecommendationBullets).Concat(m.WatchList);
}
