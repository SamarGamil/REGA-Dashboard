using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using AqarInsights.Api.Models.RentalControls.Reports;

namespace AqarInsights.Api.Services.RentalControls.Reports;

public static class NationalReportWordRenderer
{
    private const string Font = "Sakkal Majalla";
    private const string Navy = "123A52";
    private const string Teal = "1CA59A";
    private const string Muted = "6B7280";
    private const string Line = "E5E7EB";
    private const string BarBg = "F1F4F6";
    private const string Zebra = "F4FBFA";

    private static readonly byte[] Logo = LoadLogo();

    private static byte[] LoadLogo()
    {
        using var s = typeof(NationalReportWordRenderer).Assembly
            .GetManifestResourceStream("AqarInsights.Api.Resources.Images.rega-logo.png");
        if (s == null) return Array.Empty<byte>();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    public static byte[] Render(NationalReportModel model)
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

            body.AppendChild(Gap(120));
            body.AppendChild(SectionHeader("الملخص التنفيذي", "E08A3C"));
            foreach (var line in model.SummaryLines)
                body.AppendChild(Bullet(line));
            body.AppendChild(KpiTable(model.KpiCards));

            body.AppendChild(Gap(120));
            body.AppendChild(SectionHeader(model.Title, Navy));
            if (!string.IsNullOrWhiteSpace(model.SortNote))
                body.AppendChild(Para(model.SortNote, 15, color: Muted, spacingAfter: 40, spacingBefore: 0));
            body.AppendChild(CityTable(model));
            body.AppendChild(LegendPara());

            body.AppendChild(Gap(120));
            body.AppendChild(InterventionTable(model));

            if (model.Impact.Count > 0)
            {
                body.AppendChild(Gap(120));
                body.AppendChild(SectionHeader("أثر الضوابط الإيجارية المفعلة", Teal));
                foreach (var city in model.Impact)
                {
                    body.AppendChild(Para(city.CityName, 18, bold: true, color: Navy, spacingAfter: 20, spacingBefore: 40));
                    foreach (var point in city.Points) body.AppendChild(Bullet(point));
                }
            }

            body.AppendChild(SectionLetterRtl(headerId, footerId));
            main.Document.Save();
        }
        return ms.ToArray();
    }

    // ---- text helpers ----
    private static Run TextRun(string text, int halfPts, bool bold = false, string? color = null)
    {
        var rp = new RunProperties(
            new RunFonts { Ascii = Font, HighAnsi = Font, ComplexScript = Font },
            new FontSize { Val = halfPts.ToString() },
            new FontSizeComplexScript { Val = halfPts.ToString() },
            new RightToLeftText());
        if (bold) { rp.AppendChild(new Bold()); rp.AppendChild(new BoldComplexScript()); }
        if (color != null) rp.AppendChild(new Color { Val = color });
        return new Run(rp, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    private static Paragraph Para(string text, int halfPts, bool bold = false, string? color = null,
        JustificationValues? align = null, string? shadingFill = null, int spacingAfter = 20, int spacingBefore = 20)
        => Para(new[] { TextRun(text, halfPts, bold, color) }, align, shadingFill, spacingAfter, spacingBefore);

    private static Paragraph Para(IEnumerable<Run> runs, JustificationValues? align = null,
        string? shadingFill = null, int spacingAfter = 20, int spacingBefore = 20)
    {
        var pp = new ParagraphProperties(new BiDi(), new SpacingBetweenLines { After = spacingAfter.ToString(), Before = spacingBefore.ToString() });
        if (align != null) pp.AppendChild(new Justification { Val = align });
        if (shadingFill != null) pp.AppendChild(new Shading { Val = ShadingPatternValues.Clear, Fill = shadingFill });
        var p = new Paragraph(pp);
        foreach (var r in runs) p.AppendChild(r);
        return p;
    }

    // Latin/numeric run rendered left-to-right so neutral chars ('%', '+', '-') stay on the
    // correct side of the digits inside the RTL layout (e.g. "+16.4%" not "%16.4+").
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

    private static Paragraph NumPara(string text, int halfPts, bool bold = false, string? color = null,
        int spacingAfter = 20, int spacingBefore = 20)
        => Para(new[] { NumRun(text, halfPts, bold, color) }, null, null, spacingAfter, spacingBefore);

    // Bold colored run on a tinted background — the inline "pill" for intervention labels.
    private static Run BadgeRun(string text, int halfPts, string color, string fill)
    {
        var rp = new RunProperties(
            new RunFonts { Ascii = Font, HighAnsi = Font, ComplexScript = Font },
            new FontSize { Val = halfPts.ToString() },
            new FontSizeComplexScript { Val = halfPts.ToString() },
            new Shading { Val = ShadingPatternValues.Clear, Fill = fill },
            new Bold(), new BoldComplexScript(),
            new Color { Val = color },
            new RightToLeftText());
        return new Run(rp, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    private static Paragraph Divider(string color, int size) => new(
        new ParagraphProperties(
            new SpacingBetweenLines { After = "40", Before = "40" },
            new ParagraphBorders(new BottomBorder { Val = BorderValues.Single, Size = (UInt32Value)(uint)size, Color = color })));

    // Hanging-indent bullet: wrapped lines align with the text, not under the bullet symbol.
    private static Paragraph Bullet(string text)
    {
        var pp = new ParagraphProperties(
            new BiDi(),
            new Indentation { Start = "320", Hanging = "320" },
            new SpacingBetweenLines { After = "60", Before = "20", Line = "276", LineRule = LineSpacingRuleValues.Auto });
        var p = new Paragraph(pp);
        p.AppendChild(TextRun("●", 14, color: Teal));
        p.AppendChild(new Run(new RunProperties(new RunFonts { Ascii = Font, HighAnsi = Font, ComplexScript = Font }), new TabChar()));
        p.AppendChild(TextRun(text, 18, color: "374151"));
        return p;
    }

    // ---- header part (REGA logo + title + subtitle + navy divider; image lives in the header part) ----
    private static string BuildHeaderPart(MainDocumentPart main, NationalReportModel m)
    {
        var headerPart = main.AddNewPart<HeaderPart>();
        var relId = main.GetIdOfPart(headerPart);

        var titleCell = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "3400" }, VAlignCenter()),
            Para(m.HeaderTitle, 28, bold: true, color: Navy, spacingAfter: 20),
            Para(m.AssessmentLine, 17, color: Muted, spacingBefore: 0));

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

    // ---- section header: colored block marker (run shading, renders reliably even inside table cells) beside title; gap is separate ----
    private static Paragraph SectionHeader(string title, string markerColor)
    {
        var pp = new ParagraphProperties(
            new SpacingBetweenLines { Before = "0", After = "40", Line = "240", LineRule = LineSpacingRuleValues.Auto },
            new Indentation { Right = "130" },
            new BiDi());
        var p = new Paragraph(pp);
        p.AppendChild(MarkerRun(markerColor));
        p.AppendChild(TextRun("  " + title, 18, bold: true, color: Navy));
        return p;
    }

    // A short run whose background is the marker color = a thin colored bar beside the title.
    private static Run MarkerRun(string fill)
    {
        var rp = new RunProperties(
            new RunFonts { Ascii = Font, HighAnsi = Font, ComplexScript = Font },
            new FontSize { Val = "22" }, new FontSizeComplexScript { Val = "22" },
            new Shading { Val = ShadingPatternValues.Clear, Fill = fill },
            new RightToLeftText());
        return new Run(rp, new Text("\u00a0") { Space = SpaceProcessingModeValues.Preserve });
    }

    // ---- KPI ----
    private static Table KpiTable(List<ReportKpiCard> cards)
    {
        var row = new TableRow();
        foreach (var card in cards)
        {
            var color = card.ColorHex.TrimStart('#');
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
                        new RightMargin { Width = "100", Type = TableWidthUnitValues.Dxa })),
                Para(LabelRuns(card.Label, card.Frequency, card.Period), spacingAfter: 30),
                Para(new[] { TextRun(card.ValueText == "—" ? "" : card.IsUp ? "▲ " : "▼ ", 16, color: color), NumRun(card.ValueText, 30, bold: true, color: color) }, spacingAfter: 20, spacingBefore: 0),
                Para(card.CityName, 17, spacingBefore: 0));
            row.AppendChild(cell);
        }
        return new Table(BorderlessProps(), Grid(2650, 2650, 2650, 2650), row);
    }

    private static IEnumerable<Run> LabelRuns(string label, string frequency, string period)
    {
        yield return TextRun(label, 16, color: Muted);
        if (period.Length == 0) yield break;
        var (bg, fg) = ReportFrequency.Colors(frequency);
        yield return TextRun(" ", 16);
        yield return BadgeRun("\u00a0" + period + "\u00a0", 14, fg.TrimStart('#'), bg.TrimStart('#'));
    }

    // ---- city table (color-coded indicator cells + legend) ----
    private static Table CityTable(NationalReportModel model)
    {
        var rows = model.Rows;
        string P(string key) => model.IndicatorPeriods.GetValueOrDefault(key, "");
        var table = new Table(GridTableProps(), Grid(600, 2000, 2100, 1500, 1500, 1500, 1400));
        table.AppendChild(HeaderRow(
            ("#", ""), ("المدينة", ""), ("المسار", ""),
            ("الزيادة الإيجارية", P("rental-increase")), ("نسبة الإيجار إلى الدخل", P("rent-to-income")),
            ("العبء السكني لذوي الدخل المحدود", P("housing-burden")), ("الإيجار الفعلي للمسكن CPI", P("actual-rent"))));
        var burdenWidth = rows.Select(r => r.BurdenText.Length).DefaultIfEmpty(0).Max();
        var rentWidth = rows.Select(r => r.ActualRentText.Length).DefaultIfEmpty(0).Max();
        for (int i = 0; i < rows.Count; i++)
            table.AppendChild(BodyRow(rows[i], i % 2 == 1 ? Zebra : "FFFFFF", burdenWidth, rentWidth));
        return table;
    }

    private static TableRow HeaderRow(params (string Text, string Tag)[] headers)
    {
        var (tagBg, tagFg) = ReportFrequency.OnNavy;
        var row = new TableRow(new TableRowProperties(new TableRowHeight { Val = 300U, HeightType = HeightRuleValues.AtLeast }));
        foreach (var (text, tag) in headers)
        {
            var runs = new List<Run> { TextRun(text, 18, bold: true, color: "FFFFFF") };
            if (tag.Length > 0)
            {
                // Always below the name: narrow columns would wrap it there anyway.
                runs.Add(new Run(new Break()));
                runs.Add(BadgeRun("\u00a0" + tag + "\u00a0", 14, tagFg.TrimStart('#'), tagBg.TrimStart('#')));
            }
            row.AppendChild(new TableCell(
                new TableCellProperties(new Shading { Val = ShadingPatternValues.Clear, Fill = Navy }, VAlignCenter()),
                Para(runs)));
        }
        return row;
    }

    private static TableRow BodyRow(ReportCityRow r, string bg, int burdenWidth, int rentWidth)
    {
        TableCell C(Paragraph p) => new(new TableCellProperties(new Shading { Val = ShadingPatternValues.Clear, Fill = bg }, VAlignCenter()), p);
        // Value cell tinted with its platform status colour; the trend arrow, or «—» when the value is unchanged, sits on
        // the figure's left as in the PDF. Word lays these Latin runs out left to right, so the symbol run comes first;
        // figure spaces (each a digit wide) pad a shorter figure to the column's longest, so the symbols of a column line
        // up while the figures stay right-aligned.
        TableCell V(string text, string colorHex, string trend = "", int width = 0)
        {
            var c = colorHex.TrimStart('#');
            var runs = new List<Run>();
            if (trend.Length > 0)
                runs.Add(NumRun(trend + " " + new string('\u2007', Math.Max(0, width - text.Length)), 18, bold: true,
                    color: trend == "↑" ? "DC2626" : trend == "↓" ? "2E9B5B" : Muted));
            runs.Add(NumRun(text, 18, bold: true, color: c));
            return new TableCell(
                new TableCellProperties(new Shading { Val = ShadingPatternValues.Clear, Fill = Tint(c) }, VAlignCenter()),
                Para(runs, null, null, 20, 20));
        }
        var track = r.TrackColorHex.TrimStart('#');
        var row = new TableRow(new TableRowProperties(new TableRowHeight { Val = 250U, HeightType = HeightRuleValues.AtLeast }));
        row.AppendChild(C(NumPara(r.Rank.ToString(), 18, color: Muted)));
        row.AppendChild(C(Para(r.CityName, 18)));
        row.AppendChild(new TableCell(
            new TableCellProperties(new Shading { Val = ShadingPatternValues.Clear, Fill = Tint(track) }, VAlignCenter()),
            Para(r.TrackLabel, 16, bold: true, color: track)));
        row.AppendChild(V(r.RentalIncreaseText, r.RentalIncreaseColorHex));
        row.AppendChild(V(r.RentToIncomeText, r.RentToIncomeColorHex));
        row.AppendChild(V(r.BurdenText, r.BurdenColorHex, r.BurdenTrend, burdenWidth));
        row.AppendChild(V(r.ActualRentText, r.RentColorHex, r.ActualRentTrend, rentWidth));
        return row;
    }

    // Legend for the indicator cell colors.
    private static Paragraph LegendPara()
    {
        var p = new Paragraph(new ParagraphProperties(new BiDi(), new SpacingBetweenLines { Before = "60", After = "60" }));
        p.AppendChild(TextRun("دلالة الألوان:  ", 14, bold: true, color: Muted));
        void Chip(string label, string color)
        {
            p.AppendChild(BadgeRun("\u00a0" + label + "\u00a0", 14, color, Tint(color)));
            p.AppendChild(TextRun("   ", 14));
        }
        Chip("ضمن الحد المرجعي للمراقبة", "2E9B5B");
        Chip("قرب الحد المرجعي للمراقبة", "C67A1E");
        Chip("يتجاوز الحد المرجعي للمراقبة", "DC2626");
        Chip("غير متاح", "6B7280");
        return p;
    }

    // ---- intervention (two columns on one line: right = priorities header + groups table; left = monitoring header + bullets) ----
    private static Table InterventionTable(NationalReportModel m)
    {
        var right = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "2900" }, VAlignTop()));
        right.AppendChild(SectionHeader("أولويات التدخل بالمدن", "C0392B"));
        right.AppendChild(InterventionGroupsTable(m));
        right.AppendChild(new Paragraph()); // a cell must end with a paragraph after a nested table

        var left = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "2100" }, VAlignTop()));
        left.AppendChild(SectionHeader(m.MonitoringTitle, Teal));
        foreach (var b in m.MonitoringBullets)
            left.AppendChild(Para(new[] { TextRun("• ", 18, color: Teal), TextRun(b, 16, color: "374151") }, spacingAfter: 40));

        return new Table(BorderlessProps(), Grid(5300, 5300), new TableRow(right, left));
    }

    // Each group is a row: tinted colored label cell + cities cell, separated by thin horizontal rules.
    private static Table InterventionGroupsTable(NationalReportModel m)
    {
        var table = new Table(
            new TableProperties(
                new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" },
                new TableLayout { Type = TableLayoutValues.Fixed },
                new BiDiVisual(),
                new TableBorders(
                    new TopBorder { Val = BorderValues.None },
                    new BottomBorder { Val = BorderValues.Single, Size = 4, Color = Line },
                    new LeftBorder { Val = BorderValues.None },
                    new RightBorder { Val = BorderValues.None },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = Line },
                    new InsideVerticalBorder { Val = BorderValues.None })),
            Grid(1900, 3100));
        foreach (var g in m.InterventionGroups)
        {
            var color = g.ColorHex.TrimStart('#');
            var labelCell = new TableCell(
                new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "1900" },
                    new Shading { Val = ShadingPatternValues.Clear, Fill = Tint(color) }, VAlignCenter()),
                Para(g.Label, 16, bold: true, color: color, spacingAfter: 30, spacingBefore: 30));
            var citiesCell = new TableCell(
                new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "3100" }, VAlignCenter()),
                Para(string.Join("، ", g.Cities), 16, color: "374151", spacingAfter: 30, spacingBefore: 30));
            table.AppendChild(new TableRow(labelCell, citiesCell));
        }
        if (!m.InterventionGroups.Any())
            table.AppendChild(new TableRow(new TableCell(new Paragraph()), new TableCell(new Paragraph())));
        return table;
    }

    private static Paragraph Gap(int twips) => new(new ParagraphProperties(
        new SpacingBetweenLines { Before = "0", After = "0", Line = twips.ToString(), LineRule = LineSpacingRuleValues.Exact }));

    // ---- footer part (org · quarter + live page number) ----
    private static string BuildFooterPart(MainDocumentPart main, NationalReportModel m)
    {
        var footerPart = main.AddNewPart<FooterPart>();
        var relId = main.GetIdOfPart(footerPart);
        var rightCell = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "3400" }),
            Para($"الهيئة العامة للعقار · {m.PeriodLabel}", 14, color: Muted));
        var leftCell = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "1600" }),
            PageNumberPara());
        var table = new Table(BorderlessProps(), Grid(7134, 3466), new TableRow(rightCell, leftCell));
        footerPart.Footer = new Footer(table, new Paragraph());
        return relId;
    }

    private static Paragraph PageNumberPara()
    {
        var p = new Paragraph(new ParagraphProperties(new BiDi(), new Justification { Val = JustificationValues.Left }));
        p.AppendChild(TextRun("صفحة ", 14, color: Muted));
        var num = new Run(
            new RunProperties(new RunFonts { Ascii = Font, HighAnsi = Font, ComplexScript = Font },
                new FontSize { Val = "14" }, new FontSizeComplexScript { Val = "14" }, new Color { Val = Muted }),
            new Text("1"));
        p.AppendChild(new SimpleField(num) { Instruction = "PAGE" });
        return p;
    }

    // ---- executive-summary callout (light teal box + leading accent bar, like the reference) ----
    private static Table SummaryCallout(List<string> lines)
    {
        var cell = new TableCell(new TableCellProperties(
            new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "10600" },
            new Shading { Val = ShadingPatternValues.Clear, Fill = "EAF5F3" },
            new TableCellBorders(
                new LeftBorder { Val = BorderValues.Single, Size = 24, Color = Teal },
                new TopBorder { Val = BorderValues.None }, new BottomBorder { Val = BorderValues.None },
                new RightBorder { Val = BorderValues.None }),
            new TableCellMargin(
                new TopMargin { Width = "70", Type = TableWidthUnitValues.Dxa },
                new BottomMargin { Width = "70", Type = TableWidthUnitValues.Dxa },
                new LeftMargin { Width = "160", Type = TableWidthUnitValues.Dxa },
                new RightMargin { Width = "160", Type = TableWidthUnitValues.Dxa })));
        var first = true;
        foreach (var line in lines)
        {
            cell.AppendChild(Para(line, 18, spacingAfter: 40, spacingBefore: first ? 0 : 20));
            first = false;
        }
        if (first) cell.AppendChild(new Paragraph());
        return new Table(BorderlessProps(), Grid(10600), new TableRow(cell));
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
    private static TableCellVerticalAlignment VAlignTop() => new() { Val = TableVerticalAlignmentValues.Top };

    // Matches the reference: US Letter, RTL, with header/footer part references.
    private static SectionProperties SectionLetterRtl(string headerId, string footerId) => new(
        new HeaderReference { Type = HeaderFooterValues.Default, Id = headerId },
        new FooterReference { Type = HeaderFooterValues.Default, Id = footerId },
        new PageSize { Width = 12240U, Height = 15840U },
        new PageMargin { Top = 1160, Bottom = 400, Left = 820, Right = 820, Header = 0, Footer = 360, Gutter = 0 },
        new BiDi());

    private static (int w, int h) PngSize(byte[] png)
    {
        if (png.Length < 24) return (1, 1);
        int w = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        int h = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
        return (w == 0 ? 1 : w, h);
    }

    private static string Tint(string hex) => ("#" + hex) switch
    {
        "#C0392B" => "FBEAE8",
        "#DC2626" => "FDECEC",
        "#EA580C" => "FBEEE3",
        "#C67A1E" => "FBF1E2",
        "#2E9B5B" => "E8F4EC",
        "#0D9488" => "E3F1EF",
        "#1C8C8C" => "E4F1F1",
        _ => "EEF2F4",
    };

    // Light companion of a section marker color, for the thin bottom rule.
    private static string Lighten(string markerColor) => markerColor switch
    {
        Teal => "C2E9E4",
        "E08A3C" => "F6DDC2",
        Navy => "BAC5CE",
        "C0392B" => "EEC9C4",
        _ => "D8E0E5",
    };
}
