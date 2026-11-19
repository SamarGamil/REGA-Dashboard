using System.Text.Json;
using ClosedXML.Excel;
using AqarInsights.Api.Models;
using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Models.RentalControls.Raw;
using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Models.RentalControls.Tables;
using AqarInsights.Api.Services.RentalControls.Projections;
using AqarInsights.Api.Services.RentalControls.Reading;
using AqarInsights.Api.Services.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Snapshots;
using AqarInsights.Api.Services.RentalControls.Tables;
using AqarInsights.Api.Utilities;

namespace AqarInsights.Api.Services.RentalControls;

public class RentalControlsConverterService : IRentalControlsConverterService
{
    public const string NarrativeFolder = "city-reports";

    private readonly string _sourceDir;
    private readonly IRentalControlsRepository _repository;
    private readonly ILogger<RentalControlsConverterService> _logger;
    private readonly ReleaseWriter? _releases;
    private readonly TimeProvider _clock;

    private const string OverviewFileName = "لوحة الضوابط الايجارية للمدن.xlsx";
    private const string CityFilePrefix = "تحليل مدينة";
    private const string MediaMonitoringFileName = "الرصد الإعلامي.xlsx";
    private const string SupplyDemandGapFileName = "supply-demand-gap.xlsx";

    public RentalControlsConverterService(string sourceDir, IRentalControlsRepository repository,
        ILogger<RentalControlsConverterService> logger, ReleaseWriter? releases = null, TimeProvider? clock = null)
    {
        _sourceDir = sourceDir;
        _repository = repository;
        _logger = logger;
        _releases = releases;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<ConversionReport> ConvertAllAsync(CancellationToken cancellationToken = default)
    {
        var report = new ConversionReport();
        
        // 1. Read overview workbook → CityRecord[]
        var overviewPath = Path.Combine(_sourceDir, OverviewFileName);
        if (!File.Exists(overviewPath))
        {
            report.Errors.Add($"Overview workbook not found: {overviewPath}");
            return report;
        }

        List<CityRecord> cities;
        try
        {
            using var overviewWb = new XLWorkbook(overviewPath);
            cities = OverviewReader.Read(overviewWb.Worksheet(1));
        }
        catch (RentalControlsReadException ex)
        {
            report.Errors.Add($"Failed to read overview: {ex.Message}");
            return report;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            report.Errors.Add($"Cannot open overview workbook (locked or corrupt): {ex.Message}");
            return report;
        }

        // 2. Build cityIdByNormalizedName for vacancy lookup
        var cityIdByNormalizedName = cities
            .GroupBy(c => ArabicNormalizer.Normalize(c.Name))
            .ToDictionary(g => g.Key, g => g.First().Id);

        // 3. Read vacancy workbook → IndicatorSeriesPoint[]
        var vacancyFiles = Directory.GetFiles(_sourceDir, "*الشواغر العامة*.xlsx");
        var vacancyPath = vacancyFiles.FirstOrDefault(f => 
            Path.GetFileName(f).Contains("المحددات") && Path.GetFileName(f).Contains("الشواغر العامة"))
            ?? vacancyFiles.FirstOrDefault();

        List<IndicatorSeriesPoint> indicatorSeries = new();
        if (vacancyPath != null && File.Exists(vacancyPath))
        {
            try
            {
                using var vacancyWb = new XLWorkbook(vacancyPath);
                indicatorSeries = VacancyReader.Read(vacancyWb, cityIdByNormalizedName);
            }
            catch (RentalControlsReadException ex)
            {
                report.Errors.Add($"Failed to read vacancy: {ex.Message}");
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException)
            {
                report.Errors.Add($"Cannot open vacancy workbook (locked or corrupt): {ex.Message}");
            }
        }
        else
        {
            report.Warnings.Add(new ConversionWarning { Sheet = "vacancy", Issue = "vacancy workbook not found" });
        }

        // 4. Read each city workbook → CityRawData[]
        var cityRawDataList = new List<CityRawData>();
        foreach (var city in cities)
        {
            var cityFile = FindCityFile(_sourceDir, city.Id);
            if (cityFile is null)
            {
                report.Warnings.Add(new ConversionWarning { Sheet = city.Id, Issue = "city workbook not found" });
                continue;
            }

            try
            {
                using var cityWb = new XLWorkbook(cityFile);
                var cityData = CityReader.Read(city.Id, cityWb);
                cityRawDataList.Add(cityData);
            }
            catch (RentalControlsReadException ex)
            {
                report.Errors.Add($"Failed to read city {city.Id}: {ex.Message}");
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException)
            {
                report.Errors.Add($"Cannot open city {city.Id} workbook (locked or corrupt): {ex.Message}");
            }
        }

        // 4b. Read media-monitoring workbook and merge per-city blocks (optional).
        MergeMediaMonitoring(cityRawDataList, report);

        // 5. Read the supply/demand gap workbook (optional).
        var supplyDemandGap = ReadSupplyDemandGap(report);

        // 6. Hand-written report narratives (source folder city-reports/<cityId>.json), imported with the data. Without
        //    the folder every report would lose its narrative, so the import stops before writing anything.
        if (ReadNarratives(cities, report) is not { } narratives) return report;
        var data = new MappedRelease(cities, indicatorSeries, supplyDemandGap,
            cityRawDataList.ToDictionary(c => c.CityId, StringComparer.Ordinal), narratives);

        // The new data is mapped to rows before anything is written, so data the tables cannot hold (an unknown
        // indicator, a bad period) fails the import with every release unchanged.
        var latestQuarter = ReportingPeriod.LatestQuarter(cityRawDataList);
        var live = new ReleaseRow(ReleaseIds.Live, ReleaseKinds.Live, latestQuarter?.Label ?? "", latestQuarter?.Id ?? "",
            latestQuarter?.Label ?? "", _clock.GetLocalNow(), null);
        ReleaseData liveRows;
        try
        {
            liveRows = ReleaseMapper.ToRows(live, data);
        }
        catch (InvalidOperationException ex)
        {
            report.Errors.Add($"The imported data cannot be stored: {ex.Message}");
            return report;
        }

        // 7. Checks against the outgoing live (nothing is written when one fails).
        var outgoingRows = await _repository.LoadReleaseAsync(ReleaseIds.Live, cancellationToken);
        var outgoing = outgoingRows is null ? null : ReleaseMapper.FromRows(outgoingRows);
        var outgoingQuarter = outgoing is null ? null : ReportingPeriod.LatestQuarter(outgoing.CityRaw.Values);
        if (ImportChecks.Rollback(outgoingQuarter, latestQuarter) is { } rollback)
        {
            report.Errors.Add(rollback);
            return report;
        }
        YearQuarter? closing = outgoingQuarter is { } oq && latestQuarter is { } nq && nq > oq ? oq : null;
        var frozen = _repository.GetReleases()
            .Select(r => r.Kind == ReleaseKinds.Quarterly && YearQuarter.TryParse(r.Id, out var q) ? q : (YearQuarter?)null)
            .OfType<YearQuarter>()
            .Concat(closing is { } c ? new[] { c } : Array.Empty<YearQuarter>())
            .ToList();
        YearMonth? safeThrough = frozen.Count > 0 ? frozen.Max().LastMonth : null;
        if (outgoing is not null && ImportChecks.LostHistory(outgoing, data, safeThrough) is { Count: > 0 } lost)
        {
            report.Errors.Add($"The workbooks drop months the dashboard already shows (nothing was written): {string.Join("; ", lost.Take(20))}"
                + (lost.Count > 20 ? $" … ({lost.Count} in all)" : ""));
            return report;
        }

        // 8. A newer quarter closes the outgoing one: freeze the outgoing live (its stored rows and workbooks) as that
        //    quarter, once, with live's corrections, which keep applying to it under their guard.
        if (closing is { } quarter && outgoing is not null && _releases is not null)
        {
            if (_repository.GetReleases().Any(r => r.Id == quarter.Id))
                report.Warnings.Add(new ConversionWarning { Sheet = "snapshot", Issue = $"release {quarter.Id} already exists; kept as it is" });
            else
            {
                var liveSource = Path.Combine(_repository.Location(ReleaseIds.Live), JsonTablesRepository.SourceFolder);
                await _releases.WriteAsync(outgoing, quarter.Id, Directory.Exists(liveSource) ? liveSource : null, cancellationToken: cancellationToken);
                var liveCorrections = await _repository.LoadCorrectionsAsync(ReleaseIds.Live, cancellationToken);
                if (liveCorrections.Corrections.Count > 0 || liveCorrections.Notes.Count > 0)
                    await _repository.WriteCorrectionsAsync(quarter.Id, liveCorrections, cancellationToken);
                report.FilesGenerated.Add(_repository.Location(quarter.Id));
            }
        }

        // 9. The month's as-published latest-month rows, then the live release with its workbooks.
        if (SnapshotPeriod.DetectMonth(cityRawDataList) is { } month)
        {
            var (monthRows, skipped) = MonthlyMovement.Capture(data, month, _clock.GetLocalNow());
            await _repository.WriteMonthlyMovementAsync(
                MonthlyMovement.Upsert(await _repository.LoadMonthlyMovementAsync(cancellationToken), month, monthRows), cancellationToken);
            foreach (var s in skipped)
                report.Warnings.Add(new ConversionWarning { Sheet = "monthly_movement", Issue = $"not stored for {month.Id}: {s}" });
        }
        await _repository.WriteReleaseAsync(liveRows, _sourceDir, cancellationToken);
        report.FilesGenerated.Add(_repository.Location(ReleaseIds.Live));
        var (_, outcomes) = ReleaseCorrections.Apply(liveRows, await _repository.LoadCorrectionsAsync(ReleaseIds.Live, cancellationToken));
        foreach (var o in outcomes)
            report.Warnings.Add(new ConversionWarning { Sheet = "corrections", Issue = $"{(o.Applied ? "applied" : "stale")}: {o.Id} — {o.Detail}" });

        // Warn when the hand-written narratives contradict the newly imported data (stale quarter figures).
        AuditNarratives(cities, cityRawDataList, narratives, report);

        // 10. Populate report
        report.Success = true;
        report.TotalRows = cities.Count;
        report.ValidRows = cities.Count;

        _logger.LogInformation("Wrote the live rental-controls release: {CityCount} cities, {IndicatorCount} indicator points, {GapCityCount} supply-demand-gap cities, {CityDataCount} city data sets, {NarrativeCount} narratives",
            cities.Count, indicatorSeries.Count, supplyDemandGap.Count, cityRawDataList.Count, narratives.Count);

        return report;
    }

    // Null when the narrative folder is missing (an error: importing without it would drop every report narrative).
    private Dictionary<string, CityReportContent>? ReadNarratives(List<CityRecord> cities, ConversionReport report)
    {
        var narratives = new Dictionary<string, CityReportContent>(StringComparer.OrdinalIgnoreCase);
        var dir = Path.Combine(_sourceDir, NarrativeFolder);
        if (!Directory.Exists(dir))
        {
            report.Errors.Add($"Narrative folder not found: {dir} (the report narratives, {NarrativeFolder}/<cityId>.json, are imported with the data).");
            return null;
        }

        var ids = cities.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(dir, "*.json").Order(StringComparer.Ordinal))
        {
            var cityId = Path.GetFileNameWithoutExtension(file);
            if (!ids.Contains(cityId))
            {
                report.Warnings.Add(new ConversionWarning { Sheet = $"city-reports/{cityId}", Issue = "narrative for a city that is not in the overview workbook (skipped)" });
                continue;
            }
            try
            {
                if (JsonSerializer.Deserialize<CityReportContent>(File.ReadAllText(file), JsonDefaults.Read) is { } content)
                    narratives[cityId] = content;
            }
            catch (JsonException ex)
            {
                report.Warnings.Add(new ConversionWarning { Sheet = $"city-reports/{cityId}", Issue = $"unreadable narrative: {ex.Message}" });
            }
        }
        return narratives;
    }

