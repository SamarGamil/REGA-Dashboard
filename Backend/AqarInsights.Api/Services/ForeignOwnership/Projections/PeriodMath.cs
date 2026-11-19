using AqarInsights.Api.Models;
using AqarInsights.Api.Models.ForeignOwnership;

namespace AqarInsights.Api.Services.ForeignOwnership.Projections;

// Period bucketing / labelling / anchoring shared by the dashboard projections.
public static class PeriodMath
{
    private static readonly Dictionary<string, string> MonthLabels = new()
    {
        ["01"] = "يناير",
        ["02"] = "فبراير",
        ["03"] = "مارس",
        ["04"] = "أبريل",
        ["05"] = "مايو",
        ["06"] = "يونيو",
        ["07"] = "يوليو",
        ["08"] = "أغسطس",
        ["09"] = "سبتمبر",
        ["10"] = "أكتوبر",
        ["11"] = "نوفمبر",
        ["12"] = "ديسمبر"
    };

    // The reporting anchor: the newest transaction date in the full dataset (fallback to wall-clock
    // only when empty). Computed once from all records so filters subset within a fixed period window.
    public static DateTime LatestDataDate(IReadOnlyList<TransactionRecord> records, DateTime fallback)
    {
        var latest = records.Where(r => r.Date.HasValue).Select(r => r.Date!.Value).DefaultIfEmpty().Max();
        return latest == default ? fallback : latest;
    }

    public static int GetQuarter(DateTime date) => (date.Month - 1) / 3 + 1;

    public static List<TransactionRecord> FilterByLast6Periods(IReadOnlyList<TransactionRecord> records, string period, DateTime anchor)
    {
        var keys = GetLast6PeriodKeys(period, anchor).ToHashSet();
        return records.Where(r => keys.Contains(GetPeriodGroupKey(r, period))).ToList();
    }

    // Records in the single latest bucket of the selected granularity (current month/quarter/year).
    public static List<TransactionRecord> FilterByLatestPeriod(IReadOnlyList<TransactionRecord> records, string period, DateTime anchor)
    {
        var latestKey = GetLast6PeriodKeys(period, anchor)[^1];
        return records.Where(r => GetPeriodGroupKey(r, period) == latestKey).ToList();
    }

    public static string GetPeriodGroupKey(TransactionRecord r, string period)
    {
        if (!r.Date.HasValue && string.IsNullOrEmpty(r.Month)) return "";
        return period switch
        {
            Periods.Quarterly => r.Date.HasValue ? $"{r.Date.Value.Year}-Q{GetQuarter(r.Date.Value)}" : "",
            Periods.Yearly => r.Date.HasValue ? r.Date.Value.Year.ToString() : (r.Month.Length >= 4 ? r.Month[..4] : ""),
            _ => r.Month
        };
    }

    public static string GetPeriodLabel(string key, string period)
    {
        return period switch
        {
            Periods.Quarterly => key switch
            {
                _ when key.EndsWith("Q1") => $"الربع الأول {key[..4]}",
                _ when key.EndsWith("Q2") => $"الربع الثاني {key[..4]}",
                _ when key.EndsWith("Q3") => $"الربع الثالث {key[..4]}",
                _ when key.EndsWith("Q4") => $"الربع الرابع {key[..4]}",
                _ => key
            },
            Periods.Yearly => key,
            _ => GetMonthLabel(key)
        };
    }

    private static string GetMonthLabel(string month)
    {
        if (month.Length >= 7)
        {
            var year = month[..4];
            var monthNum = month.Substring(5, 2);
            return MonthLabels.TryGetValue(monthNum, out var monthName)
                ? $"{monthName} {year}"
                : month;
        }
        return month;
    }

    public static List<string> GetLast6PeriodKeys(string period, DateTime anchor)
    {
        var now = anchor;
        var keys = new List<string>(6);
        for (int i = 5; i >= 0; i--)
        {
            switch (period)
            {
                case Periods.Quarterly:
                    var qDate = now.AddMonths(-i * 3);
                    keys.Add($"{qDate.Year}-Q{GetQuarter(qDate)}");
                    break;
                case Periods.Yearly:
                    keys.Add((now.Year - i).ToString());
                    break;
                default:
                    var mDate = now.AddMonths(-i);
                    keys.Add(mDate.ToString("yyyy-MM"));
                    break;
            }
        }
        return keys;
    }
}
