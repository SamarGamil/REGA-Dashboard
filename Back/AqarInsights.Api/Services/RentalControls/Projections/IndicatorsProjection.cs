using System.Globalization;
using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Raw;

namespace AqarInsights.Api.Services.RentalControls.Projections;

public static class IndicatorsProjection
{
    private static readonly string[] Years = { "2023", "2024", "2025", "2026" };

    private record HistoricalConfig(
        string Id, string Title, string Indicator, string ThresholdType, HistoricalThreshold[] Thresholds,
        Func<CityRawData, List<RawSeriesPoint>>? CitySeries = null, string? CityValueKey = null);

    private static HistoricalConfig[] BuildHistoricalConfigs(RentalPolicyOptions policy) => new[]
    {
        new HistoricalConfig("rental-increase-hist", "الزيادة الإيجارية", "rentalChange", "higher_is_pressure", new[]
        {
            new HistoricalThreshold { Label = "ضمن الحد", Min = 0, Max = policy.GrowthWithinMax, CssClass = "bg-green-50 text-green-700" },
            new HistoricalThreshold { Label = "متابعة", Min = policy.GrowthWithinMax, Max = policy.GrowthWatchMax, CssClass = "bg-amber-50 text-amber-700" },
            new HistoricalThreshold { Label = "تجاوز مرتفع", Min = policy.GrowthWatchMax, Max = 999, CssClass = "bg-red-50 text-red-700" },
        }, CitySeries: c => c.IncreaseSeries, CityValueKey: "القيمة"),
        new HistoricalConfig("vacancy-hist", "الشواغر الإيجارية", "vacancy", "lower_is_pressure", new[]
        {
            new HistoricalThreshold { Label = "ضمن الحد", Min = policy.VacancyLimitPct, Max = 999, CssClass = "bg-green-50 text-green-700" },
            new HistoricalThreshold { Label = "دون الحد", Min = 0, Max = policy.VacancyLimitPct, CssClass = "bg-red-50 text-red-700" },
        }),
        new HistoricalConfig("investment-return-hist", "العائد الإيجاري", "roi", "lower_is_pressure", new[]
        {
            new HistoricalThreshold { Label = "ضمن الحد", Min = policy.YieldActivationPct, Max = 999, CssClass = "bg-green-50 text-green-700" },
            new HistoricalThreshold { Label = "دون الحد", Min = 0, Max = policy.YieldActivationPct, CssClass = "bg-red-50 text-red-700" },
        }, CitySeries: c => c.RoiSeries, CityValueKey: "العائد"),
        new HistoricalConfig("rent-to-income-hist", "نسبة الإيجار إلى الدخل", "rentToIncome", "higher_is_pressure", new[]
        {
            new HistoricalThreshold { Label = "ضمن الحد", Min = 0, Max = policy.RentBurdenLimitPct, CssClass = "bg-green-50 text-green-700" },
            new HistoricalThreshold { Label = "متجاوز", Min = policy.RentBurdenLimitPct, Max = 999, CssClass = "bg-red-50 text-red-700" },
        }, CitySeries: c => c.AffordabilitySeries, CityValueKey: "النسبة"),
        new HistoricalConfig("housing-burden-hist", "العبء السكني", "burden", "higher_is_pressure", new[]
        {
            new HistoricalThreshold { Label = "ضمن الحد", Min = 0, Max = policy.RentBurdenLimitPct, CssClass = "bg-green-50 text-green-700" },
            new HistoricalThreshold { Label = "متجاوز", Min = policy.RentBurdenLimitPct, Max = 999, CssClass = "bg-red-50 text-red-700" },
        }, CitySeries: c => c.BurdenSeries, CityValueKey: "العبء"),
    };