    // Client review (Buraidah): report narratives must follow the imported quarter. Any figure in a city
    // narrative that contradicts the new data becomes an import warning, so the texts get updated immediately.
    private static void AuditNarratives(List<CityRecord> cities, List<CityRawData> cityRawDataList,
        IReadOnlyDictionary<string, CityReportContent> narratives, ConversionReport report)
    {
        var overview = OverviewProjection.Build(cities, id => cityRawDataList.FirstOrDefault(r => r.CityId == id));
        foreach (var raw in cityRawDataList)
        {
            var cityData = overview.Cities.FirstOrDefault(c => c.Id == raw.CityId);
            if (cityData is null || !narratives.TryGetValue(raw.CityId, out var content)) continue;
            foreach (var finding in NarrativeAuditor.Audit(CityProjection.Build(cityData, raw), content))
                report.Warnings.Add(new ConversionWarning { Sheet = $"city-reports/{raw.CityId}", Issue = $"narrative contradicts the imported data — {finding}" });
        }
    }

    private List<RawSupplyDemandGapCity> ReadSupplyDemandGap(ConversionReport report)
    {
        var path = Path.Combine(_sourceDir, SupplyDemandGapFileName);
        if (!File.Exists(path))
        {
            report.Warnings.Add(new ConversionWarning { Sheet = "supply-demand-gap", Issue = $"workbook not found: {SupplyDemandGapFileName}" });
            return new();
        }
        try
        {
            using var wb = new XLWorkbook(path);
            return SupplyDemandGapReader.Read(wb, report.Warnings);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            report.Errors.Add($"Cannot open supply-demand-gap workbook (locked or corrupt): {ex.Message}");
            return new();
        }
    }

