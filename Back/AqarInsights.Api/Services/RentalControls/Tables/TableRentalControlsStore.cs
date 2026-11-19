using AqarInsights.Api.Models.RentalControls.Raw;
using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Reports;
using AqarInsights.Api.Services.RentalControls.Store;
using Microsoft.Extensions.Logging.Abstractions;

namespace AqarInsights.Api.Services.RentalControls.Tables;

/// <summary>
/// One release served to the projections (IRentalControlsStore) and the reports (ICityReportContentStore): either a
/// release loaded from the repository on LoadAsync (the live dashboard), or a fixed, already-mapped release.
/// </summary>
public sealed class TableRentalControlsStore : IRentalControlsStore, ICityReportContentStore
{
    private readonly IRentalControlsRepository? _repository;
    private readonly string _releaseId = "";
    private readonly ILogger _logger = NullLogger.Instance;
    private volatile MappedRelease _data;

    public TableRentalControlsStore(IRentalControlsRepository repository, string releaseId, ILogger<TableRentalControlsStore>? logger = null)
    {
        _repository = repository;
        _releaseId = releaseId;
        _logger = (ILogger?)logger ?? NullLogger.Instance;
        _data = MappedRelease.Empty;
    }

    public TableRentalControlsStore(MappedRelease data) => _data = data;

    public MappedRelease Data => _data;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_repository is null) return;
        var (rows, outcomes) = await _repository.LoadCorrectedReleaseAsync(_releaseId, cancellationToken);
        if (rows is null)
        {
            _logger.LogWarning("Rental-controls release {Release} not found at {Location}", _releaseId, _repository.Location(_releaseId));
            _data = MappedRelease.Empty;
            return;
        }
        foreach (var stale in outcomes.Where(o => !o.Applied))
            _logger.LogWarning("Correction {Id} of release {Release} not applied: {Detail}", stale.Id, _releaseId, stale.Detail);
        // Publish the fully-mapped release in a single reference swap.
        _data = ReleaseMapper.FromRows(rows);
    }

    public IReadOnlyList<CityRecord> GetCities() => _data.Cities;

    public IReadOnlyList<IndicatorSeriesPoint> GetIndicatorSeries() => _data.IndicatorSeries;

    public IReadOnlyList<RawSupplyDemandGapCity> GetSupplyDemandGap() => _data.SupplyDemandGap;

    public CityRawData? GetCityRaw(string cityId) => _data.CityRaw.GetValueOrDefault(cityId);

    public CityReportContent? Get(string cityId)
        => string.IsNullOrWhiteSpace(cityId) ? null : _data.ReportContent.GetValueOrDefault(cityId);
}
