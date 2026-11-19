using System.Globalization;
using AqarInsights.Api.Models.RentalControls.Raw;
using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Models.RentalControls.Tables;
using AqarInsights.Api.Services.RentalControls.Snapshots;

namespace AqarInsights.Api.Services.RentalControls.Tables;

/// <summary>
/// Converts one release between the in-memory raw model and its table rows. FromRows(ToRows(x)) reproduces x exactly
/// (values and order); a shape the tables cannot hold throws instead of being dropped.
/// </summary>
public static class ReleaseMapper
{
    public const string DashboardSection = "dashboard";
    private const string CityAveragesSection = "cityAverages";
    private const string KpiPrefix = "kpi.";
    private const string IndicatorPrefix = "indicator.";
    private const string CityPrefix = "city.";
    private const string GapPrefix = "gap.";
    private const string VacancyAssumption = "gap.vacancyAssumptionPct";

    // indicator-series.json: the grain of each indicator's Period number.
    private static readonly Dictionary<string, string> IndicatorGrains = new(StringComparer.Ordinal)
    {
        ["rentToIncome"] = Grains.Quarter,
        ["roi"] = Grains.Quarter,
        ["publicVacancy"] = Grains.Month,
        ["rentalChange"] = Grains.Month,
        ["vacancy"] = Grains.Month,
    };

    private static readonly (string Section, Func<CityReportContent, List<string>> Get, Action<CityReportContent, List<string>> Set)[] ReportSections =
    {
        ("executive", c => c.ExecutiveBullets, (c, v) => c.ExecutiveBullets = v),
        ("indicators", c => c.IndicatorsBullets, (c, v) => c.IndicatorsBullets = v),
        ("market", c => c.MarketBullets, (c, v) => c.MarketBullets = v),
        ("media", c => c.MediaBullets, (c, v) => c.MediaBullets = v),
        ("conclusion", c => c.ConclusionBullets, (c, v) => c.ConclusionBullets = v),
        ("recommendation", c => c.RecommendationBullets, (c, v) => c.RecommendationBullets = v),
        ("watchList", c => c.WatchList, (c, v) => c.WatchList = v),
    };

    /// <summary>A deep copy (through the rows), safe to trim or edit.</summary>
    public static MappedRelease Copy(MappedRelease data)
        => FromRows(ToRows(new ReleaseRow(ReleaseIds.Live, ReleaseKinds.Live, "", "", "", default, null), data));