    private void MergeMediaMonitoring(List<CityRawData> cityRawDataList, ConversionReport report)
    {
        var path = Path.Combine(_sourceDir, MediaMonitoringFileName);
        if (!File.Exists(path))
        {
            report.Warnings.Add(new ConversionWarning { Sheet = "media-monitoring", Issue = $"workbook not found: {MediaMonitoringFileName}" });
            return;
        }

        Dictionary<string, List<RawMediaMonitoring>> byCity;
        try
        {
            using var wb = new XLWorkbook(path);
            byCity = MediaMonitoringReader.Read(wb);
        }
        catch (RentalControlsReadException ex)
        {
            report.Errors.Add($"Failed to read media-monitoring: {ex.Message}");
            return;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            report.Errors.Add($"Cannot open media-monitoring workbook (locked or corrupt): {ex.Message}");
            return;
        }

        foreach (var city in cityRawDataList)
        {
            if (!byCity.TryGetValue(city.CityId, out var months) || months.Count == 0) continue;
            city.Media = months.OrderBy(m => m.Period, StringComparer.Ordinal).ToList();
        }
    }

    private static string? FindCityFile(string sourceDir, string cityId)
    {
        var token = RentalControlsMappings.CityFileToken(cityId);
        if (token is null) return null;
        var normToken = AqarInsights.Api.Utilities.ArabicNormalizer.Normalize(token.Normalize(System.Text.NormalizationForm.FormC));
        return Directory.GetFiles(sourceDir, $"{CityFilePrefix}*.xlsx")
            .FirstOrDefault(f => AqarInsights.Api.Utilities.ArabicNormalizer.Normalize(Path.GetFileNameWithoutExtension(f).Normalize(System.Text.NormalizationForm.FormC)).Contains(normToken));
    }
}
