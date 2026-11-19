using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using AqarInsights.Api.Models.RentalControls.Reports;

namespace AqarInsights.Api.Services.RentalControls.Reports;

public static class CityReportWordRenderer
{
    private const string Font = "Sakkal Majalla";
    private const string Navy = "123A52";
    private const string Teal = "1CA59A";
    private const string Ink = "374151";
    private const string Muted = "6B7280";
    private const string Line = "E5E7EB";
    private const string BarBg = "F1F4F6";
    private const string Zebra = "F4FBFA";

    private static readonly byte[] Logo = LoadLogo();

    private static byte[] LoadLogo()
    {
        using var s = typeof(CityReportWordRenderer).Assembly
            .GetManifestResourceStream("AqarInsights.Api.Resources.Images.rega-logo.png");
        if (s == null) return Array.Empty<byte>();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    public static byte[] Render(CityReportModel model)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            doc.PackageProperties.Title = $"{model.HeaderTitle} – {model.PeriodLabel}";
            doc.PackageProperties.Subject = model.PeriodLine;
            doc.PackageProperties.Creator = "الهيئة العامة للعقار";
            var main = doc.AddMainDocumentPart();
            main.Document = new Document();
            var body = main.Document.AppendChild(new Body());

            var headerId = BuildHeaderPart(main, model);
            var footerId = BuildFooterPart(main, model);

            // الملخص التنفيذي
            var summary = new List<OpenXmlElement>
            {
                SectionHeader("الملخص التنفيذي", "E08A3C"),
                Para(new[]
                {
                    TextRun("حالة السوق:  ", 20, bold: true, color: Navy),
                    TextRun(model.MarketState, 20, bold: true, color: model.MarketStateColorHex.TrimStart('#')),
                }, spacingAfter: 80, spacingBefore: 40),
                KpiTable(model.KpiCards),
            };
            summary.AddRange(model.ExecutiveBullets.Select(b => Bullet(b)));
            AddSection(body, summary);

            // لوحة المؤشرات
            var indicators = new List<OpenXmlElement> { SectionHeader("لوحة المؤشرات للضوابط الإيجارية", Navy) };
            indicators.AddRange(model.IndicatorsBullets.Select(b => Bullet(b)));
            indicators.Add(IndicatorsTable(model.IndicatorRows));
            AddSection(body, indicators);

            // حركة السوق والأسعار
            var market = new List<OpenXmlElement> { SectionHeader("حركة السوق والأسعار", Teal) };
            market.AddRange(model.MarketBullets.Select(b => Bullet(b)));
            if (!string.IsNullOrWhiteSpace(model.CityAveragesLine))
                market.Add(Bullet(model.CityAveragesLine));
            market.Add(ProductTable(model.ProductRows));
            if (CityReportMarketBuilder.HasSmallBase(model.ProductRows))
                market.Add(Para(CityReportMarketBuilder.SmallBaseFootnote, 15, color: Muted, spacingBefore: 40));
            if (model.SupplyInvestment.Count > 0)
                market.Add(SupplyInvestmentTable(model.SupplyInvestment));
            AddSection(body, market);

            // الرصد الإعلامي
            var mediaSection = new List<OpenXmlElement> { SectionHeader("الرصد الإعلامي", "1C8C8C") };
            if (model.MediaAvailable)
            {
                mediaSection.Add(Para(new[]
                {
                    TextRun("الانطباع العام: ", 20, bold: true, color: Navy),
                    TextRun(model.MediaSentiment, 20, bold: true, color: model.MediaSentimentColorHex.TrimStart('#')),
                    TextRun("    (" + model.MediaMonthPrefix, 18, color: Muted),
                    TextRun($"إيجابي {model.MediaPositivePct}%", 18, bold: true, color: "2E9B5B"),
                    TextRun(" — ", 18, color: Muted),
                    TextRun($"محايد {model.MediaNeutralPct}%", 18, bold: true, color: Muted),
                    TextRun(" — ", 18, color: Muted),
                    TextRun($"سلبي {model.MediaNegativePct}%", 18, bold: true, color: "DC2626"),
                    TextRun(")", 18, color: Muted),
                }, spacingAfter: 60, spacingBefore: 40));
                mediaSection.AddRange(model.MediaBullets.Select(b => Bullet(b)));
            }
            else
            {
                mediaSection.AddRange(model.MediaBullets.Select(b => Bullet(b, color: Muted)));
            }
            AddSection(body, mediaSection);

            // أبرز الاستنتاجات والتوصية
            var conclusions = new List<OpenXmlElement> { SectionHeader("أبرز الاستنتاجات والتوصية", "C0392B") };
            conclusions.AddRange(model.ConclusionBullets.Select(b => Bullet(b)));
            conclusions.Add(AxesTable(model.Axes));
            conclusions.Add(Para(new[]
            {
                TextRun("التقييم العام: ", 20, bold: true, color: Navy),
                TextRun(model.OverallAssessment + ".", 20, bold: true, color: model.MarketStateColorHex.TrimStart('#')),
            }, spacingAfter: 60, spacingBefore: 80, line: 288));
            conclusions.Add(Para("التوصية", 20, bold: true, color: Navy, spacingAfter: 20, spacingBefore: 40));
            conclusions.AddRange(model.RecommendationBullets.Select(b => Bullet(b)));
            AddSection(body, conclusions);

            // ما يجب مراقبته
            var watch = new List<OpenXmlElement> { SectionHeader(model.WatchTitle, Navy) };
            watch.AddRange(model.WatchList.Select(b => Bullet(b)));
            AddSection(body, watch);

            if (model.Charts.Count > 0)
            {
                var charts = new List<OpenXmlElement>();
                uint chartId = 100;
                foreach (var chart in model.Charts)
                {
                    charts.Add(Para(chart.Title, 20, bold: true, color: Navy, spacingAfter: 20, spacingBefore: 60));
                    if (chart.Values.All(v => v is null))
                    {
                        charts.Add(Para("لا تتوفر بيانات لهذه الفترة.", 16, color: Muted, spacingAfter: 40, spacingBefore: 0));
                        continue;
                    }
                    charts.Add(new Paragraph(
                        new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                        CityReportWordCharts.Inline(main, chart, chartId++, 6000000L, 2300000L)));
                }
                AddSection(body, charts);
            }

            body.AppendChild(SectionLetterRtl(headerId, footerId));
            main.Document.Save();
        }
        return ms.ToArray();
    }

    // ---- text helpers ----

    // Appends a section preceded by a gap; every paragraph but the last gets KeepNext and every
    // table row CantSplit, so Word moves the whole section to the next page instead of splitting it.
    private static void AddSection(Body body, IReadOnlyList<OpenXmlElement> blocks)
    {
        var paragraphs = blocks
            .SelectMany(b => b is Paragraph p ? new[] { p } : b.Descendants<Paragraph>())
            .ToList();
        for (int i = 0; i < paragraphs.Count - 1; i++)
        {
            var pp = paragraphs[i].GetFirstChild<ParagraphProperties>();
            if (pp == null) paragraphs[i].PrependChild(new ParagraphProperties(new KeepNext()));
            else pp.PrependChild(new KeepNext());
        }
        foreach (var row in blocks.SelectMany(b => b.Descendants<TableRow>()))
        {
            var rp = row.GetFirstChild<TableRowProperties>();
            if (rp == null) row.PrependChild(new TableRowProperties(new CantSplit()));
            else rp.PrependChild(new CantSplit());
        }
        body.AppendChild(Gap(360));
        foreach (var b in blocks) body.AppendChild(b);
    }

    private static Run TextRun(string text, int halfPts, bool bold = false, string? color = null)
    {
        var rp = new RunProperties(
            new RunFonts { Ascii = Font, HighAnsi = Font, ComplexScript = Font },
            new FontSize { Val = halfPts.ToString() },
            new FontSizeComplexScript { Val = halfPts.ToString() },
            new RightToLeftText());
        if (bold) { rp.AppendChild(new Bold()); rp.AppendChild(new BoldComplexScript()); }
        if (color != null) rp.AppendChild(new Color { Val = color });
        // '\n' in a label (e.g. the CPI month line) becomes a real line break.
        var run = new Run(rp);
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0) run.AppendChild(new Break());
            run.AppendChild(new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
        }
        return run;
    }

    // Indicator name and its period tag (coloured by the indicator's frequency).
    private static IEnumerable<Run> TaggedRuns(string label, string frequency, string period, int halfPts, bool bold = false, string? color = null)
    {
        var lines = label.Split('\n', 2);
        yield return TextRun(lines[0], halfPts, bold, color);
        if (period.Length > 0)
        {
            var (bg, fg) = ReportFrequency.Colors(frequency);
            yield return TextRun(" ", halfPts);
            yield return ChipRun("\u00a0" + period + "\u00a0", halfPts - 2, fg.TrimStart('#'), bg.TrimStart('#'));
        }
        if (lines.Length > 1)
        {
            var month = TextRun(lines[1], halfPts, bold, color);
            month.InsertAfter(new Break(), month.RunProperties);
            yield return month;
        }
    }

    private static Run ChipRun(string text, int halfPts, string color, string fill)
    {
        var rp = new RunProperties(
            new RunFonts { Ascii = Font, HighAnsi = Font, ComplexScript = Font },
            new FontSize { Val = halfPts.ToString() },
            new FontSizeComplexScript { Val = halfPts.ToString() },
            new Shading { Val = ShadingPatternValues.Clear, Fill = fill },
            new Color { Val = color },
            new RightToLeftText());
        return new Run(rp, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    private static Paragraph Para(string text, int halfPts, bool bold = false, string? color = null,
        JustificationValues? align = null, string? shadingFill = null, int spacingAfter = 20, int spacingBefore = 20, int? line = null)
        => Para(new[] { TextRun(text, halfPts, bold, color) }, align, shadingFill, spacingAfter, spacingBefore, line);

    private static Paragraph Para(IEnumerable<Run> runs, JustificationValues? align = null,
        string? shadingFill = null, int spacingAfter = 20, int spacingBefore = 20, int? line = null)
    {
        var spacing = new SpacingBetweenLines { After = spacingAfter.ToString(), Before = spacingBefore.ToString() };
        if (line != null) { spacing.Line = line.ToString(); spacing.LineRule = LineSpacingRuleValues.Auto; }
        var pp = new ParagraphProperties(new BiDi(), spacing);
        if (align != null) pp.AppendChild(new Justification { Val = align });
        if (shadingFill != null) pp.AppendChild(new Shading { Val = ShadingPatternValues.Clear, Fill = shadingFill });
        var p = new Paragraph(pp);
        foreach (var r in runs) p.AppendChild(r);
        return p;
    }

    // Latin/numeric run left-to-right so '%','+','-' stay on the correct side inside RTL layout.
    private static Run NumRun(string text, int halfPts, bool bold = false, string? color = null)
    {
        var rp = new RunProperties(
            new RunFonts { Ascii = Font, HighAnsi = Font, ComplexScript = Font },
            new FontSize { Val = halfPts.ToString() },
            new FontSizeComplexScript { Val = halfPts.ToString() });
        if (bold) { rp.AppendChild(new Bold()); rp.AppendChild(new BoldComplexScript()); }
        if (color != null) rp.AppendChild(new Color { Val = color });
        return new Run(rp, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    private static bool IsNumeric(string t) => !string.IsNullOrWhiteSpace(t) &&
        t.All(c => char.IsDigit(c) || "+-.,%()\u25b2\u25bc/\u00a0 ".Contains(c));

    // Hanging-indent bullet: wrapped lines align with the text, not under the bullet symbol.
    private static Paragraph Bullet(string text, string? color = null)
    {
        var pp = new ParagraphProperties(
            new BiDi(),
            new Indentation { Start = "320", Hanging = "320" },
            new SpacingBetweenLines { After = "80", Before = "20", Line = "288", LineRule = LineSpacingRuleValues.Auto });
        var p = new Paragraph(pp);
        p.AppendChild(TextRun("●", 16, color: Teal));
        p.AppendChild(new Run(new RunProperties(new RunFonts { Ascii = Font, HighAnsi = Font, ComplexScript = Font }), new TabChar()));
        p.AppendChild(TextRun(text, 20, color: color ?? Ink));
        return p;
    }

    private static Paragraph Divider(string color, int size) => new(
        new ParagraphProperties(
            new SpacingBetweenLines { After = "40", Before = "40" },
            new ParagraphBorders(new BottomBorder { Val = BorderValues.Single, Size = (UInt32Value)(uint)size, Color = color })));

    // ---- header ----
    // ---- header part (REGA logo + title + subtitle + navy divider; image lives in the header part) ----
    private static string BuildHeaderPart(MainDocumentPart main, CityReportModel m)
    {
        var headerPart = main.AddNewPart<HeaderPart>();
        var relId = main.GetIdOfPart(headerPart);

        var titleCell = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "3400" }, VAlignCenter()),
            Para(m.HeaderTitle, 28, bold: true, color: Navy, spacingAfter: 20),
            Para(m.HeaderSubtitle, 17, color: Teal, spacingBefore: 0));
        TableCell logoCell;
        if (Logo.Length > 0)
        {
            var imgPart = headerPart.AddImagePart(ImagePartType.Png);
            using (var s = new MemoryStream(Logo)) imgPart.FeedData(s);
            logoCell = LogoCell(headerPart.GetIdOfPart(imgPart));
        }
        else logoCell = new TableCell(new Paragraph());

        var table = new Table(BorderlessProps(), Grid(8200, 2400), new TableRow(titleCell, logoCell));
        headerPart.Header = new Header(table, Divider(Navy, 12));
        return relId;
    }

    private static TableCell LogoCell(string embedRelId)
    {
        var (w, h) = PngSize(Logo);
        long cx = 1500000L; long cy = h > 0 ? cx * h / w : 620000L;
        var drawing = new Drawing(new DW.Inline(
            new DW.Extent { Cx = cx, Cy = cy },
            new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            new DW.DocProperties { Id = 1U, Name = "REGA" },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            new A.Graphic(new A.GraphicData(new PIC.Picture(
                new PIC.NonVisualPictureProperties(
                    new PIC.NonVisualDrawingProperties { Id = 0U, Name = "rega.png" },
                    new PIC.NonVisualPictureDrawingProperties()),
                new PIC.BlipFill(new A.Blip { Embed = embedRelId }, new A.Stretch(new A.FillRectangle())),
                new PIC.ShapeProperties(
                    new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = cx, Cy = cy }),
                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
            { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
        { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U });
        return new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "1600" }, VAlignCenter()),
            new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Left }), new Run(drawing)));
    }

    // ---- section header: thin tall colored right-marker bar beside title; gap is a separate empty paragraph ----
    private static Paragraph SectionHeader(string title, string markerColor)
    {
        var pp = new ParagraphProperties(
            new ParagraphBorders(
                new RightBorder { Val = BorderValues.Single, Size = 26U, Space = 6U, Color = markerColor }),
            new SpacingBetweenLines { Before = "0", After = "80", Line = "240", LineRule = LineSpacingRuleValues.Auto },
            new Indentation { Right = "130" },
            new BiDi());
        var p = new Paragraph(pp);
        p.AppendChild(TextRun(title, 24, bold: true, color: Navy));
        return p;
    }

    private static Paragraph Gap(int twips) => new(new ParagraphProperties(
        new SpacingBetweenLines { Before = "0", After = "0", Line = twips.ToString(), LineRule = LineSpacingRuleValues.Exact }));

    // ---- KPI strip ----
    private static Table KpiTable(List<CityReportKpiCardView> cards)
    {
        var row = new TableRow();
        foreach (var card in cards)
        {
            var color = card.ColorHex.TrimStart('#');
            var paragraphs = new List<OpenXmlElement>
            {
                Para(TaggedRuns(card.Label, card.Frequency, card.Period, 18, color: Muted), spacingAfter: 40),
                Para(new[] { NumRun(card.ValueText, 30, bold: true, color: color) }, spacingAfter: 30, spacingBefore: 0),
                Para(card.Note, 16, spacingBefore: 0),
            };
            if (!string.IsNullOrEmpty(card.Reference))
                paragraphs.Add(Para(card.Reference, 16, color: Muted, spacingBefore: 20));
            var cell = new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = (5000 / Math.Max(cards.Count, 1)).ToString() },
                    new TableCellBorders(
                        new TopBorder { Val = BorderValues.Single, Size = 4, Color = Line },
                        new BottomBorder { Val = BorderValues.Single, Size = 4, Color = Line },
                        new LeftBorder { Val = BorderValues.Single, Size = 4, Color = Line },
                        new RightBorder { Val = BorderValues.Single, Size = 30, Color = color }),
                    new TableCellMargin(
                        new TopMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                        new BottomMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                        new LeftMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                        new RightMargin { Width = "100", Type = TableWidthUnitValues.Dxa })));
            cell.Append(paragraphs);
            row.AppendChild(cell);
        }
        var widths = Enumerable.Repeat(10600 / Math.Max(cards.Count, 1), Math.Max(cards.Count, 1)).ToArray();
        return new Table(BorderlessProps(), Grid(widths), row);
    }

    // ---- indicators table ----
    private static Table IndicatorsTable(List<CityReportIndicatorRowView> rows)
    {
        var table = new Table(GridTableProps(), Grid(3600, 1600, 2600, 2800));
        table.AppendChild(HeaderRow("المؤشر", "القيمة", "الحد المرجعي للمراقبة", "الحالة"));
        for (int i = 0; i < rows.Count; i++)
        {
            var bg = i % 2 == 1 ? Zebra : "FFFFFF";
            var row = new TableRow(new TableRowProperties(new TableRowHeight { Val = 320U, HeightType = HeightRuleValues.AtLeast }));
            row.AppendChild(new TableCell(
                new TableCellProperties(new Shading { Val = ShadingPatternValues.Clear, Fill = bg }, VAlignCenter()),
                Para(TaggedRuns(rows[i].Indicator, rows[i].Frequency, rows[i].Period, 18, bold: true, color: Navy))));
            row.AppendChild(Cell(rows[i].Value, bg));
            row.AppendChild(Cell(rows[i].Reference, bg));
            row.AppendChild(new TableCell(
                new TableCellProperties(new Shading { Val = ShadingPatternValues.Clear, Fill = rows[i].StatusBgHex.TrimStart('#') }, VAlignCenter()),
                Para(rows[i].Status, 18, bold: true, color: rows[i].StatusColorHex.TrimStart('#'))));
            table.AppendChild(row);
        }
        return table;
    }

    // ---- product table ----
    private static Table ProductTable(List<CityReportProductRow> rows)
    {
        var table = new Table(GridTableProps(), Grid(1600, 3000, 3000, 3000));
        table.AppendChild(HeaderRow("المنتج", "الصفقات الإيجارية", "الصفقات البيعية", "الرقم القياسي"));
        for (int i = 0; i < rows.Count; i++)
        {
            var bg = i % 2 == 1 ? Zebra : "FFFFFF";
            var row = new TableRow(new TableRowProperties(new TableRowHeight { Val = 320U, HeightType = HeightRuleValues.AtLeast }));
            row.AppendChild(Cell(rows[i].Product, bg, bold: true));
            row.AppendChild(Cell(rows[i].RentalDeals, bg));
            row.AppendChild(Cell(rows[i].SalesDeals, bg));
            row.AppendChild(Cell(rows[i].FisherIndex, bg));
            table.AppendChild(row);
        }
        return table;
    }

    // ---- العرض والاستثمار strip: navy band + one row of label/value/trend cells ----
    private static Table SupplyInvestmentTable(List<CityReportSupplyStatView> stats)
    {
        int n = Math.Max(stats.Count, 1);
        var widths = Enumerable.Repeat(10600 / n, n).ToArray();
        var table = new Table(GridTableProps(), Grid(widths));

        var band = new TableRow(new TableRowProperties(new TableRowHeight { Val = 320U, HeightType = HeightRuleValues.AtLeast }));
        band.AppendChild(new TableCell(
            new TableCellProperties(new GridSpan { Val = n }, new Shading { Val = ShadingPatternValues.Clear, Fill = Navy }, VAlignCenter()),
            Para("العرض والاستثمار", 18, bold: true, color: "FFFFFF")));
        table.AppendChild(band);

        var row = new TableRow(new TableRowProperties(new TableRowHeight { Val = 500U, HeightType = HeightRuleValues.AtLeast }));
        foreach (var s in stats)
        {
            var color = s.ColorHex.TrimStart('#');
            var cell = new TableCell(
                new TableCellProperties(VAlignCenter()),
                Para(TaggedRuns(s.Label, s.Frequency, s.Period, 16, color: Muted), spacingAfter: 20, spacingBefore: 40),
                Para(new[]
                {
                    NumRun(s.Value, 22, bold: true, color: color),
                    TextRun("  " + s.Note, 16, color: color),
                }, spacingAfter: s.Reference.Length > 0 ? 10 : 40, spacingBefore: 0));
            if (s.Reference.Length > 0)
                cell.AppendChild(Para(s.Reference, 14, color: Muted, spacingAfter: 40, spacingBefore: 0));
            row.AppendChild(cell);
        }
        table.AppendChild(row);
        return table;
    }

    // ---- axes table ----
    private static Table AxesTable(List<CityReportAxis> axes)
    {
        var table = new Table(GridTableProps(), Grid(5300, 5300));
        table.AppendChild(HeaderRow("المحور", "الحالة"));
        for (int i = 0; i < axes.Count; i++)
        {
            var bg = i % 2 == 1 ? Zebra : "FFFFFF";
            var row = new TableRow(new TableRowProperties(new TableRowHeight { Val = 320U, HeightType = HeightRuleValues.AtLeast }));
            row.AppendChild(Cell(axes[i].Axis, bg, bold: true));
            row.AppendChild(axes[i].ColorHex.Length == 0
                ? Cell(axes[i].Status, bg)
                : new TableCell(
                    new TableCellProperties(new Shading { Val = ShadingPatternValues.Clear, Fill = axes[i].BgHex.TrimStart('#') }, VAlignCenter()),
                    Para(axes[i].Status, 18, bold: true, color: axes[i].ColorHex.TrimStart('#'), align: JustificationValues.Center, spacingAfter: 20, spacingBefore: 20)));
            table.AppendChild(row);
        }
        return table;
    }

    private static TableRow HeaderRow(params string[] headers)
    {
        var row = new TableRow(new TableRowProperties(new TableRowHeight { Val = 360U, HeightType = HeightRuleValues.AtLeast }));
        foreach (var h in headers)
            row.AppendChild(new TableCell(
                new TableCellProperties(new Shading { Val = ShadingPatternValues.Clear, Fill = Navy }, VAlignCenter()),
                Para(h, 18, bold: true, color: "FFFFFF")));
        return row;
    }

    private static TableCell Cell(string text, string bg, bool bold = false) => new(
        new TableCellProperties(new Shading { Val = ShadingPatternValues.Clear, Fill = bg }, VAlignCenter()),
        IsNumeric(text)
            ? Para(new[] { NumRun(text, 18, bold, bold ? Navy : Ink) }, null, null, 20, 20)
            : Para(text, 18, bold: bold, color: bold ? Navy : Ink));

    // ---- footer part (org · city · quarter + live page number) ----
    private static string BuildFooterPart(MainDocumentPart main, CityReportModel m)
    {
        var footerPart = main.AddNewPart<FooterPart>();
        var relId = main.GetIdOfPart(footerPart);
        var rightCell = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "3400" }),
            Para($"الهيئة العامة للعقار · مدينة {m.CityName} · {m.PeriodLabel}", 14, color: Muted));
        var leftCell = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "1600" }),
            PageNumberPara($"تاريخ الإنشاء: {m.GeneratedOnLabel} · "));
        var table = new Table(BorderlessProps(), Grid(7134, 3466), new TableRow(rightCell, leftCell));
        footerPart.Footer = new Footer(table, new Paragraph());
        return relId;
    }

    private static Paragraph PageNumberPara(string prefix = "")
    {
        var p = new Paragraph(new ParagraphProperties(new BiDi(), new Justification { Val = JustificationValues.Left }));
        p.AppendChild(TextRun(prefix + "صفحة ", 14, color: Muted));
        var num = new Run(
            new RunProperties(new RunFonts { Ascii = Font, HighAnsi = Font, ComplexScript = Font },
                new FontSize { Val = "14" }, new FontSizeComplexScript { Val = "14" }, new Color { Val = Muted }),
            new Text("1"));
        p.AppendChild(new SimpleField(num) { Instruction = "PAGE" });
        return p;
    }

    // ---- shared props ----
    private static TableGrid Grid(params int[] widths)
    {
        var g = new TableGrid();
        foreach (var w in widths) g.AppendChild(new GridColumn { Width = w.ToString() });
        return g;
    }

    private static TableProperties BorderlessProps() => new(
        new TableWidth { Type = TableWidthUnitValues.Dxa, Width = "10600" },
        new TableLayout { Type = TableLayoutValues.Fixed },
        new BiDiVisual(),
        new TableBorders(
            new TopBorder { Val = BorderValues.None }, new BottomBorder { Val = BorderValues.None },
            new LeftBorder { Val = BorderValues.None }, new RightBorder { Val = BorderValues.None },
            new InsideHorizontalBorder { Val = BorderValues.None }, new InsideVerticalBorder { Val = BorderValues.None }));

    private static TableProperties GridTableProps() => new(
        new TableWidth { Type = TableWidthUnitValues.Dxa, Width = "10600" },
        new TableLayout { Type = TableLayoutValues.Fixed },
        new TableCellMarginDefault(
            new TableCellLeftMargin { Width = 10, Type = TableWidthValues.Dxa },
            new TableCellRightMargin { Width = 10, Type = TableWidthValues.Dxa }),
        new BiDiVisual(),
        new TableBorders(
            new TopBorder { Val = BorderValues.None },
            new BottomBorder { Val = BorderValues.Single, Size = 4, Color = Line },
            new LeftBorder { Val = BorderValues.None },
            new RightBorder { Val = BorderValues.None },
            new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = Line },
            new InsideVerticalBorder { Val = BorderValues.None }));

    private static TableCellVerticalAlignment VAlignCenter() => new() { Val = TableVerticalAlignmentValues.Center };

    // Matches the reference: US Letter, RTL, with header/footer part references.
    private static SectionProperties SectionLetterRtl(string headerId, string footerId) => new(
        new HeaderReference { Type = HeaderFooterValues.Default, Id = headerId },
        new FooterReference { Type = HeaderFooterValues.Default, Id = footerId },
        new PageSize { Width = 12240U, Height = 15840U },
        new PageMargin { Top = 1200, Bottom = 520, Left = 820, Right = 820, Header = 0, Footer = 360, Gutter = 0 },
        new BiDi());

    private static (int w, int h) PngSize(byte[] png)
    {
        if (png.Length < 24) return (1, 1);
        int w = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        int h = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
        return (w == 0 ? 1 : w, h);
    }
}
