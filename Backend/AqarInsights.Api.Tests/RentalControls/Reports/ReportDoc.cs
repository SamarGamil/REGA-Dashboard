using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using AqarInsights.Api.Services.RentalControls.Reports;

namespace AqarInsights.Api.Tests.RentalControls.Reports;

// A generated Word report read back the way a reader sees it: paragraphs, table rows, header/footer, coloured runs.
internal sealed class ReportDoc
{
    public sealed record ColoredText(string Text, string? Color, string? Fill);

    public IReadOnlyList<string> Paragraphs { get; }
    public IReadOnlyList<IReadOnlyList<IReadOnlyList<string>>> Tables { get; }
    public IReadOnlyList<Table> TableElements { get; }
    public IReadOnlyList<IReadOnlyList<ColoredText>> TableRuns { get; }
    public IReadOnlyList<OpenXmlElement> BodyElements { get; }
    public IReadOnlyList<ColoredText> Runs { get; }
    public int ChartPartCount { get; }
    public int BodyImageCount { get; }
    public IReadOnlyList<string> ChartXml { get; }
    public string HeaderText { get; }
    public string FooterText { get; }

    // Body + header + footer, paragraphs joined by newlines (so a phrase never spans two paragraphs by accident).
    public string FullText => string.Join("\n", Paragraphs) + "\n" + HeaderText + "\n" + FooterText;

    private ReportDoc(WordprocessingDocument doc)
    {
        var main = doc.MainDocumentPart!;
        var body = main.Document.Body!;
        Paragraphs = body.Descendants<Paragraph>().Select(p => p.InnerText).Where(t => t.Length > 0).ToList();
        var top = body.Descendants<Table>().Where(t => !t.Ancestors<Table>().Any()).ToList();
        TableElements = top.Select(t => (Table)t.CloneNode(true)).ToList();
        Tables = top.Select(t => (IReadOnlyList<IReadOnlyList<string>>)t.Elements<TableRow>()
            .Select(r => (IReadOnlyList<string>)r.Elements<TableCell>().Select(c => c.InnerText).ToList()).ToList()).ToList();
        TableRuns = top.Select(t => (IReadOnlyList<ColoredText>)RunsOf(t)).ToList();
        BodyElements = body.ChildElements.Select(e => e.CloneNode(true)).ToList();
        Runs = RunsOf(body).Concat(main.HeaderParts.SelectMany(h => RunsOf(h.Header!))).ToList();
        HeaderText = string.Concat(main.HeaderParts.Select(h => h.Header!.InnerText));
        FooterText = string.Concat(main.FooterParts.Select(f => f.Footer!.InnerText));
        ChartPartCount = main.ChartParts.Count();
        BodyImageCount = main.ImageParts.Count();
        ChartXml = main.ChartParts.Select(c => { using var r = new StreamReader(c.GetStream()); return r.ReadToEnd(); }).ToList();
    }

    private static List<ColoredText> RunsOf(OpenXmlElement root) => root.Descendants<Run>()
        .Select(r => new ColoredText(r.InnerText, r.RunProperties?.Color?.Val?.Value, r.RunProperties?.Shading?.Fill?.Value))
        .Where(r => r.Text.Length > 0).ToList();

    public static ReportDoc Load(ReportFile file)
    {
        using var ms = new MemoryStream(file.Content);
        using var doc = WordprocessingDocument.Open(ms, false);
        return new ReportDoc(doc);
    }

    // Arabic text as compared in the checks: diacritics and tatweel dropped, no-break spaces and runs of spaces collapsed.
    public static string Norm(string s) => Regex.Replace(
        Regex.Replace(s, "[\u064B-\u0652\u0640\u200E\u200F]", "").Replace('\u00a0', ' '), @"\s+", " ").Trim();

    public string NormText => Norm(FullText);

    public int ParagraphIndex(string phrase) => Paragraphs.ToList().FindIndex(p => Norm(p).Contains(Norm(phrase), StringComparison.Ordinal));

    public string Dump()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("== HEADER ==").AppendLine(HeaderText).AppendLine("== FOOTER ==").AppendLine(FooterText);
        sb.AppendLine("== PARAGRAPHS ==");
        foreach (var p in Paragraphs) sb.AppendLine(p.Replace('\n', '|').Replace('\u00a0', '~'));
        sb.AppendLine("== TABLES ==");
        for (var i = 0; i < Tables.Count; i++)
        {
            sb.AppendLine($"-- table {i} --");
            foreach (var row in Tables[i]) sb.AppendLine(string.Join(" ¦ ", row.Select(c => c.Replace('\n', '|').Replace('\u00a0', '~'))));
        }
        return sb.ToString();
    }
}
