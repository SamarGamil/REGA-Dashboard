namespace AqarInsights.Api.Services.RentalControls.MonthlyUpdate;

public sealed class MonthlyUpdateReport
{
    public List<string> Changes { get; } = new();
    public List<string> Warnings { get; } = new();
}
