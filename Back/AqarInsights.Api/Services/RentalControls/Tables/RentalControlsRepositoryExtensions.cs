using AqarInsights.Api.Models.RentalControls.Tables;

namespace AqarInsights.Api.Services.RentalControls.Tables;

public static class RentalControlsRepositoryExtensions
{
    /// <summary>A release with its guarded corrections applied (what every reader serves); null when not stored.</summary>
    public static async Task<(ReleaseData? Data, IReadOnlyList<CorrectionOutcome> Outcomes)> LoadCorrectedReleaseAsync(
        this IRentalControlsRepository repository, string releaseId, CancellationToken cancellationToken = default)
    {
        if (await repository.LoadReleaseAsync(releaseId, cancellationToken) is not { } rows) return (null, []);
        var corrections = await repository.LoadCorrectionsAsync(releaseId, cancellationToken);
        return ReleaseCorrections.Apply(rows, corrections);
    }
}
