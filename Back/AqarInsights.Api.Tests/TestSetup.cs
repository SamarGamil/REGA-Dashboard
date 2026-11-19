using System.Runtime.CompilerServices;

namespace AqarInsights.Api.Tests;

internal static class TestSetup
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }
}
