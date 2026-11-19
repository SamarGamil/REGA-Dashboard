using AqarInsights.Api.Models.RentalControls;

namespace AqarInsights.Api.Services.RentalControls;

public static class IndicatorReshaper
{
    public static int ParsePercent(string display) => ArabicNumber.PercentInt(display);

    // Status thresholds compare the value as displayed, decimals included (yield 5.8% is دون الحد against 6%).
    private record Def(string Id, string Title, string Icon, string Definition, string ThresholdRule, string ThresholdSummary,
        Func<CityData, string> Field, Func<double, string> Status);

    private static Def[] BuildDefs(RentalPolicyOptions policy) => new[]
    {
        new Def("rental-increase", "الزيادة الإيجارية", "ri-arrow-up-circle-line",
            "معدل النمو السنوي في أسعار الإيجارات السكنية خلال فترة القياس.",
            $"{policy.GrowthWithinMax}% أو أقل: ضمن الحد — من {policy.GrowthWithinMax + 1}% إلى {policy.GrowthWatchMax - 1}%: متابعة — {policy.GrowthWatchMax}% فأكثر: متجاوز", $"الحد المرجعي: أكبر من {policy.GrowthWithinMax}%",
            c => c.ActualRentalGrowth, v => v <= policy.GrowthWithinMax ? "ضمن الحد" : v < policy.GrowthWatchMax ? "متابعة" : "متجاوز"),
        new Def("vacancy", "الشواغر الإيجارية", "ri-building-4-line",
            "نسبة الوحدات السكنية المؤجرة غير المشغولة من إجمالي الوحدات المؤجرة في المدينة.",
            $"{policy.VacancyLimitPct}% فأكثر: ضمن الحد — أقل من {policy.VacancyLimitPct}%: دون الحد", $"الحد المرجعي: أقل من {policy.VacancyLimitPct}%",
            c => c.VacancyRate, v => v >= policy.VacancyLimitPct ? "ضمن الحد" : "دون الحد"),
        new Def("investment-return", "العائد الإيجاري", "ri-funds-line",
            "صافي العائد السنوي على الاستثمار العقاري الإيجاري.",
            $"{policy.YieldActivationPct}% فأكثر: ضمن الحد — أقل من {policy.YieldActivationPct}%: دون الحد", $"الحد المرجعي: أقل من {policy.YieldActivationPct}%",
            c => c.InvestmentReturn, v => v >= policy.YieldActivationPct ? "ضمن الحد" : "دون الحد"),
        new Def("rent-to-income", "نسبة الإيجار إلى الدخل", "ri-scales-line",
            "متوسط نسبة ما تنفقه الأسرة من دخلها الشهري على الإيجار السكني.",
            $"{policy.RentBurdenLimitPct}% أو أقل: ضمن الحد — أكثر من {policy.RentBurdenLimitPct}%: متجاوز", $"الحد المرجعي: أكبر من {policy.RentBurdenLimitPct}%",
            c => c.RentToIncome, v => v <= policy.RentBurdenLimitPct ? "ضمن الحد" : "متجاوز"),
        new Def("housing-burden", "العبء السكني للدخل المحدود", "ri-home-smile-line",
            "نسبة دخل الأسرة محدودة الدخل الذي يُنفق على الإيجار السكني.",
            $"{policy.RentBurdenLimitPct}% أو أقل: ضمن الحد — أكثر من {policy.RentBurdenLimitPct}%: متجاوز", $"الحد المرجعي: أكبر من {policy.RentBurdenLimitPct}%",
            c => c.HousingBurden, v => v <= policy.RentBurdenLimitPct ? "ضمن الحد" : "متجاوز"),
    };

    public static List<IndicatorPanel> CurrentPanels(OverviewData overview, RentalPolicyOptions? policy = null)
    {
        var p = policy ?? new RentalPolicyOptions();
        var panels = new List<IndicatorPanel>();
        foreach (var d in BuildDefs(p))
        {
            var cities = overview.Cities.Select(c =>
            {
                var raw = d.Field(c);
                var number = ArabicNumber.FirstNumber(raw);
                var missing = number is null;
                var v = number ?? 0;
                return new CityIndicatorValue
                {
                    CityName = c.Name, CityId = c.Id, Value = v,
                    DisplayValue = missing ? "—" : raw,
                    Status = missing ? "—" : d.Status(v),
                };
            })
            .OrderByDescending(c => c.Value).ToList();
            for (var i = 0; i < cities.Count; i++) cities[i].SortOrder = i + 1;

            panels.Add(new IndicatorPanel
            {
                Id = d.Id, Title = d.Title, Icon = d.Icon, Definition = d.Definition,
                ThresholdRule = d.ThresholdRule, ThresholdSummary = d.ThresholdSummary, Cities = cities,
            });
        }
        return panels;
    }
}
