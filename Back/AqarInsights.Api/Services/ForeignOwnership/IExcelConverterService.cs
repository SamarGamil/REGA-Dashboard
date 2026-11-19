using AqarInsights.Api.Models;
using AqarInsights.Api.Models.ForeignOwnership;

namespace AqarInsights.Api.Services.ForeignOwnership;

public interface IExcelConverterService
{
    string InputDir { get; }
    Task<ConversionReport> ConvertLatestAsync(CancellationToken cancellationToken = default);
    List<(string SheetName, List<string> Columns)> InspectFile();
}
