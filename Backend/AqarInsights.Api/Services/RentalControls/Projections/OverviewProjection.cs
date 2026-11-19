using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Raw;

namespace AqarInsights.Api.Services.RentalControls.Projections;

public static class OverviewProjection
{
    /// <param name="cityRaw">The city workbooks (optional): their yield series gives the yield's decimal.</param>
    public static OverviewData Build(IReadOnlyList<CityRecord> cities, Func<string, CityRawData?>? cityRaw = null)
    {
        var data = new OverviewData { ClassificationLegend = RentalControlsMappings.Legend.ToList() };
        var priority = 0;

        foreach (var record in cities)
        {
            priority++;
            var (classification, classificationLabel) = RentalControlsMappings.Classification(record.Path);
            var status = !string.IsNullOrWhiteSpace(record.Status)
                ? record.Status
                : RentalControlsMappings.OperationalStatus(record.AssessmentLine1);

            data.Cities.Add(new CityData
            {
                Id = record.Id,
                Name = record.Name,
                Cagr = RentalControlsMappings.Percent(record.Cagr, decimals: 1),
                Classification = classification,
                ClassificationLabel = classificationLabel,
                VacancyRate = RentalControlsMappings.Percent(record.Vacancy),
                InvestmentReturn = Yield(record, cityRaw?.Invoke(record.Id)),
                ActualRentalGrowth = RentalControlsMappings.Percent(record.RentalIncrease, plusSign: true),
                RentToIncome = RentalControlsMappings.Percent(record.RentToIncome),
                HousingBurden = RentalControlsMappings.Percent(record.Burden),
                OperationalStatus = status,
                Recommendation = record.AssessmentLine2,
                ImpactData = new ImpactData
                {
                    Available = status == "الضوابط مفعلة",
                    Description = record.AssessmentLine2
                },
                Priority = priority,
            });
        }

        data.Recommendations = data.Cities.Select(c => new Recommendation { City = c.Name, Text = c.Recommendation }).ToList();
        data.NationalSummary = ComputeSummary(data.Cities);
        data.OperationalStages = ComputeStages(data.Cities);
        return data;
    }

    // The overview workbook rounds the yield to a whole percent (Jeddah 5.33% → 5%), which also hides breaches of the
    // 6% reference (Buraidah 5.79% → 6%). That whole figure is the city's latest yield-series point rounded, so the
    // decimal comes from, in order: the city workbook's own latest yield point (Bahah's 5.54 of Q4 2025 → 5.5%), else
    // the overview's assessment text ("العائد 5.3% دون الحد") — each used only when it rounds to the overview's figure
    // (same number, more precision); otherwise the overview. The city's own point goes first because the overview's
    // text can be another city's (Q2 2026: Buraidah's line repeats Hail's "العائد 4.7%"; its own point is 5.23).
    private static readonly System.Text.RegularExpressions.Regex AssessmentYield = new(@"العائد\s*(?<v>\d+\.\d+)%");

    private static string Yield(CityRecord record, CityRawData? raw)
    {
        var overview = RentalControlsMappings.Percent(record.InvestmentReturn);
        if (record.InvestmentReturn is not { } fraction) return overview;
        var whole = Math.Round(fraction * 100, MidpointRounding.AwayFromZero);
        bool Same(double v) => Math.Round(v, MidpointRounding.AwayFromZero) == whole;

        var latest = raw?.RoiSeries
            .Where(p => p.Values.ContainsKey("العائد"))
            .OrderBy(p => p.Period, StringComparer.Ordinal)
            .LastOrDefault();
        if (latest is not null && Same(latest.Values["العائد"]))
            return RentalControlsMappings.Percent(latest.Values["العائد"] / 100, decimals: 1);

        var text = AssessmentYield.Match(record.AssessmentLine2 ?? "");
        return text.Success && double.TryParse(text.Groups["v"].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var stated) && Same(stated)
            ? RentalControlsMappings.Percent(stated / 100, decimals: 1)
            : overview;
    }

    private static NationalSummaryData ComputeSummary(List<CityData> cities) => new()
    {
        AcceleratedCities = cities.Count(c => c.ClassificationLabel == "accelerated"),
        ElevatedCities = cities.Count(c => c.ClassificationLabel == "elevated"),
        SustainableCities = cities.Count(c => c.ClassificationLabel == "sustainable"),
        RegulationActivated = cities.Count(c => c.OperationalStatus == "الضوابط مفعلة"),
        DetailedStudy = cities.Count(c => c.OperationalStatus == "يتطلب دراسة تفصيلية"),
        ProactiveMonitoring = cities.Count(c => c.OperationalStatus == "يتطلب دراسة استباقية"),
        RoutineMonitoring = cities.Count(c => c.OperationalStatus == "لا إجراء"),
    };

    private static List<OperationalStage> ComputeStages(List<CityData> cities) => new()
    {
        new() { Key = "noAction", Label = "لا إجراء", Count = cities.Count(c => c.OperationalStatus == "لا إجراء") },
        new() { Key = "proactive", Label = "يتطلب دراسة استباقية", Count = cities.Count(c => c.OperationalStatus == "يتطلب دراسة استباقية") },
        new() { Key = "detailed", Label = "يتطلب دراسة تفصيلية", Count = cities.Count(c => c.OperationalStatus == "يتطلب دراسة تفصيلية") },
        new() { Key = "activated", Label = "الضوابط مفعلة", Count = cities.Count(c => c.OperationalStatus == "الضوابط مفعلة") },
    };
}
