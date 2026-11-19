namespace AqarInsights.Api.Models.RentalControls;

// Bound from appsettings "RentalPolicy". Policy limits (%) shared by the rental-controls projections.
public class RentalPolicyOptions
{
    // Rent-to-income / low-income housing-burden monitoring limit: ≤ limit = ضمن الحد, above = متجاوز.
    public int RentBurdenLimitPct { get; set; } = 30;

    // Rental vacancy floor: ≥ limit = ضمن الحد, below = دون الحد.
    public int VacancyLimitPct { get; set; } = 5;

    // Investment-return (yield) activation floor: ≥ limit = ضمن الحد, below = دون الحد.
    public int YieldActivationPct { get; set; } = 6;

    // Rental-increase bands: ≤ WithinMax = ضمن الحد, ≥ WatchMax = تجاوز مرتفع, between = متابعة.
    public int GrowthWithinMax { get; set; } = 6;
    public int GrowthWatchMax { get; set; } = 10;
}
