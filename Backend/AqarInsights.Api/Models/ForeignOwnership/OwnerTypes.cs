namespace AqarInsights.Api.Models.ForeignOwnership;

public static class OwnerTypes
{
    public const string Individual = "فرد";
    public const string Company = "شركة";
    public static readonly IReadOnlyList<string> All = [Individual, Company];
}
