using System.Text.Encodings.Web;
using System.Text.Json;
using AqarInsights.Api.Models.RentalControls.Reports;
using AqarInsights.Api.Services;
using AqarInsights.Api.Tests.RentalControls.Reports;

namespace AqarInsights.Api.Tests.RentalControls.Tables;

// Reports serve the narrative stored in the live release; its source files only take effect through
// --import-controls. This fails when a source file was edited but not imported (or the other way round).
public class NarrativeSourceTests
{
    private static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    [Fact]
    public void Narrative_sources_are_imported_into_the_live_release()
    {
        var files = Directory.GetFiles(RealReportData.NarrativeSourceDir, "*.json");

        Assert.Equal(
            files.Select(f => Path.GetFileNameWithoutExtension(f)).Order(StringComparer.Ordinal),
            RealReportData.LiveRelease.ReportContent.Keys.Order(StringComparer.Ordinal));
        foreach (var file in files)
        {
            var source = JsonSerializer.Deserialize<CityReportContent>(File.ReadAllText(file), JsonDefaults.Read);
            Assert.Equal(JsonSerializer.Serialize(source, Json),
                JsonSerializer.Serialize(RealReportData.LiveContent.Get(Path.GetFileNameWithoutExtension(file)), Json));
        }
    }
}