    public static ReleaseData ToRows(ReleaseRow release, MappedRelease data)
    {
        var id = release.Id;
        var cityIds = data.Cities.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        if (data.CityRaw.Keys.FirstOrDefault(k => !cityIds.Contains(k)) is { } strayRaw)
            throw new InvalidOperationException($"Release '{id}': city data for '{strayRaw}', which is not in the city list.");
        if (data.ReportContent.Keys.FirstOrDefault(k => !cityIds.Contains(k)) is { } strayReport)
            throw new InvalidOperationException($"Release '{id}': report narrative for '{strayReport}', which is not in the city list.");

        var observations = new List<ObservationRow>();
        void Observe(string cityId, string measure, string grain, string period, string dim, double value, string? note = null)
            => observations.Add(new ObservationRow(id, observations.Count, cityId, measure, grain, period, dim, value, note));

        void ObserveGap(string cityId, string grain, string period, long demandExp, long supplyExp, long? supplyAct, long gapExp, long? gapAct)
        {
            Observe(cityId, GapPrefix + "demandExp", grain, period, "", demandExp);
            Observe(cityId, GapPrefix + "supplyExp", grain, period, "", supplyExp);
            if (supplyAct is { } sa) Observe(cityId, GapPrefix + "supplyAct", grain, period, "", sa);
            Observe(cityId, GapPrefix + "gapExp", grain, period, "", gapExp);
            if (gapAct is { } ga) Observe(cityId, GapPrefix + "gapAct", grain, period, "", ga);
        }

        var cities = new List<CityRow>();
        var status = new List<CityStatusRow>();
        foreach (var city in data.Cities)
        {
            cities.Add(new CityRow(id, city.Id, cities.Count, city.Name, data.CityRaw.ContainsKey(city.Id), data.ReportContent.ContainsKey(city.Id)));
            status.Add(new CityStatusRow(id, city.Id, city.Path, city.Status, city.AssessmentLine1, city.AssessmentLine2));
            Observe(city.Id, KpiPrefix + "cagr", Grains.Latest, "", "", city.Cagr);
            Observe(city.Id, KpiPrefix + "vacancy", Grains.Latest, "", "", city.Vacancy);
            if (city.InvestmentReturn is { } roi) Observe(city.Id, KpiPrefix + "investmentReturn", Grains.Latest, "", "", roi);
            Observe(city.Id, KpiPrefix + "rentalIncrease", Grains.Latest, "", "", city.RentalIncrease);
            Observe(city.Id, KpiPrefix + "rentToIncome", Grains.Latest, "", "", city.RentToIncome);
            Observe(city.Id, KpiPrefix + "burden", Grains.Latest, "", "", city.Burden);
        }

        foreach (var p in data.IndicatorSeries)
        {
            if (!IndicatorGrains.TryGetValue(p.Indicator, out var grain))
                throw new InvalidOperationException($"Unknown indicator '{p.Indicator}' (city {p.CityId}); add its grain to ReleaseMapper.");
            if (p.Nationality is not null)
                throw new InvalidOperationException($"Indicator '{p.Indicator}' (city {p.CityId}) has a nationality; the tables have no column for it.");
            Observe(p.CityId, IndicatorPrefix + p.Indicator, grain, IndicatorPeriod(p, grain), p.UnitType ?? "", p.Value);
        }

        var movement = new List<MarketMovementTableRow>();
        var narrative = new List<CityNarrativeRow>();
        var media = new List<MediaMonitoringRow>();
        var mediaItems = new List<MediaItemRow>();
        foreach (var city in data.Cities)
        {
            if (!data.CityRaw.TryGetValue(city.Id, out var raw)) continue;
            foreach (var point in raw.Series)
                Observe(city.Id, CityPrefix + point.Series, SeriesGrain(point.Period, city.Id), point.Period, point.Key, point.Value, point.Change);
            movement.AddRange(raw.MovementRows.Select((m, i) => new MarketMovementTableRow(id, city.Id, i, m.Indicator, m.Level, m.Change, m.Assessment)));
            narrative.AddRange(raw.Bullets.Select((b, i) => new CityNarrativeRow(id, city.Id, DashboardSection, i, b)));
            foreach (var (m, seq) in (raw.Media ?? new List<RawMediaMonitoring>()).Select((m, i) => (m, i)))
            {
                media.Add(new MediaMonitoringRow(id, city.Id, seq, m.Period, m.Available, m.OverallSentiment,
                    m.PositivePct, m.NeutralPct, m.NegativePct, m.DiscussionSharePct));
                foreach (var (kind, texts) in MediaLists(m))
                    mediaItems.AddRange(texts.Select((t, i) => new MediaItemRow(id, city.Id, seq, kind, i, t)));
            }
        }

        var products = new List<ReportProductTableRow>();
        var axes = new List<ReportAxisTableRow>();
        foreach (var city in data.Cities)
        {
            if (!data.ReportContent.TryGetValue(city.Id, out var content)) continue;
            foreach (var (section, get, _) in ReportSections)
                narrative.AddRange((get(content) ?? []).Select((b, i) => new CityNarrativeRow(id, city.Id, section, i, b)));
            if (content.CityAveragesLine is { Length: > 0 } averages)
                narrative.Add(new CityNarrativeRow(id, city.Id, CityAveragesSection, 0, averages));
            products.AddRange((content.ProductTable ?? []).Select((p, i) => new ReportProductTableRow(id, city.Id, i, p.Product, p.RentalDeals, p.SalesDeals, p.FisherIndex)));
            axes.AddRange((content.AssessmentAxes ?? []).Select((a, i) => new ReportAxisTableRow(id, city.Id, i, a.Axis, a.Status)));
        }

        var gapCities = new List<GapCityRow>();
        foreach (var g in data.SupplyDemandGap)
        {
            gapCities.Add(new GapCityRow(id, g.CityId, gapCities.Count));
            if (g.VacancyAssumptionPct is { } assumption) Observe(g.CityId, VacancyAssumption, Grains.Latest, "", "", assumption);
            foreach (var a in g.Annual)
                ObserveGap(g.CityId, Grains.Year, a.Year.ToString("D4", CultureInfo.InvariantCulture), a.DemandExp, a.SupplyExp, a.SupplyAct, a.GapExp, a.GapAct);
            foreach (var m in g.Monthly)
                ObserveGap(g.CityId, Grains.Month, m.Period, m.DemandExp, m.SupplyExp, m.SupplyAct, m.GapExp, m.GapAct);
        }

        return new ReleaseData(release)
        {
            Cities = cities,
            CityStatus = status,
            Observations = observations,
            MarketMovement = movement,
            CityNarrative = narrative,
            ReportProducts = products,
            ReportAxes = axes,
            MediaMonitoring = media,
            MediaItems = mediaItems,
            GapCities = gapCities,
        };
    }

