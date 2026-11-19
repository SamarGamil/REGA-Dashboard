using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using AqarInsights.Api.Models.RentalControls.Reports;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace AqarInsights.Api.Services.RentalControls.Reports;

/// <summary>Native Word line charts (chart parts), so the plotted values stay readable and editable in Word.</summary>
internal static class CityReportWordCharts
{
    private const uint CategoryAxisId = 111;
    private const uint ValueAxisId = 222;
    private const string MarkerColor = "C0392B";
    private const string LineColor = "2F6F62", PeakColor = "1F5A4D", LastColor = "B91C1C", DecisionColor = "8A6A2F";

    public static Run Inline(MainDocumentPart main, CityReportChartView chart, uint id, long width, long height)
    {
        var part = main.AddNewPart<ChartPart>();
        part.ChartSpace = BuildChartSpace(chart);
        var drawing = new Drawing(new DW.Inline(
            new DW.Extent { Cx = width, Cy = height },
            new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            new DW.DocProperties { Id = id, Name = $"Chart {id}", Description = chart.Title },
            new DW.NonVisualGraphicFrameDrawingProperties(),
            new A.Graphic(new A.GraphicData(new C.ChartReference { Id = main.GetIdOfPart(part) })
            { Uri = "http://schemas.openxmlformats.org/drawingml/2006/chart" }))
        { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U });
        return new Run(drawing);
    }

    // 6,000,000 x 2,300,000 EMU fits the two charts on page two; up to 2,400,000 height is fine.
    private static C.ChartSpace BuildChartSpace(CityReportChartView v)
    {
        var known = v.Values.Select((value, i) => (Index: i, Value: value)).Where(p => p.Value is not null).Select(p => (p.Index, Value: p.Value!.Value)).ToList();
        var (axisMin, axisMax) = AxisRange(known.Select(p => p.Value));
        var marked = v.MarkerIndex >= 0 && v.MarkerIndex < v.Categories.Count;

        // The line chart comes first so the first c:val and the only c:cat belong to the plotted line series.
        var lineChart = new C.LineChart(new C.Grouping { Val = C.GroupingValues.Standard }, new C.VaryColors { Val = false },
            LineSeries(0, v.Title, v.Categories, known));
        if (marked)
            lineChart.Append(MarkerSeries(1, v.MarkerLabel, v.Categories.Count, v.MarkerIndex, axisMax, axisMax - axisMin));
        lineChart.Append(new C.ShowMarker { Val = true }, new C.AxisId { Val = CategoryAxisId }, new C.AxisId { Val = ValueAxisId });

        var areaChart = new C.AreaChart(new C.Grouping { Val = C.GroupingValues.Standard }, new C.VaryColors { Val = false },
            AreaSeries(2, v.Title, v.Categories.Count, Bridged(known)),
            new C.AxisId { Val = CategoryAxisId }, new C.AxisId { Val = ValueAxisId });

        // The inner plot area leaves room above for the decision label, below for the month and year lines, and on the right for the value axis.
        var layout = new C.Layout(new C.ManualLayout(
            new C.LayoutTarget { Val = C.LayoutTargetValues.Inner },
            new C.LeftMode { Val = C.LayoutModeValues.Edge }, new C.TopMode { Val = C.LayoutModeValues.Edge },
            new C.Left { Val = 0.02 }, new C.Top { Val = 0.13 }, new C.Width { Val = 0.91 }, new C.Height { Val = 0.62 }));
        var chart = new C.Chart(
            new C.AutoTitleDeleted { Val = true },
            new C.PlotArea(layout, lineChart, areaChart, CategoryAxis(), ValueAxis(axisMin, axisMax)));
        chart.Append(new C.PlotVisibleOnly { Val = true }, new C.DisplayBlanksAs { Val = C.DisplayBlanksAsValues.Span });

        return new C.ChartSpace(
            new C.Date1904 { Val = false },
            new C.RoundedCorners { Val = false },
            chart,
            new C.ChartShapeProperties(new A.SolidFill(new A.RgbColorModelHex { Val = "FFFFFF" }), new A.Outline(new A.NoFill())));
    }

