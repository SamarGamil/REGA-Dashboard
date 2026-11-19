using System.Text;
using ClosedXML.Excel;
using AqarInsights.Api.Services.RentalControls.Snapshots;
using AqarInsights.Api.Utilities;

namespace AqarInsights.Api.Services.RentalControls.MonthlyUpdate;

// Applies a month's update folder to the source workbooks in Data/rental-controls-source (README "Monthly data
// update"); --import-controls then publishes them.
public sealed class MonthlyUpdateRunner(string sourceDir, string updatesDir, YearMonth month)
{
    public static IReadOnlyList<string> AllSteps { get; } = ["cpi", "rental-vacancy", "public-vacancy", "media", "gap"];

    private static readonly string[] CityIds =
        ["riyadh", "jeddah", "makkah", "madinah", "dammam", "buraidah", "tabuk", "abha", "hail", "najran", "jazan", "arar", "sakaka", "bahah"];
    private const string CityPrefix = "تحليل مدينة";
    private const string MediaWorkbook = "الرصد الإعلامي.xlsx";
    private const string GapWorkbook = "supply-demand-gap.xlsx";

    public MonthlyUpdateReport Run(IReadOnlyCollection<string> steps)
    {
        var report = new MonthlyUpdateReport();
        foreach (var unknown in steps.Except(AllSteps)) report.Warnings.Add($"unknown step «{unknown}» (steps: {string.Join(", ", AllSteps)})");
        var cityUpdates = CityIds.Select(id => (id, path: City(updatesDir, id))).Where(c => c.path is not null)
            .ToDictionary(c => c.id, c => c.path!, StringComparer.Ordinal);
        if (steps.Contains("cpi") || steps.Contains("rental-vacancy"))
            foreach (var missing in CityIds.Except(cityUpdates.Keys)) report.Warnings.Add($"{missing}: no city workbook in {updatesDir}");

        if (steps.Contains("cpi"))
            foreach (var (cityId, updatePath) in cityUpdates)
            {
                if (City(sourceDir, cityId) is not { } targetPath) { report.Warnings.Add($"{cityId}: no source city workbook"); continue; }
                using var target = new XLWorkbook(targetPath);
                using var update = new XLWorkbook(updatePath);
                if (CityCpiPatcher.Apply(cityId, target, update, month, report)) target.Save();
            }

        if (steps.Contains("rental-vacancy") || steps.Contains("public-vacancy"))
        {
            using var vacancy = new XLWorkbook(Single(sourceDir, n => n.Contains("المحددات") && n.Contains("الشواغر العامة")));
            if (steps.Contains("rental-vacancy"))
                foreach (var (cityId, updatePath) in cityUpdates)
                {
                    using var update = new XLWorkbook(updatePath);
                    RentalVacancyPatcher.Append(vacancy, cityId, RentalVacancyPatcher.ReadCityMonthly(update).Where(m => m.Month <= month).ToList(), report);
                }
            if (steps.Contains("public-vacancy"))
            {
                using var update = new XLWorkbook(Single(updatesDir, n => n.Contains("الشواغر العامة") && !n.Contains("المحددات")));
                PublicVacancyPatcher.Apply(vacancy, update, report);
            }
            vacancy.Save();
        }

        if (steps.Contains("media"))
        {
            var cities = MediaDeckParser.Parse(PptxDeckLoader.Load(Single(updatesDir, _ => true, "*.pptx")), report);
            using var media = new XLWorkbook(Path.Combine(sourceDir, MediaWorkbook));
            MediaWorkbookWriter.Write(media, month, cities, report);
            media.Save();
        }

        if (steps.Contains("gap"))
        {
            using var summary = new XLWorkbook(Single(updatesDir, n => n.Contains("فجوة")));
            using var gap = new XLWorkbook(Path.Combine(sourceDir, GapWorkbook));
            GapSummaryPatcher.Apply(gap, GapSummaryPatcher.ReadSummary(summary).Where(r => r.Month <= month).ToList(), report);
            gap.Save(GapSummaryPatcher.SaveOptions);
        }
        return report;
    }

    private static string? City(string dir, string cityId)
    {
        var token = Name(RentalControlsMappings.CityFileToken(cityId)!);
        return Files(dir, "*.xlsx").FirstOrDefault(f => Name(f).Contains(Name(CityPrefix)) && Name(f).Contains(token));
    }

    private static string Single(string dir, Func<string, bool> nameMatches, string pattern = "*.xlsx")
    {
        var matches = Files(dir, pattern).Where(f => nameMatches(Name(f))).ToList();
        return matches.Count == 1 ? matches[0]
            : throw new InvalidOperationException($"Expected one {pattern} file in {dir} for this step, found {matches.Count}: {string.Join(", ", matches.Select(Path.GetFileName))}");
    }

    private static IEnumerable<string> Files(string dir, string pattern)
        => Directory.GetFiles(dir, pattern).Where(f => !Path.GetFileName(f).StartsWith("~$", StringComparison.Ordinal));

    // File names compared NFC + Arabic-normalized (alef forms, word-final ه → ة), as the converter finds the city
    // workbooks; needles are written in that normalized form.
    private static string Name(string pathOrText)
        => ArabicNormalizer.Normalize(Path.GetFileNameWithoutExtension(pathOrText).Normalize(NormalizationForm.FormC));
}