    public static MappedRelease FromRows(ReleaseData rows)
    {
        var releaseId = rows.Release.Id;
        var byCity = rows.Observations.OrderBy(o => o.Seq).GroupBy(o => o.CityId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        List<ObservationRow> Of(string cityId, string prefix) => byCity.TryGetValue(cityId, out var list)
            ? list.Where(o => o.Measure.StartsWith(prefix, StringComparison.Ordinal)).ToList()
            : [];
        var status = rows.CityStatus.ToDictionary(s => s.CityId, StringComparer.Ordinal);

        var cities = new List<CityRecord>();
        var cityRaw = new Dictionary<string, CityRawData>(StringComparer.Ordinal);
        var content = new Dictionary<string, CityReportContent>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows.Cities.OrderBy(c => c.Seq))
        {
            var kpis = Of(row.CityId, KpiPrefix).ToDictionary(o => o.Measure[KpiPrefix.Length..], o => o.Value, StringComparer.Ordinal);
            double Required(string name) => kpis.TryGetValue(name, out var v)
                ? v
                : throw new InvalidDataException($"Release '{releaseId}': city '{row.CityId}' has no {KpiPrefix}{name} row.");
            var s = status.GetValueOrDefault(row.CityId)
                ?? throw new InvalidDataException($"Release '{releaseId}': city '{row.CityId}' has no city_status row.");

            cities.Add(new CityRecord
            {
                Id = row.CityId,
                Name = row.Name,
                Cagr = Required("cagr"),
                Path = s.Path,
                Vacancy = Required("vacancy"),
                InvestmentReturn = kpis.TryGetValue("investmentReturn", out var roi) ? roi : null,
                RentalIncrease = Required("rentalIncrease"),
                RentToIncome = Required("rentToIncome"),
                Burden = Required("burden"),
                Status = s.Status,
                AssessmentLine1 = s.AssessmentLine1,
                AssessmentLine2 = s.AssessmentLine2,
            });
            if (row.HasDetail) cityRaw[row.CityId] = CityRaw(rows, row.CityId, Of(row.CityId, CityPrefix));
            if (row.HasReport) content[row.CityId] = Content(rows, row.CityId);
        }

        var indicators = rows.Observations
            .Where(o => o.Measure.StartsWith(IndicatorPrefix, StringComparison.Ordinal))
            .OrderBy(o => o.Seq)
            .Select(ToIndicatorPoint)
            .ToList();
        var gap = rows.GapCities.OrderBy(g => g.Seq).Select(g => GapCity(g.CityId, Of(g.CityId, GapPrefix))).ToList();
        return new MappedRelease(cities, indicators, gap, cityRaw, content);
    }

    private static CityRawData CityRaw(ReleaseData rows, string cityId, List<ObservationRow> series)
    {
        var media = rows.MediaMonitoring.Where(m => m.CityId == cityId).OrderBy(m => m.Seq).ToList();
        var items = rows.MediaItems.Where(i => i.CityId == cityId).OrderBy(i => i.Seq).ToList();
        List<string> Items(int mediaSeq, string kind) => items.Where(i => i.MediaSeq == mediaSeq && i.Kind == kind).Select(i => i.Text).ToList();

        return new CityRawData
        {
            CityId = cityId,
            Bullets = Narrative(rows, cityId, DashboardSection),
            MovementRows = rows.MarketMovement.Where(m => m.CityId == cityId).OrderBy(m => m.Seq)
                .Select(m => new RawMovementRow { Indicator = m.Indicator, Level = m.Level, Change = m.Change, Assessment = m.Assessment })
                .ToList(),
            Media = media.Count == 0 ? null : media.Select(m => new RawMediaMonitoring
            {
                Period = m.Period,
                Available = m.Available,
                OverallSentiment = m.OverallSentiment,
                PositivePct = m.PositivePct,
                NeutralPct = m.NeutralPct,
                NegativePct = m.NegativePct,
                DiscussionSharePct = m.DiscussionSharePct,
                Topics = Items(m.Seq, "topic"),
                PositiveImpressions = Items(m.Seq, "positive"),
                NeutralImpressions = Items(m.Seq, "neutral"),
                NegativeImpressions = Items(m.Seq, "negative"),
            }).ToList(),
            Series = series.Select(o => new CitySeriesPoint
            {
                Series = o.Measure[CityPrefix.Length..],
                Period = o.Period,
                Key = o.Dim,
                Value = o.Value,
                Change = o.Note,
            }).ToList(),
        };
    }

    private static CityReportContent Content(ReleaseData rows, string cityId)
    {
        var content = new CityReportContent
        {
            CityId = cityId,
            ProductTable = rows.ReportProducts.Where(p => p.CityId == cityId).OrderBy(p => p.Seq)
                .Select(p => new CityReportProductRow { Product = p.Product, RentalDeals = p.RentalDeals, SalesDeals = p.SalesDeals, FisherIndex = p.FisherIndex })
                .ToList(),
            CityAveragesLine = Narrative(rows, cityId, CityAveragesSection).SingleOrDefault() ?? "",
            AssessmentAxes = rows.ReportAxes.Where(a => a.CityId == cityId).OrderBy(a => a.Seq)
                .Select(a => new CityReportAxis { Axis = a.Axis, Status = a.Status })
                .ToList(),
        };
        foreach (var (section, _, set) in ReportSections)
            set(content, Narrative(rows, cityId, section));
        return content;
    }