    public static IndicatorsData Build(OverviewData overview, IReadOnlyList<IndicatorSeriesPoint> series, IReadOnlyList<RawSupplyDemandGapCity> gap, IReadOnlyDictionary<string, CityRawData> cityRaw, RentalPolicyOptions? policy = null)
    {
        var rentalPolicy = policy ?? new RentalPolicyOptions();
        var cityIdToName = overview.Cities.ToDictionary(c => c.Id, c => c.Name);

        // Current panels: reuse IndicatorReshaper.CurrentPanels + add public-vacancy panel
        var currentPanels = IndicatorReshaper.CurrentPanels(overview, rentalPolicy);
        UseVacancySeries(currentPanels.Single(p => p.Id == "vacancy"), series, rentalPolicy);
        currentPanels.Add(BuildPublicVacancyPanel(series, cityIdToName));
        PanelPeriods.Apply(currentPanels, series, cityRaw);

        // Historical indicators: rent-to-income / roi / rental-change come from the per-city
        // workbook sheets; burden + rental-vacancy come from the determinants workbook series.
        var historicalIndicators = BuildHistoricalConfigs(rentalPolicy)
            .Select(cfg => cfg.CitySeries != null
                ? BuildHistoricalFromCity(cfg, cityRaw, cityIdToName)
                : BuildHistoricalIndicator(cfg, series, cityIdToName))
            .ToList();

        var supplyDemandGap = SupplyDemandGapProjection.Build(gap, cityIdToName);

        return new IndicatorsData
        {
            CurrentPeriodIndicators = currentPanels,
            HistoricalIndicators = historicalIndicators,
            SupplyDemandGap = supplyDemandGap,
            CurrentToHistoricalMap = new()
            {
                ["rental-increase"] = "rental-increase-hist",
                ["vacancy"] = "vacancy-hist",
                ["investment-return"] = "investment-return-hist",
                ["rent-to-income"] = "rent-to-income-hist",
                ["housing-burden"] = "housing-burden-hist",
            },
        };
    }

    // Rental vacancy takes value and month from its series (the overview workbook's vacancy column has no month), so
    // the panel never shows a figure under another month's label; a city without that month's value shows "—".
    private static void UseVacancySeries(IndicatorPanel panel, IReadOnlyList<IndicatorSeriesPoint> series, RentalPolicyOptions policy)
    {
        var byCity = MonthlySeries.Latest(series, "vacancy").ByCity;
        foreach (var city in panel.Cities)
        {
            var found = byCity.TryGetValue(city.CityId, out var v);
            city.Value = found ? v : 0;
            city.DisplayValue = found ? Percent(v) : "—";
            city.Status = !found ? "—" : v >= policy.VacancyLimitPct ? "ضمن الحد" : "دون الحد";
        }
        panel.Cities = panel.Cities.OrderByDescending(c => c.Value).ToList();
        for (var i = 0; i < panel.Cities.Count; i++) panel.Cities[i].SortOrder = i + 1;
    }

    public static string Percent(double value) => $"{value.ToString(CultureInfo.InvariantCulture)}%";

    private static IndicatorPanel BuildPublicVacancyPanel(IReadOnlyList<IndicatorSeriesPoint> series, Dictionary<string, string> cityIdToName)
    {
        // Each city with public-vacancy data shows its value for the panel's month (the latest month with any value),
        // or "—" when it has none for that month.
        var byCity = MonthlySeries.Latest(series, "publicVacancy").ByCity;
        var cities = series
            .Where(s => s.Indicator == "publicVacancy" && cityIdToName.ContainsKey(s.CityId))
            .Select(s => s.CityId).Distinct()
            .Select(id => (id, found: byCity.TryGetValue(id, out var v), value: Math.Round(v, 1)))
            .OrderByDescending(x => x.found).ThenByDescending(x => x.value)
            .Select((x, i) => new CityIndicatorValue
            {
                CityName = cityIdToName[x.id],
                CityId = x.id,
                Value = x.found ? x.value : 0,
                DisplayValue = x.found ? Percent(x.value) : "—",
                Status = "—",
                SortOrder = i + 1,
            })
            .ToList();

        // The latest month is shown by the panel's PeriodLabel (PanelPeriods), so no summary line here.
        return new IndicatorPanel
        {
            Id = "public-vacancy",
            Title = "الشواغر العامة",
            Icon = "ri-door-open-line",
            Definition = "نسبة الوحدات السكنية العامة غير المشغولة من إجمالي الوحدات السكنية في المدينة.",
            ThresholdRule = "الحدود المرجعية قيد الاعتماد",
            ThresholdSummary = "",
            Cities = cities,
        };
    }

