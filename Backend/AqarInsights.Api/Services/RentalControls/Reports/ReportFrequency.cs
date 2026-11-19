namespace AqarInsights.Api.Services.RentalControls.Reports;

// The data-frequency tag printed beside an indicator, matching the dashboard's شهري / ربعي badges.
public static class ReportFrequency
{
    public const string Monthly = "شهري";
    public const string Quarterly = "ربعي";

    public static string Label(string frequency) => frequency switch
    {
        "monthly" => Monthly,
        "quarterly" => Quarterly,
        _ => "",
    };

    // (background, text) on a light surface — same palette as the dashboard badges.
    public static (string Bg, string Text) Colors(string tag)
        => tag == Monthly ? ("#E8F7F6", "#00848D") : ("#EEF2F6", "#475569");

    // (background, text) on the navy table header.
    public static (string Bg, string Text) OnNavy => ("#2E5267", "#C4CED4");
}
