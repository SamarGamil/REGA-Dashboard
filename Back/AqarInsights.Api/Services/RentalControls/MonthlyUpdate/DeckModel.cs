namespace AqarInsights.Api.Services.RentalControls.MonthlyUpdate;

// A slide reduced to what the media parser needs: text shapes with their slide position (EMU), chart series and
// table rows.
public sealed record DeckText(long X, long Y, IReadOnlyList<string> Paragraphs);
public sealed record DeckSeries(string Name, IReadOnlyList<string> Categories, IReadOnlyList<double?> Values);
public sealed record DeckSlide(int Number, IReadOnlyList<DeckText> Texts, IReadOnlyList<DeckSeries> Series,
    IReadOnlyList<IReadOnlyList<string>> TableRows);