    private static HistoricalIndicator BuildHistoricalIndicator(
        HistoricalConfig cfg,
        IReadOnlyList<IndicatorSeriesPoint> series,
        Dictionary<string, string> cityIdToName)
    {
        // Group series by cityId → year, keeping the max period per year
        var best = new Dictionary<string, Dictionary<string, (int period, double value)>>();

        foreach (var point in series.Where(s => s.Indicator == cfg.Indicator))
        {
            if (!cityIdToName.TryGetValue(point.CityId, out var cityName))
                continue;

            var year = point.Year.ToString();
            if (!Years.Contains(year))
                continue;

            var byYear = best.TryGetValue(point.CityId, out var m) ? m : (best[point.CityId] = new());
            if (!byYear.TryGetValue(year, out var cur) || point.Period >= cur.period)
                byYear[year] = (point.Period, Math.Round(point.Value, 1));
        }

        var indicator = new HistoricalIndicator
        {
            Id = cfg.Id,
            Title = cfg.Title,
            Years = Years.ToList(),
            ThresholdType = cfg.ThresholdType,
            Thresholds = cfg.Thresholds.ToList(),
        };

        // Emit all cities (14) sorted by name, even those with no data — fill "—".
        foreach (var (cityId, cityName) in cityIdToName.OrderBy(kv => kv.Value))
        {
            best.TryGetValue(cityId, out var byYear);
            var rowOut = new HistoricalCityRow { CityName = cityName };
            foreach (var y in Years)
            {
                if (byYear != null && byYear.TryGetValue(y, out var pv))
                {
                    rowOut.Values[y] = pv.value;
                    rowOut.DisplayValues[y] = $"{(pv.value % 1 == 0 ? (int)pv.value : pv.value)}%";
                }
                else
                {
                    rowOut.Values[y] = null;
                    rowOut.DisplayValues[y] = "—";
                }
            }
            indicator.Cities.Add(rowOut);
        }

        return indicator;
    }

    // Builds a historical indicator from the per-city workbook series (quarterly "YYYY-Qn" points),
    // keeping the latest quarter within each year of the 2023–2026 window.
    private static HistoricalIndicator BuildHistoricalFromCity(
        HistoricalConfig cfg,
        IReadOnlyDictionary<string, CityRawData> cityRaw,
        Dictionary<string, string> cityIdToName)
    {
        var indicator = new HistoricalIndicator
        {
            Id = cfg.Id,
            Title = cfg.Title,
            Years = Years.ToList(),
            ThresholdType = cfg.ThresholdType,
            Thresholds = cfg.Thresholds.ToList(),
        };

        // Emit all cities (14) sorted by name, even those with no data — fill "—".
        foreach (var (cityId, cityName) in cityIdToName.OrderBy(kv => kv.Value))
        {
            var byYear = new Dictionary<string, (int quarter, double value)>();
            if (cityRaw.TryGetValue(cityId, out var raw))
            {
                foreach (var point in cfg.CitySeries!(raw))
                {
                    if (!TryParseYearQuarter(point.Period, out var year, out var quarter))
                        continue;
                    var y = year.ToString();
                    if (!Years.Contains(y))
                        continue;
                    if (!point.Values.TryGetValue(cfg.CityValueKey!, out var val))
                        continue;
                    // Keep the latest quarter in the year; for duplicate periods keep the first
                    // (real) occurrence — some city sheets append a stray trailing block.
                    if (!byYear.TryGetValue(y, out var cur) || quarter > cur.quarter)
                        byYear[y] = (quarter, val);
                }
            }

            var rowOut = new HistoricalCityRow { CityName = cityName };
            foreach (var y in Years)
            {
                if (byYear.TryGetValue(y, out var qv))
                {
                    rowOut.Values[y] = qv.value;
                    rowOut.DisplayValues[y] = FormatPercent(qv.value);
                }
                else
                {
                    rowOut.Values[y] = null;
                    rowOut.DisplayValues[y] = "—";
                }
            }
            indicator.Cities.Add(rowOut);
        }

        return indicator;
    }

    private static string FormatPercent(double value) => $"{(value % 1 == 0 ? (int)value : value)}%";

    private static bool TryParseYearQuarter(string period, out int year, out int quarter)
    {
        year = 0;
        quarter = 0;
        var parts = period.Split('-');
        if (parts.Length != 2)
            return false;
        if (!int.TryParse(parts[0], out year))
            return false;
        return int.TryParse(parts[1].TrimStart('Q', 'q'), out quarter);
    }
}