    private static List<string> Narrative(ReleaseData rows, string cityId, string section) => rows.CityNarrative
        .Where(n => n.CityId == cityId && n.Section == section)
        .OrderBy(n => n.Seq)
        .Select(n => n.Text)
        .ToList();

    private static (string Kind, List<string> Texts)[] MediaLists(RawMediaMonitoring m) =>
    [
        ("topic", m.Topics ?? []),
        ("positive", m.PositiveImpressions ?? []),
        ("neutral", m.NeutralImpressions ?? []),
        ("negative", m.NegativeImpressions ?? []),
    ];

    private static string IndicatorPeriod(IndicatorSeriesPoint p, string grain)
    {
        if (grain == Grains.Quarter && p.Period is >= 1 and <= 4) return new YearQuarter(p.Year, p.Period).Id;
        if (grain == Grains.Month && p.Period is >= 1 and <= 12) return new YearMonth(p.Year, p.Period).Id;
        throw new InvalidOperationException($"Indicator '{p.Indicator}' (city {p.CityId}) has period {p.Period}, outside its {grain} range.");
    }

    private static string SeriesGrain(string period, string cityId)
    {
        if (YearMonth.TryParse(period, out _)) return Grains.Month;
        if (YearQuarter.TryParse(period, out _)) return Grains.Quarter;
        throw new InvalidOperationException($"City '{cityId}' has a series period '{period}' that is neither yyyy-MM nor yyyy-Qn.");
    }

    private static IndicatorSeriesPoint ToIndicatorPoint(ObservationRow o)
    {
        int year, period;
        if (o.Grain == Grains.Quarter && YearQuarter.TryParse(o.Period, out var q)) (year, period) = (q.Year, q.Quarter);
        else if (o.Grain == Grains.Month && YearMonth.TryParse(o.Period, out var m)) (year, period) = (m.Year, m.Month);
        else throw new InvalidDataException($"Indicator row {o.Seq} ({o.Measure}) has period '{o.Period}', which does not match its grain '{o.Grain}'.");
        return new IndicatorSeriesPoint
        {
            Indicator = o.Measure[IndicatorPrefix.Length..],
            CityId = o.CityId,
            Year = year,
            Period = period,
            Value = o.Value,
            UnitType = o.Dim.Length == 0 ? null : o.Dim,
        };
    }

    private static RawSupplyDemandGapCity GapCity(string cityId, List<ObservationRow> rows)
    {
        // Rows of one grain grouped by period, in first-appearance order.
        List<(string Period, Dictionary<string, long> Values)> Points(string grain) => rows
            .Where(o => o.Grain == grain)
            .GroupBy(o => o.Period, StringComparer.Ordinal)
            .Select(g => (g.Key, g.ToDictionary(o => o.Measure[GapPrefix.Length..], o => (long)o.Value, StringComparer.Ordinal)))
            .ToList();
        long Required(Dictionary<string, long> values, string name, string period) => values.TryGetValue(name, out var v)
            ? v
            : throw new InvalidDataException($"Gap city '{cityId}' period {period} has no {GapPrefix}{name} row.");
        long? Optional(Dictionary<string, long> values, string name) => values.TryGetValue(name, out var v) ? v : null;

        return new RawSupplyDemandGapCity
        {
            CityId = cityId,
            VacancyAssumptionPct = rows.FirstOrDefault(o => o.Measure == VacancyAssumption)?.Value,
            Annual = Points(Grains.Year).Select(p => new RawGapAnnualRow
            {
                Year = int.Parse(p.Period, CultureInfo.InvariantCulture),
                DemandExp = Required(p.Values, "demandExp", p.Period),
                SupplyExp = Required(p.Values, "supplyExp", p.Period),
                SupplyAct = Optional(p.Values, "supplyAct"),
                GapExp = Required(p.Values, "gapExp", p.Period),
                GapAct = Optional(p.Values, "gapAct"),
            }).ToList(),
            Monthly = Points(Grains.Month).Select(p => new RawGapMonthlyRow
            {
                Period = p.Period,
                DemandExp = Required(p.Values, "demandExp", p.Period),
                SupplyExp = Required(p.Values, "supplyExp", p.Period),
                SupplyAct = Optional(p.Values, "supplyAct"),
                GapExp = Required(p.Values, "gapExp", p.Period),
                GapAct = Optional(p.Values, "gapAct"),
            }).ToList(),
        };
    }
}
