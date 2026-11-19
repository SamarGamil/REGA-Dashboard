using AqarInsights.Api.Controllers;
using AqarInsights.Api.Models;
using AqarInsights.Api.Models.ForeignOwnership;
using AqarInsights.Api.Services;
using AqarInsights.Api.Services.ForeignOwnership;
using AqarInsights.Api.Tests.Assistant;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace AqarInsights.Api.Tests;

public class ImportControllerTests
{
    private sealed class NoopConverter : IExcelConverterService
    {
        public string InputDir => Path.GetTempPath();
        public Task<ConversionReport> ConvertLatestAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ConversionReport { Success = true });
        public List<(string SheetName, List<string> Columns)> InspectFile() => new();
    }

    private sealed class NoopLoader : IDataLoaderService
    {
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public IReadOnlyList<TransactionRecord> GetRecords() => new List<TransactionRecord>();
    }

    private static ImportController Build()
        => new(new NoopConverter(), new NoopLoader(), new FakeForeignAgg(), new FakeFilterOptions(), NullLogger<ImportController>.Instance);

    [Fact]
    public async Task Import_rejects_missing_file()
    {
        var r = Assert.IsType<ObjectResult>(await Build().Import(null!));
        Assert.Equal(400, r.StatusCode);
        Assert.IsType<ProblemDetails>(r.Value);
    }

    [Fact]
    public async Task Import_rejects_non_xlsx()
    {
        var file = new FormFile(new MemoryStream(new byte[10]), 0, 10, "file", "data.csv");
        var r = Assert.IsType<ObjectResult>(await Build().Import(file));
        Assert.Equal(400, r.StatusCode);
    }
}
