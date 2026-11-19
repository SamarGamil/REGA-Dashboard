using AqarInsights.Api.Models;
using AqarInsights.Api.Models.ForeignOwnership;

namespace AqarInsights.Api.Services.ForeignOwnership.Projections;

public static class StrategicProjection
{
    public static StrategicResponse Build(IReadOnlyList<TransactionRecord> records, string period, DateTime anchor)
    {
        // Growth compares the latest period bucket against the previous one at the selected granularity.
        var keys = PeriodMath.GetLast6PeriodKeys(period, anchor);
        var currentKey = keys[^1];
        var previousKey = keys[^2];
        var currentRecords = records.Where(r => PeriodMath.GetPeriodGroupKey(r, period) == currentKey).ToList();
        var previousRecords = records.Where(r => PeriodMath.GetPeriodGroupKey(r, period) == previousKey).ToList();

        // Counts/values shown reflect the current period only, consistent with the other dashboard tables.
        var districtGroups = currentRecords
            .Where(r => !string.IsNullOrWhiteSpace(r.District))
            .GroupBy(r => new { r.City, r.District })
            .OrderByDescending(g => g.TxCount())
            .ToList();

        var currentDistrictCounts = currentRecords
            .Where(r => !string.IsNullOrWhiteSpace(r.District))
            .GroupBy(r => new { r.City, r.District })
            .ToDictionary(g => g.Key, g => g.TxCount());

        var previousDistrictCounts = previousRecords
            .Where(r => !string.IsNullOrWhiteSpace(r.District))
            .GroupBy(r => new { r.City, r.District })
            .ToDictionary(g => g.Key, g => g.TxCount());

        var hotSpots = districtGroups
            .Take(5)
            .Select((g, i) =>
            {
                var curr = currentDistrictCounts.GetValueOrDefault(g.Key);
                var prev = previousDistrictCounts.GetValueOrDefault(g.Key);
                var growth = ValueBuckets.ComputeGrowth(curr, prev);
                return new HotSpotDto
                {
                    Id = i + 1,
                    City = g.Key.City,
                    District = g.Key.District,
                    Transactions = g.TxCount().RoundCount(),
                    Value = g.Sum(r => r.Value),
                    Growth = growth,
                    Reason = growth > 0
                        ? $"نمو ملحوظ في {g.Key.District} بمدينة {g.Key.City}"
                        : growth == 0
                            ? $"استقرار في {g.Key.District} بمدينة {g.Key.City}"
                            : $"تراجع في {g.Key.District} بمدينة {g.Key.City}"
                };
            })
            .ToList();

        var hotSpotKeys = hotSpots.Select(h => new { h.City, h.District }).ToHashSet();
        var emergingAreas = districtGroups
            .Select(g =>
            {
                var curr = currentDistrictCounts.GetValueOrDefault(g.Key);
                var prev = previousDistrictCounts.GetValueOrDefault(g.Key);
                var growth = ValueBuckets.ComputeGrowth(curr, prev);
                return new { g.Key, Count = g.TxCount(), Growth = growth };
            })
            .Where(x => !hotSpotKeys.Contains(new { x.Key.City, x.Key.District }))
            .OrderByDescending(x => x.Growth)
            .Take(7)
            .Select(x => new EmergingAreaDto
            {
                City = x.Key.City,
                District = x.Key.District,
                Transactions = x.Count.RoundCount(),
                Growth = x.Growth,
                Trend = x.Growth > 0 ? "صاعد" : x.Growth == 0 ? "مستقر" : "هابط"
            })
            .ToList();

        var currentRegionCounts = currentRecords
            .Where(r => !string.IsNullOrWhiteSpace(r.Region))
            .GroupBy(r => r.Region)
            .ToDictionary(g => g.Key, g => g.TxCount());

        var previousRegionCounts = previousRecords
            .Where(r => !string.IsNullOrWhiteSpace(r.Region))
            .GroupBy(r => r.Region)
            .ToDictionary(g => g.Key, g => g.TxCount());

        var highGrowthRegions = records
            .Where(r => !string.IsNullOrWhiteSpace(r.Region))
            .GroupBy(r => r.Region)
            .Select(g =>
            {
                var curr = currentRegionCounts.GetValueOrDefault(g.Key);
                var prev = previousRegionCounts.GetValueOrDefault(g.Key);
                return new HighGrowthRegionDto
                {
                    Region = g.Key,
                    Growth = ValueBuckets.ComputeGrowth(curr, prev),
                    Reason = curr > prev
                        ? $"نمو ملحوظ في النشاط الاستثماري في {g.Key}"
                        : curr == prev
                            ? $"استقرار في النشاط الاستثماري في {g.Key}"
                            : $"تراجع في النشاط الاستثماري في {g.Key}"
                };
            })
            .OrderByDescending(r => r.Growth)
            .Take(4)
            .ToList();

        // Market concentration (HHI normalized to 0-100)
        var total = records.TxCount();
        var regionShares = records.GroupBy(r => r.Region).Select(g => g.TxCount() / total).ToList();
        var natShares = records.GroupBy(r => r.Nationality).Select(g => g.TxCount() / total).ToList();

        var n = regionShares.Count;
        var rawHhi = regionShares.Sum(s => s * s);
        var concentrationIndex = n > 1 ? Math.Round((rawHhi - 1m / n) / (1m - 1m / n) * 100, 0) : (n == 1 ? 100m : 0m);
        concentrationIndex = Math.Max(0, Math.Min(concentrationIndex, 100));

        var totalValue = records.Sum(r => r.Value);
        var valueShares = totalValue > 0 ? records.GroupBy(r => r.Region).Select(g => g.Sum(r => r.Value) / totalValue).ToList() : [];
        var nv = valueShares.Count;
        var rawValueHhi = valueShares.Sum(s => s * s);
        var investmentConcentration = nv > 1 ? Math.Round((rawValueHhi - 1m / nv) / (1m - 1m / nv) * 100, 0) : (nv == 1 ? 100m : 0m);
        investmentConcentration = Math.Max(0, Math.Min(investmentConcentration, 100));

        var nationalityDiversity = Math.Round((1 - natShares.Sum(s => s * s)) * 100, 0);
        var geographicSpread = Math.Max(0, Math.Min(100 - concentrationIndex, 100));

        var topPropertyType = records.GroupBy(r => r.PropertyType).OrderByDescending(g => g.TxCount()).FirstOrDefault()?.Key ?? "";
        var topRegion = records.GroupBy(r => r.Region).OrderByDescending(g => g.TxCount()).FirstOrDefault()?.Key ?? "";
        var topNat = records.Where(r => !string.IsNullOrWhiteSpace(r.Nationality)).GroupBy(r => r.Nationality).OrderByDescending(g => g.TxCount()).FirstOrDefault()?.Key ?? "";

        var topOpportunities = new List<OpportunityDto>
        {
            new() { Id = 1, Title = "الاستثمار في الفئة الأكثر طلباً", Description = $"فئة {topPropertyType} تمثل أعلى طلب", Priority = "عالية" },
            new() { Id = 2, Title = "التوسع في المنطقة الرائدة", Description = $"{topRegion} تشهد أكبر حجم عمليات", Priority = "عالية" },
            new() { Id = 3, Title = "استهداف الجنسية الأكثر نشاطاً", Description = $"المستثمرون من {topNat} هم الأكثر نشاطاً", Priority = "متوسطة" },
        };

        return new StrategicResponse
        {
            Insights = new StrategicInsightsDto
            {
                ConcentrationIndex = concentrationIndex,
                InvestmentConcentrationIndex = investmentConcentration,
                NationalityDiversityIndex = nationalityDiversity,
                GeographicSpreadIndex = geographicSpread,
                HotSpots = hotSpots,
                EmergingAreas = emergingAreas,
                HighGrowthRegions = highGrowthRegions,
                TopOpportunities = topOpportunities
            }
        };
    }
}
