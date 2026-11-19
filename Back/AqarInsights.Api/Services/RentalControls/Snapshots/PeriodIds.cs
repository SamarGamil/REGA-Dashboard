using System.Text.RegularExpressions;

namespace AqarInsights.Api.Services.RentalControls.Snapshots;

/// <summary>A calendar month in the "yyyy-MM" id form used by snapshots and monthly reports.</summary>
public readonly record struct YearMonth(int Year, int Month) : IComparable<YearMonth>
{
    public string Id => $"{Year:D4}-{Month:D2}";
    public string Label => QuarterLabel.MonthLabel(new DateTime(Year, Month, 1));
    public YearQuarter Quarter => new(Year, (Month - 1) / 3 + 1);
    public YearMonth Next() => Month == 12 ? new(Year + 1, 1) : new(Year, Month + 1);
    public YearMonth Previous() => Month == 1 ? new(Year - 1, 12) : new(Year, Month - 1);

    public int CompareTo(YearMonth other) => (Year * 12 + Month).CompareTo(other.Year * 12 + other.Month);
    public static bool operator <(YearMonth a, YearMonth b) => a.CompareTo(b) < 0;
    public static bool operator >(YearMonth a, YearMonth b) => a.CompareTo(b) > 0;
    public static bool operator <=(YearMonth a, YearMonth b) => a.CompareTo(b) <= 0;
    public static bool operator >=(YearMonth a, YearMonth b) => a.CompareTo(b) >= 0;

    public static bool TryParse(string? text, out YearMonth value)
    {
        value = default;
        var m = Regex.Match(text ?? "", @"^([0-9]{4})-([0-9]{2})$");
        if (!m.Success) return false;
        var month = int.Parse(m.Groups[2].Value);
        if (month is < 1 or > 12) return false;
        value = new YearMonth(int.Parse(m.Groups[1].Value), month);
        return true;
    }

    public override string ToString() => Id;
}

/// <summary>A calendar quarter in the "yyyy-Qn" id form used by the city series and quarterly reports.</summary>
public readonly record struct YearQuarter(int Year, int Quarter) : IComparable<YearQuarter>
{
    public string Id => $"{Year:D4}-Q{Quarter}";
    public string Label => QuarterLabel.Format(Year, Quarter);
    public YearMonth FirstMonth => new(Year, (Quarter - 1) * 3 + 1);
    public YearMonth LastMonth => new(Year, Quarter * 3);
    public YearQuarter Previous() => Quarter == 1 ? new(Year - 1, 4) : new(Year, Quarter - 1);

    public int CompareTo(YearQuarter other) => (Year * 4 + Quarter).CompareTo(other.Year * 4 + other.Quarter);
    public static bool operator <(YearQuarter a, YearQuarter b) => a.CompareTo(b) < 0;
    public static bool operator >(YearQuarter a, YearQuarter b) => a.CompareTo(b) > 0;
    public static bool operator <=(YearQuarter a, YearQuarter b) => a.CompareTo(b) <= 0;
    public static bool operator >=(YearQuarter a, YearQuarter b) => a.CompareTo(b) >= 0;

    public static bool TryParse(string? text, out YearQuarter value)
    {
        value = default;
        var m = Regex.Match(text ?? "", @"^([0-9]{4})-Q([1-4])$");
        if (!m.Success) return false;
        value = new YearQuarter(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value));
        return true;
    }

    public override string ToString() => Id;
}
