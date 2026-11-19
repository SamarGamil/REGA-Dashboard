namespace AqarInsights.Api.Models.RentalControls;

// Bound from appsettings "KpiThresholds". Boundaries (%) that map each city KPI value to a
// status (سلبي/محايد/إيجابي) and monitoring priority. Defaults preserve the built-in behavior.
public class KpiThresholdsOptions
{
    // Higher is worse: >=High → سلبي, >=Med → محايد, else إيجابي.
    public double GrowthHigh { get; set; } = 10;
    public double GrowthMed { get; set; } = 7;

    // Lower is worse: <Tight → سلبي, <=Med → محايد, else إيجابي.
    public double VacancyTight { get; set; } = 5;
    public double VacancyMed { get; set; } = 8;

    // Lower is worse: <Low → سلبي, <Min → محايد, else إيجابي.
    public double YieldLow { get; set; } = 4;
    public double YieldMin { get; set; } = 6;

    // Higher is worse.
    public double RentToIncomeHigh { get; set; } = 40;
    public double RentToIncomeMed { get; set; } = 30;

    // Higher is worse.
    public double BurdenHigh { get; set; } = 40;
    public double BurdenMed { get; set; } = 35;
}
