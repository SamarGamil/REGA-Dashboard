using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using AqarInsights.Api.Models.RentalControls.Reports;

namespace AqarInsights.Api.Services.RentalControls.Reports;

public static class CityReportPdfRenderer
{
    private const string Font = "Tajawal";
    private const string Navy = "#123A52";
    private const string Teal = "#1CA59A";
    private const string Ink = "#374151";
    private const string Muted = "#6B7280";
    private const string Line = "#E5E7EB";
    private const string BarBg = "#F1F4F6";
    private const string Zebra = "#F7FAFB";

    private static readonly byte[] Logo = LoadLogo();

    private static byte[] LoadLogo()
    {
        using var s = typeof(CityReportPdfRenderer).Assembly
            .GetManifestResourceStream("AqarInsights.Api.Resources.Images.rega-logo.png");
        if (s == null) return Array.Empty<byte>();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    public static byte[] Render(CityReportModel model) => Build(model).GeneratePdf();

    public static IDocument Build(CityReportModel model) => Compose(model).WithMetadata(new DocumentMetadata
    {
        Title = $"{model.HeaderTitle} – {model.PeriodLabel}",
        Subject = model.PeriodLine,
        Author = "الهيئة العامة للعقار",
    });

    private static Document Compose(CityReportModel model) => Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(24);
            page.DefaultTextStyle(t => t.FontFamily(Font).FontSize(8.5f).FontColor(Ink).DirectionFromRightToLeft());

            page.Content().ContentFromRightToLeft().Column(col =>
            {
                col.Spacing(7);

                Header(col, model);

                // الملخص التنفيذي
                Section(col, c =>
                {
                    SectionBar(c, "الملخص التنفيذي", "#E08A3C");
                    c.Item().Text(t =>
                    {
                        t.Span("حالة السوق:  ").FontSize(9.5f).Bold().FontColor(Navy);
                        t.Span(model.MarketState).FontSize(9.5f).Bold().FontColor(model.MarketStateColorHex);
                    });
                    KpiRow(c, model.KpiCards);
                    foreach (var b in model.ExecutiveBullets)
                        Bullet(c, b);
                });

                // لوحة المؤشرات
                Section(col, c =>
                {
                    SectionBar(c, "لوحة المؤشرات للضوابط الإيجارية", Navy);
                    foreach (var b in model.IndicatorsBullets)
                        Bullet(c, b);
                    IndicatorsTable(c, model.IndicatorRows);
                    c.Item().Height(8);
                });

                // حركة السوق والأسعار
                Section(col, c =>
                {
                    SectionBar(c, "حركة السوق والأسعار", Teal);
                    foreach (var b in model.MarketBullets)
                        Bullet(c, b);
                    if (!string.IsNullOrWhiteSpace(model.CityAveragesLine))
                        Bullet(c, model.CityAveragesLine);
                    ProductTable(c, model.ProductRows);
                    if (CityReportMarketBuilder.HasSmallBase(model.ProductRows))
                        c.Item().PaddingTop(2).Text(CityReportMarketBuilder.SmallBaseFootnote).FontSize(7).FontColor(Muted);
                    if (model.SupplyInvestment.Count > 0)
                        SupplyInvestmentStrip(c, model.SupplyInvestment);
                });

                // الرصد الإعلامي
                Section(col, c =>
                {
                    SectionBar(c, "الرصد الإعلامي", "#1C8C8C");
                    if (model.MediaAvailable)
                    {
                        c.Item().Text(t =>
                        {
                            t.Span("الانطباع العام: ").FontSize(8.5f).Bold().FontColor(Navy);
                            t.Span(model.MediaSentiment).FontSize(8.5f).Bold().FontColor(model.MediaSentimentColorHex);
                            t.Span("    (" + model.MediaMonthPrefix).FontSize(8).FontColor(Muted);
                            t.Span($"إيجابي {model.MediaPositivePct}%").FontSize(8).Bold().FontColor("#2E9B5B");
                            t.Span(" — ").FontSize(8).FontColor(Muted);
                            t.Span($"محايد {model.MediaNeutralPct}%").FontSize(8).Bold().FontColor(Muted);
                            t.Span(" — ").FontSize(8).FontColor(Muted);
                            t.Span($"سلبي {model.MediaNegativePct}%").FontSize(8).Bold().FontColor("#DC2626");
                            t.Span(")").FontSize(8).FontColor(Muted);
                        });
                        foreach (var b in model.MediaBullets)
                            Bullet(c, b);
                    }
                    else
                    {
                        foreach (var b in model.MediaBullets)
                            Bullet(c, b, Muted);
                    }
                });

                // أبرز الاستنتاجات والتوصية
                Section(col, c =>
                {
                    SectionBar(c, "أبرز الاستنتاجات والتوصية", "#C0392B");
                    foreach (var b in model.ConclusionBullets)
                        Bullet(c, b);
                    AxesTable(c, model.Axes);
                    c.Item().Text(t =>
                    {
                        t.Span("التقييم العام: ").FontSize(9.5f).Bold().FontColor(Navy);
                        t.Span(model.OverallAssessment + ".").FontSize(9.5f).Bold().FontColor(model.MarketStateColorHex);
                    });
                    c.Item().Text("التوصية").FontSize(9).Bold().FontColor(Navy);
                    foreach (var b in model.RecommendationBullets)
                        Bullet(c, b);
                });

                // ما يجب مراقبته
                Section(col, c =>
                {
                    SectionBar(c, model.WatchTitle, Navy);
                    foreach (var b in model.WatchList)
                        Bullet(c, b);
                });

                // Each chart is kept whole, so the first one fills the free space left on page two; the footer travels with
                // the last block so it is never left alone on an empty page.
                for (var k = 0; k < model.Charts.Count; k++)
                {
                    var chart = model.Charts[k];
                    var lastBlock = k == model.Charts.Count - 1;
                    Section(col, c =>
                    {
                        c.Item().Text(chart.Title).FontSize(8.5f).Bold().FontColor(Navy);
                        if (chart.Values.All(v => v is null))
                            c.Item().Text("لا تتوفر بيانات لهذه الفترة.").FontSize(7.5f).FontColor(Muted);
                        else
                            c.Item().AlignCenter().Width((float)ChartW).Column(plot =>
                            {
                                ChartDecisionLabel(plot, chart);
                                plot.Item().Height((float)ChartH).Svg(ChartSvg(chart));
                                ChartMonthLabels(plot, chart);
                            });
                        if (lastBlock) Footer(c, model);
                    }, spacing: 3);
                }

                if (model.Charts.Count == 0)
                    Footer(col, model);
            });
        });
    });

    private static void Header(ColumnDescriptor col, CityReportModel m)
    {
        col.Item().Row(row =>
        {
            row.RelativeItem().AlignMiddle().Column(c =>
            {
                c.Item().Text(m.HeaderTitle).FontSize(15).Bold().FontColor(Navy);
                c.Item().PaddingTop(2).Text(m.HeaderSubtitle).FontSize(9).FontColor(Teal);
            });
            if (Logo.Length > 0)
                row.ConstantItem(118).AlignLeft().AlignMiddle().Image(Logo);
        });
        col.Item().PaddingTop(3).LineHorizontal(1.2f).LineColor(Navy);
    }

    // Keeps a whole section on one page: if it doesn't fit, it moves entirely to the next page.
    private static void Section(ColumnDescriptor col, Action<ColumnDescriptor> content, float spacing = 7)
        => col.Item().ShowEntire().Column(c =>
        {
            c.Spacing(spacing);
            content(c);
        });

    private static void SectionBar(ColumnDescriptor col, string title, string marker)
    {
        col.Item().PaddingTop(3).Background(BarBg).Padding(4).Row(row =>
        {
            row.ConstantItem(4).Background(marker);
            row.ConstantItem(7);
            row.RelativeItem().AlignMiddle().Text(title).FontSize(10).Bold().FontColor(Navy);
        });
    }

    private static void Bullet(ColumnDescriptor col, string text, string? color = null)
    {
        col.Item().Row(row =>
        {
            row.ConstantItem(12).AlignTop().Text("●").FontColor(Teal).FontSize(7);
            row.RelativeItem().Text(text).FontSize(8.5f).FontColor(color ?? Ink).LineHeight(1.45f);
        });
    }

    private static void KpiRow(ColumnDescriptor col, List<CityReportKpiCardView> cards)
    {
        col.Item().Row(row =>
        {
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                row.RelativeItem().Border(1).BorderColor(Line).Background("#FFFFFF").MinHeight(46).Row(cr =>
                {
                    cr.ConstantItem(4).Background(card.ColorHex);
                    cr.RelativeItem().Padding(6).Column(c =>
                    {
                        TaggedLabel(c.Item(), card.Label, card.Frequency, card.Period, 7, Muted);
                        c.Item().PaddingTop(2).Text(PdfBidi.Ltr(card.ValueText)).FontSize(13).Bold().FontColor(card.ColorHex);
                        c.Item().PaddingTop(1).Text(card.Note).FontSize(7.2f);
                        if (!string.IsNullOrEmpty(card.Reference))
                            c.Item().PaddingTop(1).Text(card.Reference).FontSize(7).FontColor(Muted);
                    });
                });
                if (i < cards.Count - 1) row.ConstantItem(6);
            }
        });
    }

    // Indicator name and its period tag (coloured by the indicator's frequency).
    private static void TaggedLabel(IContainer container, string label, string frequency, string period, float size, string color, bool bold = false)
    {
        var lines = label.Split('\n', 2);
        container.Text(t =>
        {
            var name = t.Span(lines[0]).FontSize(size).FontColor(color);
            if (bold) name.Bold();
            if (period.Length > 0)
            {
                var (bg, fg) = ReportFrequency.Colors(frequency);
                t.Span(" ").FontSize(size);
                t.Span("\u00a0" + period + "\u00a0").FontSize(size - 0.8f).FontColor(fg).BackgroundColor(bg);
            }
            if (lines.Length > 1)
            {
                var month = t.Span("\n" + lines[1]).FontSize(size).FontColor(color);
                if (bold) month.Bold();
            }
        });
    }

    private static void IndicatorsTable(ColumnDescriptor col, List<CityReportIndicatorRowView> rows)
    {
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(cd =>
            {
                cd.RelativeColumn(2.8f);
                cd.RelativeColumn(1.1f);
                cd.RelativeColumn(1.6f);
                cd.RelativeColumn(1.6f);
            });

            void H(string t) => table.Cell().Background(Navy).PaddingVertical(5).PaddingHorizontal(6)
                .Text(t).FontColor("#FFFFFF").Bold().FontSize(8);
            H("المؤشر"); H("القيمة"); H("الحد المرجعي للمراقبة"); H("الحالة");

            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                var bg = i % 2 == 1 ? Zebra : "#FFFFFF";
                IContainer Cell() => table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Line)
                    .PaddingVertical(3).PaddingHorizontal(6);
                TaggedLabel(Cell().AlignRight(), r.Indicator, r.Frequency, r.Period, 8, Navy, bold: true);
                Cell().AlignRight().Text(PdfBidi.Ltr(r.Value)).FontSize(8);
                Cell().AlignRight().Text(PdfBidi.Ltr(r.Reference)).FontSize(8);
                table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Line).Padding(2)
                    .Background(r.StatusBgHex).PaddingVertical(1).PaddingHorizontal(6)
                    .AlignRight().AlignMiddle().Text(r.Status).FontSize(7.8f).Bold().FontColor(r.StatusColorHex);
            }
        });
    }

    private static void ProductTable(ColumnDescriptor col, List<CityReportProductRow> rows)
    {
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(cd =>
            {
                cd.RelativeColumn(1.0f);
                cd.RelativeColumn(1.5f);
                cd.RelativeColumn(1.5f);
                cd.RelativeColumn(1.5f);
            });

            void H(string t) => table.Cell().Background(Navy).PaddingVertical(5).PaddingHorizontal(6)
                .Text(t).FontColor("#FFFFFF").Bold().FontSize(8);
            H("المنتج"); H("الصفقات الإيجارية"); H("الصفقات البيعية"); H("الرقم القياسي");

            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                var bg = i % 2 == 1 ? Zebra : "#FFFFFF";
                IContainer Cell() => table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Line)
                    .PaddingVertical(3).PaddingHorizontal(6);
                Cell().AlignRight().Text(r.Product).FontSize(8).Bold().FontColor(Navy);
                Cell().AlignRight().Text(r.RentalDeals).FontSize(8);
                Cell().AlignRight().Text(r.SalesDeals).FontSize(8);
                Cell().AlignRight().Text(r.FisherIndex).FontSize(8);
            }
        });
    }

    // العرض والاستثمار strip: navy band + one row of label/value/trend cells.
    private static void SupplyInvestmentStrip(ColumnDescriptor col, List<CityReportSupplyStatView> stats)
    {
        col.Item().Background(Navy).PaddingVertical(4).PaddingHorizontal(6)
            .Text("العرض والاستثمار").FontColor("#FFFFFF").Bold().FontSize(8);
        col.Item().Row(row =>
        {
            for (int i = 0; i < stats.Count; i++)
            {
                var s = stats[i];
                row.RelativeItem().BorderBottom(0.5f).BorderColor(Line).PaddingVertical(4).PaddingHorizontal(6).Column(c =>
                {
                    TaggedLabel(c.Item(), s.Label, s.Frequency, s.Period, 7.2f, Muted);
                    c.Item().PaddingTop(1).Text(t =>
                    {
                        t.Span(PdfBidi.Ltr(s.Value)).FontSize(9.5f).Bold().FontColor(s.ColorHex);
                        t.Span("  " + s.Note).FontSize(7.2f).FontColor(s.ColorHex);
                    });
                    if (s.Reference.Length > 0)
                        c.Item().Text(s.Reference).FontSize(6.8f).FontColor(Muted);
                });
            }
        });
    }

    private static void AxesTable(ColumnDescriptor col, List<CityReportAxis> axes)
    {
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(cd =>
            {
                cd.RelativeColumn(1f);
                cd.RelativeColumn(1f);
            });

            void H(string t) => table.Cell().Background(Navy).PaddingVertical(5).PaddingHorizontal(6)
                .Text(t).FontColor("#FFFFFF").Bold().FontSize(8);
            H("المحور"); H("الحالة");

            for (int i = 0; i < axes.Count; i++)
            {
                var bg = i % 2 == 1 ? Zebra : "#FFFFFF";
                IContainer Cell() => table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Line)
                    .PaddingVertical(3).PaddingHorizontal(6);
                Cell().AlignRight().Text(axes[i].Axis).FontSize(8).Bold().FontColor(Navy);
                if (axes[i].ColorHex.Length == 0) Cell().AlignRight().Text(axes[i].Status).FontSize(8);
                else table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Line).PaddingVertical(1.6f).PaddingHorizontal(4)
                    .Background(axes[i].BgHex).PaddingVertical(1.6f).AlignCenter()
                    .Text(axes[i].Status).FontSize(8).Bold().FontColor(axes[i].ColorHex);
            }
        });
    }

    private const double ChartW = 536, ChartAxis = 26, ChartPad = 8, ChartH = 66;
    private const string ChartLine = "#2F6F62", ChartDecision = "#8A6A2F";

    // Right-to-left: the first month (January 2025) is at the right edge, beside the value axis.
    private static double ChartX(int i, int n) => ChartW - ChartAxis - (ChartW - ChartAxis - ChartPad) / n * (i + 0.5);

    // The decision label sits above the plot, centred on its dotted line.
    private static void ChartDecisionLabel(ColumnDescriptor col, CityReportChartView chart)
    {
        var n = chart.Categories.Count;
        if (chart.MarkerIndex < 0 || chart.MarkerIndex >= n) return;
        var x = ChartX(chart.MarkerIndex, n);
        var half = Math.Min(x, ChartW - x);
        col.Item().ContentFromLeftToRight().Row(r =>
        {
            if (x - half > 0) r.RelativeItem((float)(x - half));
            r.RelativeItem((float)(2 * half)).AlignCenter().Text(chart.MarkerLabel).FontSize(6.5f).FontColor(ChartDecision);
            if (ChartW - x - half > 0) r.RelativeItem((float)(ChartW - x - half));
        });
    }

    // One label per month under its point, and the year under each January (and the first month).
    private static void ChartMonthLabels(ColumnDescriptor col, CityReportChartView chart)
    {
        var n = chart.Categories.Count;
        var pw = (float)((ChartW - ChartAxis - ChartPad) / n);
        var parts = chart.Categories.Select(CityReportWordCharts.SplitLabel).ToList();
        string Month(int i) => parts[i].Name;
        string Year(int i) => parts[i].Year;

        // Laid out left to right, so the newest month comes first.
        void Strip(Action<IContainer, int> cell) => col.Item().ContentFromLeftToRight().Row(r =>
        {
            r.ConstantItem((float)ChartPad);
            for (var i = n - 1; i >= 0; i--) cell(r.ConstantItem(pw).AlignCenter(), i);
            r.ConstantItem((float)ChartAxis);
        });
        Strip((cell, i) => cell.Text(Month(i)).FontSize(5.6f).FontColor(Muted));
        Strip((cell, i) =>
        {
            if (i == 0 || CityReportWordCharts.StartsYear(Month(i)))
                cell.Text(Year(i)).FontSize(6.5f).Bold().FontColor(Ink);
        });
    }

    // A plain SVG line chart in the platform's style: 5% grid with tick labels on the right, light area, value labels, the peak
    // emphasised, a red last point and the dotted decision-month line; the month labels are PDF text (ChartMonthLabels).
    private static string ChartSvg(CityReportChartView chart)
    {
        const double w = ChartW, h = ChartH, axis = ChartAxis, pad = ChartPad, top = 12, bottom = 4;
        var n = chart.Values.Count;
        var known = chart.Values.Select((v, i) => (Index: i, Value: v)).Where(p => p.Value is not null).Select(p => (p.Index, Value: p.Value!.Value)).ToList();
        var (min, max) = CityReportWordCharts.AxisRange(known.Select(p => p.Value));
        double X(int i) => ChartX(i, n);
        double Y(double v) => h - bottom - (h - top - bottom) * (v - min) / (max - min);
        string N(double v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        var sb = new System.Text.StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {w} {h}\">");
        for (var g = min; g <= max + 0.001; g += 5)
        {
            var y = Y(g);
            sb.Append($"<line x1=\"{N(pad)}\" y1=\"{N(y)}\" x2=\"{N(w - axis)}\" y2=\"{N(y)}\" stroke=\"{(g == 0 ? "#6B7280" : "#E5E7EB")}\" stroke-width=\"{(g == 0 ? "0.8" : "0.6")}\"/>");
            sb.Append($"<text x=\"{N(w - axis + 3)}\" y=\"{N(y + 2)}\" font-family=\"sans-serif\" font-size=\"6\" text-anchor=\"start\" fill=\"#6B7280\">{N(g)}%</text>");
        }

        // One run: a month the data lacks is bridged by the line between its neighbours.
        var runs = new List<List<(int Index, double Value)>> { known };
        foreach (var run in runs)
        {
            var pts = string.Join(" ", run.Select(p => $"{N(X(p.Index))},{N(Y(p.Value))}"));
            var zero = N(Y(0));
            if (run.Count > 1)
                sb.Append($"<polygon points=\"{N(X(run[0].Index))},{zero} {pts} {N(X(run[^1].Index))},{zero}\" fill=\"#DCE8EA\" fill-opacity=\"0.8\"/>");
            sb.Append($"<polyline points=\"{pts}\" fill=\"none\" stroke=\"{ChartLine}\" stroke-width=\"1.4\" stroke-linejoin=\"round\"/>");
        }

        if (chart.MarkerIndex >= 0 && chart.MarkerIndex < n)
        {
            var x = X(chart.MarkerIndex);
            sb.Append($"<line x1=\"{N(x)}\" y1=\"0\" x2=\"{N(x)}\" y2=\"{N(h - bottom)}\" stroke=\"{ChartDecision}\" stroke-width=\"0.8\" stroke-dasharray=\"1.5,2\"/>");
        }
        var peak = known.OrderByDescending(p => p.Value).First().Index;
        foreach (var p in known)
        {
            var last = p.Index == known[^1].Index;
            var emphasised = last || p.Index == peak;
            var fill = last ? "#B91C1C" : p.Index == peak ? "#1F5A4D" : ChartLine;
            sb.Append($"<circle cx=\"{N(X(p.Index))}\" cy=\"{N(Y(p.Value))}\" r=\"{(emphasised ? "2.8" : "1.5")}\" fill=\"{fill}\"/>");
            sb.Append($"<text x=\"{N(X(p.Index))}\" y=\"{N(Y(p.Value) - (emphasised ? 5 : 3.5))}\" font-family=\"sans-serif\" font-size=\"{(emphasised ? "6.8" : "5.2")}\" font-weight=\"{(emphasised ? "bold" : "normal")}\" text-anchor=\"middle\" fill=\"{(last ? "#B91C1C" : emphasised ? "#111827" : "#374151")}\">{p.Value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}</text>");
        }
        sb.Append("</svg>");
        return sb.ToString();
    }

    private static void Footer(ColumnDescriptor col, CityReportModel m)
    {
        col.Item().PaddingTop(4).LineHorizontal(0.8f).LineColor(Line);
        col.Item().PaddingTop(3).Row(row =>
        {
            row.RelativeItem().Text($"الهيئة العامة للعقار — {m.PeriodLabel}").FontSize(7.5f).FontColor(Muted);
            row.RelativeItem().AlignLeft().Text($"تاريخ الإنشاء: {m.GeneratedOnLabel}").FontSize(7.5f).FontColor(Muted);
        });
    }
}