    // The shading is continuous between the first and last known month (months the data lacks are interpolated).
    private static List<(int Index, double Value)> Bridged(IReadOnlyList<(int Index, double Value)> known)
    {
        var result = new List<(int Index, double Value)>();
        for (var k = 0; k < known.Count; k++)
        {
            result.Add(known[k]);
            if (k + 1 == known.Count) break;
            var (a, b) = (known[k], known[k + 1]);
            for (var i = a.Index + 1; i < b.Index; i++)
                result.Add((i, a.Value + (b.Value - a.Value) * (i - a.Index) / (b.Index - a.Index)));
        }
        return result;
    }

    // Value axis range in whole 5% steps, with headroom above the highest point for its data label.
    internal static (double Min, double Max) AxisRange(IEnumerable<double> values)
    {
        var list = values.ToList();
        if (list.Count == 0) return (0, 5);
        var min = Math.Min(0, Math.Floor(list.Min() / 5) * 5);
        var top = list.Max();
        var max = Math.Max(0, Math.Ceiling(top / 5) * 5);
        if (max - top < 1.5) max += 5;
        if (max <= min) max = min + 5;
        return (min, max);
    }

    private static C.TextProperties Text(int size, bool bold = false, string? color = null) => new(
        new A.BodyProperties(),
        new A.ListStyle(),
        new A.Paragraph(
            new A.ParagraphProperties(color is null
                ? new A.DefaultRunProperties { FontSize = size, Bold = bold }
                : new A.DefaultRunProperties(new A.SolidFill(new A.RgbColorModelHex { Val = color })) { FontSize = size, Bold = bold }),
            new A.EndParagraphRunProperties { Language = "ar-SA" }));

    private static C.DataLabel PointLabel(int index, double value, int size, string color, bool below)
        => new(
            new C.Index { Val = (uint)index },
            new C.NumberingFormat { FormatCode = "0.0", SourceLinked = false },
            new C.ChartShapeProperties(new A.NoFill(), new A.Outline(new A.NoFill())),
            Text(size, true, color),
            new C.DataLabelPosition { Val = below ? C.DataLabelPositionValues.Bottom : C.DataLabelPositionValues.Top },
            new C.ShowLegendKey { Val = false },
            new C.ShowValue { Val = true },
            new C.ShowCategoryName { Val = false },
            new C.ShowSeriesName { Val = false },
            new C.ShowPercent { Val = false },
            new C.ShowBubbleSize { Val = false });

    private static C.DataPoint EmphasisedPoint(int index, string color)
        => new(
            new C.Index { Val = (uint)index },
            new C.Marker(new C.Symbol { Val = C.MarkerStyleValues.Circle }, new C.Size { Val = 8 },
                new C.ChartShapeProperties(new A.SolidFill(new A.RgbColorModelHex { Val = color }), new A.Outline(new A.SolidFill(new A.RgbColorModelHex { Val = color })))));

