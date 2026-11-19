using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using P = DocumentFormat.OpenXml.Presentation;

namespace AqarInsights.Api.Services.RentalControls.MonthlyUpdate;

public static class PptxDeckLoader
{
    public static List<DeckSlide> Load(string path)
    {
        using var doc = PresentationDocument.Open(path, false);
        var presentation = doc.PresentationPart ?? throw new InvalidDataException($"Not a presentation: {path}");
        var slides = new List<DeckSlide>();
        var number = 0;
        foreach (var id in presentation.Presentation.SlideIdList?.Elements<P.SlideId>() ?? [])
        {
            number++;
            var slide = (SlidePart)presentation.GetPartById(id.RelationshipId!);
            slides.Add(new DeckSlide(number, Texts(slide), Series(slide).ToList(), TableRows(slide)));
        }
        return slides;
    }

    private static List<DeckText> Texts(SlidePart slide)
    {
        var texts = new List<DeckText>();
        foreach (var shape in slide.Slide.Descendants<P.Shape>())
        {
            var paragraphs = shape.Descendants<A.Paragraph>().Select(Text).Where(t => t.Length > 0).ToList();
            if (paragraphs.Count == 0) continue;
            var (x, y) = Offset(shape);
            texts.Add(new DeckText(x, y, paragraphs));
        }
        return texts;
    }

    // Slide position of a shape; a grouped shape is mapped through its groups' child transforms.
    private static (long X, long Y) Offset(P.Shape shape)
    {
        var off = shape.ShapeProperties?.Transform2D?.Offset;
        double x = off?.X?.Value ?? 0, y = off?.Y?.Value ?? 0;
        for (var g = shape.Parent as P.GroupShape; g is not null; g = g.Parent as P.GroupShape)
        {
            var t = g.GroupShapeProperties?.TransformGroup;
            if (t?.Offset is null || t.Extents is null || t.ChildOffset is null || t.ChildExtents is null) continue;
            var sx = t.ChildExtents.Cx!.Value == 0 ? 1 : (double)t.Extents.Cx!.Value / t.ChildExtents.Cx.Value;
            var sy = t.ChildExtents.Cy!.Value == 0 ? 1 : (double)t.Extents.Cy!.Value / t.ChildExtents.Cy.Value;
            x = t.Offset.X!.Value + (x - t.ChildOffset.X!.Value) * sx;
            y = t.Offset.Y!.Value + (y - t.ChildOffset.Y!.Value) * sy;
        }
        return ((long)x, (long)y);
    }

    private static IEnumerable<DeckSeries> Series(SlidePart slide)
    {
        foreach (var chart in slide.ChartParts)
        foreach (var ser in chart.ChartSpace.Descendants().Where(e => e.LocalName == "ser"))
        {
            var name = string.Concat(ser.Elements<C.SeriesText>().SelectMany(t => t.Descendants<C.NumericValue>()).Select(v => v.Text)).Trim();
            var categories = Points(ser.Elements<C.CategoryAxisData>().FirstOrDefault()).Select(p => p ?? "").ToList();
            var values = Points(ser.Elements<C.Values>().FirstOrDefault())
                .Select(p => double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : (double?)null)
                .ToList();
            yield return new DeckSeries(name, categories, values);
        }
    }

    // Point texts by c:pt idx up to c:ptCount; a point the chart leaves out is null.
    private static List<string?> Points(OpenXmlElement? data)
    {
        if (data is null) return [];
        var count = (int)(data.Descendants<C.PointCount>().FirstOrDefault()?.Val?.Value ?? 0);
        var points = Enumerable.Repeat<string?>(null, count).ToList();
        foreach (var pt in data.Descendants().Where(e => e.LocalName == "pt"))
        {
            var idx = int.Parse(pt.GetAttribute("idx", "").Value!, CultureInfo.InvariantCulture);
            while (points.Count <= idx) points.Add(null);
            points[idx] = pt.Descendants<C.NumericValue>().FirstOrDefault()?.Text;
        }
        return points;
    }

    private static List<IReadOnlyList<string>> TableRows(SlidePart slide)
        => slide.Slide.Descendants<A.TableRow>()
            .Select(r => (IReadOnlyList<string>)r.Elements<A.TableCell>()
                .Select(c => string.Join(" ", c.Descendants<A.Paragraph>().Select(Text).Where(t => t.Length > 0)))
                .ToList())
            .ToList();

    private static string Text(A.Paragraph p) => string.Concat(p.Descendants<A.Text>().Select(t => t.Text)).Trim();
}
