using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using AqarInsights.Api.Models.RentalControls.Reports;

namespace AqarInsights.Api.Services.RentalControls.Reports;

public static class NationalReportPdfRenderer
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
        using var s = typeof(NationalReportPdfRenderer).Assembly
            .GetManifestResourceStream("AqarInsights.Api.Resources.Images.rega-logo.png");
        if (s == null) return Array.Empty<byte>();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    public static byte[] Render(NationalReportModel model) => Build(model).GeneratePdf();

    public static IDocument Build(NationalReportModel model) => Compose(model).WithMetadata(new DocumentMetadata
    {
        Title = $"{model.HeaderTitle} – {model.PeriodLabel}",
        Subject = model.PeriodLine,
        Author = "الهيئة العامة للعقار",
    });

    private static Document Compose(NationalReportModel model) => Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(16);
            page.DefaultTextStyle(t => t.FontFamily(Font).FontSize(8).FontColor(Ink).DirectionFromRightToLeft());

            // One page only: content that would overflow is scaled down to fit.
            page.Content().ContentFromRightToLeft().Border(1).BorderColor(Line).Padding(10).ScaleToFit().Column(col =>
            {
                col.Spacing(4);
                Header(col, model);
                SectionBar(col, "الملخص التنفيذي", "#E08A3C");
                foreach (var line in model.SummaryLines)
                    Bullet(col, line);
                KpiRow(col, model.KpiCards);
                SectionBar(col, model.Title, Navy);
                if (!string.IsNullOrWhiteSpace(model.SortNote))
                    col.Item().Text(model.SortNote).FontSize(7.2f).FontColor(Muted);
                CityTable(col, model);
                Legend(col);
                Intervention(col, model);
                if (model.Impact.Count > 0)
                    col.Item().ShowEntire().Column(impact =>
                    {
                        SectionBar(impact, "أثر الضوابط الإيجارية المفعلة", Teal);
                        foreach (var city in model.Impact)
                        {
                            impact.Item().PaddingTop(2).Text(city.CityName).FontSize(8.5f).Bold().FontColor(Navy);
                            foreach (var point in city.Points) Bullet(impact, point);
                        }
                    });
                Footer(col, model);
            });
        });
    });

    private static void Header(ColumnDescriptor col, NationalReportModel m)
    {
        col.Item().Row(row =>
        {
            row.RelativeItem().AlignMiddle().Column(c =>
            {
                c.Item().Text(m.HeaderTitle).FontSize(15).Bold().FontColor(Navy);
                c.Item().PaddingTop(2).Text(m.AssessmentLine).FontSize(8.5f).FontColor(Muted);
            });
            if (Logo.Length > 0)
                row.ConstantItem(118).AlignLeft().AlignMiddle().Image(Logo);
        });
        col.Item().PaddingTop(3).LineHorizontal(1.2f).LineColor(Navy);
    }

    private static void SectionBar(ColumnDescriptor col, string title, string marker)
    {
        col.Item().PaddingTop(6).Background(BarBg).Padding(4).Row(row =>
        {
            row.ConstantItem(4).Background(marker);
            row.ConstantItem(7);
            row.RelativeItem().AlignMiddle().Text(title).FontSize(9.5f).Bold().FontColor(Navy);
        });
    }

    private static void Bullet(ColumnDescriptor col, string text)
    {
        col.Item().Row(row =>
        {
            row.ConstantItem(11).AlignTop().Text("●").FontColor(Teal).FontSize(6.5f);
            row.RelativeItem().Text(text).FontSize(8f).LineHeight(1.35f);
        });
    }

    // Legend for the indicator cell colors.
    private static void Legend(ColumnDescriptor col)
    {
        col.Item().Row(row =>
        {
            row.AutoItem().Text("دلالة الألوان:  ").FontSize(7.2f).Bold().FontColor(Muted);
            void Chip(string label, string color)
            {
                row.AutoItem().Background(Tint(color)).PaddingVertical(1).PaddingHorizontal(5)
                    .Text(label).FontSize(7).FontColor(color);
                row.ConstantItem(5);
            }
            Chip("ضمن الحد المرجعي للمراقبة", "#2E9B5B");
            Chip("قرب الحد المرجعي للمراقبة", "#C67A1E");
            Chip("يتجاوز الحد المرجعي للمراقبة", "#DC2626");
            Chip("غير متاح", "#6B7280");
        });
    }

    private static void KpiRow(ColumnDescriptor col, List<ReportKpiCard> cards)
    {
        col.Item().Row(row =>
        {
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                row.RelativeItem().Border(1).BorderColor(Line).Background("#FFFFFF").MinHeight(38).Row(cr =>
                {
                    cr.ConstantItem(4).Background(card.ColorHex);
                    cr.RelativeItem().Padding(4).Column(c =>
                    {
                        c.Item().Text(t =>
                        {
                            t.Span(card.Label).FontSize(7.2f).FontColor(Muted);
                            if (card.Period.Length > 0)
                            {
                                var (bg, fg) = ReportFrequency.Colors(card.Frequency);
                                t.Span(" ").FontSize(7.2f);
                                t.Span("\u00a0" + card.Period + "\u00a0").FontSize(6.5f).FontColor(fg).BackgroundColor(bg);
                            }
                        });
                        c.Item().PaddingTop(1).Text(t =>
                        {
                            t.Span(card.ValueText == "—" ? "" : card.IsUp ? "▲ " : "▼ ").FontSize(8).FontColor(card.ColorHex);
                            t.Span(PdfBidi.Ltr(card.ValueText)).FontSize(13).Bold().FontColor(card.ColorHex);
                        });
                        c.Item().Text(card.CityName).FontSize(7.5f);
                    });
                });
                if (i < cards.Count - 1) row.ConstantItem(7);
            }
        });
    }

    private static void CityTable(ColumnDescriptor col, NationalReportModel model)
    {
        var rows = model.Rows;
        string P(string key) => model.IndicatorPeriods.GetValueOrDefault(key, "");
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(cd =>
            {
                cd.ConstantColumn(22);
                cd.RelativeColumn(1.9f);
                cd.RelativeColumn(1.9f);
                cd.RelativeColumn(1.3f);
                cd.RelativeColumn(1.3f);
                cd.RelativeColumn(1.5f);
                cd.RelativeColumn(1.5f);
            });

            void H(string t, string tag = "") => table.Cell().Background(Navy).PaddingVertical(5).PaddingHorizontal(6)
                .Text(x =>
                {
                    x.Span(t).FontColor("#FFFFFF").Bold().FontSize(8);
                    if (tag.Length == 0) return;
                    var (bg, fg) = ReportFrequency.OnNavy;
                    // Always below the name: narrow columns would wrap it there anyway.
                    x.Span("\n").FontSize(8);
                    x.Span("\u00a0" + tag + "\u00a0").FontSize(7).FontColor(fg).BackgroundColor(bg);
                });
            H("#"); H("المدينة"); H("المسار");
            H("الزيادة الإيجارية", P("rental-increase")); H("نسبة الإيجار إلى الدخل", P("rent-to-income"));
            H("العبء السكني لذوي الدخل المحدود", P("housing-burden")); H("الإيجار الفعلي للمسكن CPI", P("actual-rent"));

            // Trend columns: the figure in a slot as wide as the column's longest figure (right-aligned), the arrow or dash
            // in a fixed slot beside it, so every symbol of a column sits in one line whatever the figure's width.
            float SlotWidth(IEnumerable<string> texts) => texts.Select(t => t.Length).DefaultIfEmpty(1).Max() * 4.6f + 2;
            var burdenSlot = SlotWidth(rows.Select(r => r.BurdenText));
            var rentSlot = SlotWidth(rows.Select(r => r.ActualRentText));

            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                var bg = i % 2 == 1 ? Zebra : "#FFFFFF";
                IContainer Cell() => table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Line)
                    .PaddingVertical(2.2f).PaddingHorizontal(6);
                IContainer ValueCell(string colorHex) => table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Line)
                    .Background(Tint(colorHex)).PaddingVertical(2.8f).PaddingHorizontal(6).AlignRight();
                void V(string text, string colorHex) => ValueCell(colorHex)
                    .Text(PdfBidi.Ltr(text)).FontSize(8).Bold().FontColor(colorHex);
                void VT(string text, string colorHex, string trend, float slot) => ValueCell(colorHex).Row(row =>
                {
                    // Right to left: the figure's slot first (rightmost), then the symbol's.
                    row.ConstantItem(slot).AlignRight().Text(PdfBidi.Ltr(text)).FontSize(8).Bold().FontColor(colorHex);
                    row.ConstantItem(10).AlignCenter().Text(trend).FontSize(8).Bold()
                        .FontColor(trend == "↑" ? "#DC2626" : trend == "↓" ? "#2E9B5B" : Muted);
                });
                Cell().AlignRight().Text(r.Rank.ToString()).FontSize(8).FontColor(Muted);
                Cell().AlignRight().Text(r.CityName).FontSize(8);
                // The badge fills the column, so every row's box has the same width.
                table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Line).PaddingVertical(1.6f).PaddingHorizontal(4)
                    .Background(Tint(r.TrackColorHex)).PaddingVertical(1.6f).AlignCenter()
                    .Text(r.TrackLabel).FontSize(7.5f).Bold().FontColor(r.TrackColorHex);
                V(r.RentalIncreaseText, r.RentalIncreaseColorHex);
                V(r.RentToIncomeText, r.RentToIncomeColorHex);
                VT(r.BurdenText, r.BurdenColorHex, r.BurdenTrend, burdenSlot);
                VT(r.ActualRentText, r.RentColorHex, r.ActualRentTrend, rentSlot);
            }
        });
    }

    private static void Intervention(ColumnDescriptor col, NationalReportModel m)
    {
        col.Item().Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                c.Spacing(5);
                SectionBar(c, "أولويات التدخل بالمدن", "#C0392B");
                foreach (var g in m.InterventionGroups)
                {
                    c.Item().Row(gr =>
                    {
                        gr.ConstantItem(92).AlignTop().Background(Tint(g.ColorHex))
                            .PaddingVertical(2).AlignCenter()
                            .Text(g.Label).FontSize(7.5f).Bold().FontColor(g.ColorHex);
                        gr.ConstantItem(8);
                        gr.RelativeItem().AlignMiddle().Text(string.Join("، ", g.Cities)).FontSize(8).LineHeight(1.4f);
                    });
                }
            });
            row.ConstantItem(16);
            row.RelativeItem().Column(c =>
            {
                SectionBar(c, m.MonitoringTitle, Teal);
                foreach (var b in m.MonitoringBullets)
                {
                    c.Item().PaddingTop(3).Row(br =>
                    {
                        br.ConstantItem(11).AlignTop().Text("•").FontColor(Teal).FontSize(11);
                        br.RelativeItem().Text(b).FontSize(8).LineHeight(1.4f);
                    });
                }
            });
        });
    }

    private static void Footer(ColumnDescriptor col, NationalReportModel m)
    {
        col.Item().PaddingTop(4).LineHorizontal(0.8f).LineColor(Line);
        col.Item().PaddingTop(3).Row(row =>
        {
            row.RelativeItem().Text($"الهيئة العامة للعقار — {m.PeriodLabel}").FontSize(7.5f).FontColor(Muted);
            row.RelativeItem().AlignLeft().Text("1 صفحة").FontSize(7.5f).FontColor(Muted);
        });
    }

    private static string Tint(string hex) => hex switch
    {
        "#C0392B" => "#FBEAE8",
        "#DC2626" => "#FDECEC",
        "#EA580C" => "#FBEEE3",
        "#C67A1E" => "#FBF1E2",
        "#2E9B5B" => "#E8F4EC",
        "#0D9488" => "#E3F1EF",
        "#1C8C8C" => "#E4F1F1",
        _ => "#EEF2F4",
    };
}