    private static C.LineChartSeries LineSeries(uint index, string name, IReadOnlyList<string> categories, IReadOnlyList<(int Index, double Value)> points)
    {
        var series = new C.LineChartSeries(
            new C.Index { Val = index }, new C.Order { Val = index },
            new C.SeriesText(new C.NumericValue(name)),
            new C.ChartShapeProperties(new A.Outline(new A.SolidFill(new A.RgbColorModelHex { Val = LineColor })) { Width = 17780 }),
            new C.Marker(new C.Symbol { Val = C.MarkerStyleValues.Circle }, new C.Size { Val = 4 },
                new C.ChartShapeProperties(new A.SolidFill(new A.RgbColorModelHex { Val = LineColor }))));
        // The highest value is a larger dark dot with a bold label; the latest value is the red dot (below the line when negative).
        var labels = new List<C.DataLabel>();
        if (points.Count > 0)
        {
            var peak = points.OrderByDescending(p => p.Value).First();
            var last = points[^1];
            if (peak.Index != last.Index)
            {
                series.Append(EmphasisedPoint(peak.Index, PeakColor));
                labels.Add(PointLabel(peak.Index, peak.Value, 900, "111827", false));
            }
            series.Append(EmphasisedPoint(last.Index, LastColor));
            labels.Add(PointLabel(last.Index, last.Value, 800, LastColor, false));
        }
        var dataLabels = new C.DataLabels();
        foreach (var l in labels.OrderBy(l => l.Index!.Val!.Value)) dataLabels.Append(l);
        dataLabels.Append(
            new C.NumberingFormat { FormatCode = "0.0", SourceLinked = false },
            new C.ChartShapeProperties(new A.NoFill(), new A.Outline(new A.NoFill())),
            Text(700),
            new C.DataLabelPosition { Val = C.DataLabelPositionValues.Top },
            new C.ShowLegendKey { Val = false },
            new C.ShowValue { Val = true },
            new C.ShowCategoryName { Val = false },
            new C.ShowSeriesName { Val = false },
            new C.ShowPercent { Val = false },
            new C.ShowBubbleSize { Val = false });
        series.Append(
            dataLabels,
            Categories(categories),
            Numbers(categories.Count, points),
            new C.Smooth { Val = false });
        return series;
    }

    // Light shading under the line; shares the axes, has no categories and no labels.
    private static C.AreaChartSeries AreaSeries(uint index, string name, int count, IReadOnlyList<(int Index, double Value)> points)
        => new(
            new C.Index { Val = index }, new C.Order { Val = index },
            new C.SeriesText(new C.NumericValue(name)),
            new C.ChartShapeProperties(
                new A.SolidFill(new A.RgbColorModelHex(new A.Alpha { Val = 80000 }) { Val = "DCE8EA" }),
                new A.Outline(new A.NoFill())),
            Numbers(count, points));

    // One point at the decision month on the axis top, labelled with the series name above the plot; its downward error bar is
    // the dotted vertical line to the axis floor.
    private static C.LineChartSeries MarkerSeries(uint index, string name, int count, int at, double top, double span)
        => new(
            new C.Index { Val = index }, new C.Order { Val = index },
            new C.SeriesText(new C.NumericValue(name)),
            new C.ChartShapeProperties(new A.Outline(new A.NoFill())),
            new C.Marker(new C.Symbol { Val = C.MarkerStyleValues.None }),
            new C.DataLabels(
                new C.ChartShapeProperties(new A.NoFill(), new A.Outline(new A.NoFill())),
                Text(700, false, DecisionColor),
                new C.DataLabelPosition { Val = C.DataLabelPositionValues.Top },
                new C.ShowLegendKey { Val = false },
                new C.ShowValue { Val = false },
                new C.ShowCategoryName { Val = false },
                new C.ShowSeriesName { Val = true },
                new C.ShowPercent { Val = false },
                new C.ShowBubbleSize { Val = false }),
            new C.ErrorBars(
                new C.ErrorDirection { Val = C.ErrorBarDirectionValues.Y },
                new C.ErrorBarType { Val = C.ErrorBarValues.Minus },
                new C.ErrorBarValueType { Val = C.ErrorValues.FixedValue },
                new C.NoEndCap { Val = true },
                new C.ErrorBarValue { Val = span },
                new C.ChartShapeProperties(new A.Outline(
                    new A.SolidFill(new A.RgbColorModelHex { Val = DecisionColor }),
                    new A.PresetDash { Val = A.PresetLineDashValues.SystemDot }) { Width = 12700 })),
            Numbers(count, new[] { (at, top) }),
            new C.Smooth { Val = false });

    private static C.CategoryAxisData Categories(IReadOnlyList<string> categories)
    {
        var literal = new C.StringLiteral(new C.PointCount { Val = (uint)categories.Count });
        for (var i = 0; i < categories.Count; i++)
            literal.Append(new C.StringPoint(new C.NumericValue(AxisLabel(categories[i], i))) { Index = (uint)i });
        return new C.CategoryAxisData(literal);
    }

