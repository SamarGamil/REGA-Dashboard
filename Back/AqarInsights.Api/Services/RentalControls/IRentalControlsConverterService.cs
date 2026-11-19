using AqarInsights.Api.Models;

namespace AqarInsights.Api.Services.RentalControls;

public interface IRentalControlsConverterService
{
    Task<ConversionReport> ConvertAllAsync(CancellationToken cancellationToken = default);
}
