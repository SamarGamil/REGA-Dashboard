using System.Globalization;
using System.Text.Json;
using AqarInsights.Api.Models.RentalControls.Raw;
using AqarInsights.Api.Services.RentalControls.Store;

namespace AqarInsights.Api.Services.RentalControls.Snapshots;

/// <summary>
/// A read-only view of a loaded dataset with every monthly series (CPI, media, monthly indicators) cut at
/// <c>asOf</c>; quarterly figures are kept. Used so a quarterly report never shows months after the quarter.
/// The month's market-movement rows come from <c>monthRows</c> (monthly_movement: as published that month) when
/// present, otherwise the CPI rows are recomputed from the cut series. Works on copies; the wrapped store is untouched.
/// </summary>
public sealed class CappedRentalControlsStore : IRentalControlsStore
{
    // Deep copies of a city's data (the cut and the CPI-panel alignment work on copies).
    private static readonly JsonSerializerOptions CopyJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    // Market-panel rows whose value is the latest CPI month (series key in RentVsInflationSeries).
    private static readonly (string Indicator, string SeriesKey)[] CpiRows =
    {
        ("الإيجار الفعلي للمسكن (CPI)", "الإيجار الفعلي"),
        ("التضخم العام (CPI)", "التضخم العام"),
    };

    private readonly IRentalControlsStore _inner;
    private readonly List<IndicatorSeriesPoint> _indicators;
    private readonly List<RawSupplyDemandGapCity> _gap;
    private readonly Dictionary<string, CityRawData> _cities = new(StringComparer.Ordinal);

    public CappedRentalControlsStore(
        IRentalControlsStore inner, YearMonth asOf, IReadOnlyDictionary<string, IReadOnlyList<RawMovementRow>>? monthRows = null)
    {
        _inner = inner;
        _indicators = SnapshotPeriod.Trim(inner.GetIndicatorSeries(), asOf);
        // The supply/demand gap workbook is a single sheet with no history: every period shows it as is.
        _gap = inner.GetSupplyDemandGap().ToList();
        foreach (var city in inner.GetCities())
        {
            if (inner.GetCityRaw(city.Id) is not { } raw) continue;
            var copy = JsonSerializer.Deserialize<CityRawData>(
                JsonSerializer.Serialize(raw, CopyJson), CopyJson)!;
            SnapshotPeriod.Trim(copy, asOf);
            AlignCpiPanel(copy, monthRows?.GetValueOrDefault(city.Id), asOf);
            _cities[city.Id] = copy;
        }
    }

    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public IReadOnlyList<CityRecord> GetCities() => _inner.GetCities();

    public IReadOnlyList<IndicatorSeriesPoint> GetIndicatorSeries() => _indicators;

    public IReadOnlyList<RawSupplyDemandGapCity> GetSupplyDemandGap() => _gap;

    public CityRawData? GetCityRaw(string cityId) => _cities.GetValueOrDefault(cityId);

    // The rows take the as-of month's value and level (the level names the row's month). The current workbook's cut
    // series is the freshest reading for the month — a later workbook may revise it (e.g. Buraidah August CPI +9.2%
    // published, +9.9% in the September workbook) — so when the series ends on the as-of month and genuinely revises
    // the published figure (beyond display rounding: the workbook shows 10.25 as 10.2) its value is shown, keeping
    // the month's written assessment (dropped when the revised value points the other way). Otherwise the month's
    // published row is shown as published; computed values have no written assessment. Without either, the source's
    // row (written for a later month) is not shown: it becomes "—" dated the as-of month. The monthly rent change is
    // only ever the month's own row (otherwise the mask hides the source's row, which names a later month).
    private static void AlignCpiPanel(CityRawData city, IReadOnlyList<RawMovementRow>? panel, YearMonth asOf)
    {
        foreach (var (indicator, key) in CpiRows)
        {
            var row = city.MovementRows.FirstOrDefault(r => r.Indicator.Trim() == indicator);
            if (row is null) continue;
            var asOfRow = panel?.FirstOrDefault(r => r.Indicator.Trim() == indicator);
            var last = city.RentVsInflationSeries.LastOrDefault(p => p.Values.ContainsKey(key));
            if (last is not null && YearMonth.TryParse(last.Period, out var seriesMonth) && seriesMonth == asOf
                && (asOfRow is null || IsRevised(asOfRow.Change, last.Values[key])))
            {
                var v = Math.Round(last.Values[key], 1, MidpointRounding.AwayFromZero);
                row.Level = $"مدينة - \u200E{asOf.Id}";
                row.Change = (v > 0 ? "+" : "") + v.ToString("0.0", CultureInfo.InvariantCulture) + "%";
                row.Assessment = asOfRow is not null && SameDirection(asOfRow.Change, v) ? asOfRow.Assessment : "—";
                continue;
            }
            if (asOfRow is not null)
            {
                row.Level = asOfRow.Level;
                row.Change = asOfRow.Change;
                row.Assessment = asOfRow.Assessment;
                continue;
            }
            row.Assessment = "—";
            if (last is null)
            {
                row.Level = $"مدينة - \u200E{asOf.Id}";
                row.Change = "—";
                continue;
            }
            var earlier = Math.Round(last.Values[key], 1, MidpointRounding.AwayFromZero);
            row.Level = YearMonth.TryParse(last.Period, out var month) ? $"مدينة - \u200E{month.Id}" : row.Level;
            row.Change = (earlier > 0 ? "+" : "") + earlier.ToString("0.0", CultureInfo.InvariantCulture) + "%";
        }

        const string monthlyChange = "التغير الإيجاري (شهري)";
        if (panel?.FirstOrDefault(r => r.Indicator.Trim() == monthlyChange) is { } own
            && city.MovementRows.FirstOrDefault(r => r.Indicator.Trim() == monthlyChange) is { } changeRow)
        {
            changeRow.Level = own.Level;
            changeRow.Change = own.Change;
            changeRow.Assessment = own.Assessment;
        }
    }

    // The published assessment still describes a revised value only while both point the same way.
    private static bool SameDirection(string published, double revised)
        => ArabicNumber.FirstNumber(published) is { } p && Math.Sign(p) == Math.Sign(revised);

    // A later workbook revised the month's reading when the series value is further from the published figure than
    // display rounding allows (10.25 may be shown as 10.2 or 10.3; Buraidah's 9.2 → 9.9 is a revision).
    private static bool IsRevised(string published, double seriesValue)
        => ArabicNumber.FirstNumber(published) is not { } p || Math.Abs(seriesValue - p) > 0.05 + 1e-9;
}