    // The month or quarter name; the year follows on a second line under each year start (and the first point), as in the platform's chart.
    private static string AxisLabel(string category, int index)
    {
        var (name, year) = SplitLabel(category);
        return year.Length > 0 && (index == 0 || StartsYear(name)) ? $"{name}\n{year}" : name;
    }

    // "يناير 2025" -> ("يناير", "2025"); "الربع الأول 2025" -> ("الربع الأول", "2025").
    internal static (string Name, string Year) SplitLabel(string category)
    {
        var cut = category.LastIndexOf(' ');
        return cut > 0 && category[(cut + 1)..].All(char.IsDigit) ? (category[..cut], category[(cut + 1)..]) : (category, "");
    }

    internal static bool StartsYear(string name) => name is "يناير" or "الربع الأول";

    private static C.Values Numbers(int count, IReadOnlyList<(int Index, double Value)> points)
    {
        var literal = new C.NumberLiteral(new C.FormatCode("0.0"), new C.PointCount { Val = (uint)count });
        foreach (var (index, value) in points)
            literal.Append(new C.NumericPoint(new C.NumericValue(value.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture))) { Index = (uint)index });
        return new C.Values(literal);
    }

    private static C.CategoryAxis CategoryAxis() => new(
        new C.AxisId { Val = CategoryAxisId },
        new C.Scaling(new C.Orientation { Val = C.OrientationValues.MaxMin }),
        new C.Delete { Val = false },
        new C.AxisPosition { Val = C.AxisPositionValues.Bottom },
        new C.NumberingFormat { FormatCode = "General", SourceLinked = false },
        new C.MajorTickMark { Val = C.TickMarkValues.None },
        new C.MinorTickMark { Val = C.TickMarkValues.None },
        new C.TickLabelPosition { Val = C.TickLabelPositionValues.Low },
        new C.ChartShapeProperties(new A.Outline(new A.SolidFill(new A.RgbColorModelHex { Val = "6B7280" })) { Width = 9525 }),
        new C.TextProperties(
            new A.BodyProperties { Rotation = 0, Vertical = A.TextVerticalValues.Horizontal },
            new A.ListStyle(),
            new A.Paragraph(new A.ParagraphProperties(new A.DefaultRunProperties { FontSize = 600 }), new A.EndParagraphRunProperties { Language = "ar-SA" })),
        new C.CrossingAxis { Val = ValueAxisId },
        new C.Crosses { Val = C.CrossesValues.AutoZero },
        new C.AutoLabeled { Val = true },
        new C.LabelAlignment { Val = C.LabelAlignmentValues.Center },
        new C.LabelOffset { Val = 100 },
        new C.TickLabelSkip { Val = 1 },
        new C.NoMultiLevelLabels { Val = false });

    private static C.ValueAxis ValueAxis(double min, double max) => new(
        new C.AxisId { Val = ValueAxisId },
        new C.Scaling(new C.Orientation { Val = C.OrientationValues.MinMax }, new C.MaxAxisValue { Val = max }, new C.MinAxisValue { Val = min }),
        new C.Delete { Val = false },
        new C.AxisPosition { Val = C.AxisPositionValues.Right },
        new C.MajorGridlines(new C.ChartShapeProperties(new A.Outline(new A.SolidFill(new A.RgbColorModelHex { Val = "E5E7EB" })))),
        new C.NumberingFormat { FormatCode = "0\"%\"", SourceLinked = false },
        new C.MajorTickMark { Val = C.TickMarkValues.None },
        new C.MinorTickMark { Val = C.TickMarkValues.None },
        new C.TickLabelPosition { Val = C.TickLabelPositionValues.NextTo },
        Text(800),
        new C.CrossingAxis { Val = CategoryAxisId },
        new C.Crosses { Val = C.CrossesValues.AutoZero },
        new C.CrossBetween { Val = C.CrossBetweenValues.Between },
        new C.MajorUnit { Val = 5D });
}
