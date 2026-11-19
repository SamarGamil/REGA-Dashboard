namespace AqarInsights.Api.Models.RentalControls.Reports;

public sealed record CityReportIndicatorFact
{
    public string Label { get; init; } = "";
    public string Value { get; init; } = "—";
    public string Reference { get; init; } = "—";
    public string Status { get; init; } = "—";
    public string Month { get; init; } = "";
    public string Frequency { get; init; } = "";   // "شهري" | "ربعي" | "" (the tag's colour)
    // The period the value belongs to ("سبتمبر", "الربع الثالث"), printed in the tag instead of the frequency word.
    public string Period { get; init; } = "";
    public string DisplayLabel => Label;
}

public sealed record CityReportFacts
{
    public string OperationalStatus { get; init; } = "";
    public string Classification { get; init; } = "";
    public string ClassificationLabel { get; init; } = "";
    public string Recommendation { get; init; } = "";
    public CityReportIndicatorFact Cagr { get; init; } = new();
    public CityReportIndicatorFact RentalIncrease { get; init; } = new();
    public CityReportIndicatorFact RentToIncome { get; init; } = new();
    public CityReportIndicatorFact HousingBurden { get; init; } = new();
    public CityReportIndicatorFact Vacancy { get; init; } = new();
    public CityReportIndicatorFact InvestmentReturn { get; init; } = new();
    public CityReportIndicatorFact ActualRentCpi { get; init; } = new();
    public CityReportIndicatorFact GeneralInflation { get; init; } = new();
}
