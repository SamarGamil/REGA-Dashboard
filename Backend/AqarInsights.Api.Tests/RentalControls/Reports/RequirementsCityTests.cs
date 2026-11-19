using System.Globalization;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AqarInsights.Api.Tests.RentalControls.Reports;

// VERIFICATION of "متطلبات تعديل منصة الضوابط الإيجارية" (REGA, 04/11/2026), sections 4 and 5 (city reports).
// Each requirement applies to every city and period that carries the element, so every test loops over all of them.
public class RequirementsCityTests
{
    public static IEnumerable<object[]> Periods() => Rv.AllPeriods();

    // ---- helpers -----------------------------------------------------------------------------------------------------

    private static List<string> Card(ReportDoc doc, string startsWith)
    {
        var cell = Rv.Cells(doc.TableElements[0]).FirstOrDefault(c => ReportDoc.Norm(c.InnerText).StartsWith(startsWith, StringComparison.Ordinal));
        return cell?.Descendants<Paragraph>().Select(p => ReportDoc.Norm(p.InnerText)).Where(t => t.Length > 0).ToList() ?? new();
    }

    private static List<string> Section(ReportDoc doc, string heading, params string[] stopAt)
    {
        var all = doc.Paragraphs.Select(ReportDoc.Norm).ToList();
        var start = all.FindIndex(p => p.StartsWith(heading, StringComparison.Ordinal));
        if (start < 0) return new();
        var rest = all.Skip(start + 1).ToList();
        var stop = rest.FindIndex(p => stopAt.Any(s => p.StartsWith(s, StringComparison.Ordinal)));
        return stop < 0 ? rest : rest.Take(stop).ToList();
    }

    private static string StatusOf(Rv.CityPortal portal, string cityId, string panelId)
        => portal.Panels.First(p => p.Id == panelId).Cities.FirstOrDefault(c => c.CityId == cityId)?.Status ?? "—";

    private static string Movement(Rv.CityPortal portal, string indicatorPrefix)
        => portal.City.MarketMovementTable.FirstOrDefault(m => m.Indicator.StartsWith(indicatorPrefix, StringComparison.Ordinal))?.Assessment ?? "—";

    // ---- page 1 ------------------------------------------------------------------------------------------------------

    // 4.1  Market state is the platform's state for the city and period.
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "4.1")]
    public void R4_1_market_state_is_the_platforms(string period)
    {
        var violations = new List<string>();
        foreach (var cityId in Rv.CityIds(period))
        {
            var state = Rv.City(period, cityId).Paragraphs.Select(ReportDoc.Norm).FirstOrDefault(p => p.StartsWith("حالة السوق:"));
            var expected = Rv.Portal(period, cityId).Overview.OperationalStatus;
            if (state != $"حالة السوق: {expected}") violations.Add($"{period}/{cityId}: report says «{state}», the platform says «{expected}»");
        }
        Rv.NoViolations(violations, "market state");
    }

    // 4.2  Yield card: reference line, the platform's status, no arrow.
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "4.2")]
    public void R4_2_yield_card_has_the_monitoring_reference_the_platform_status_and_no_arrow(string period)
    {
        var violations = new List<string>();
        foreach (var cityId in Rv.CityIds(period))
        {
            var card = Card(Rv.City(period, cityId), "العائد الإيجاري");
            var text = string.Join(" | ", card);
            // The platform's status against the reference printed on the card (indicators page, «دون الحد» for Dammam),
            // named in full — the same status the supply strip and the axis show (client review, Riyadh August 2026,
            // notes 10–11; it replaced the city page's «محايد»).
            var status = AqarInsights.Api.Services.RentalControls.Reports.ReportThresholds.StatusText(StatusOf(Rv.Portal(period, cityId), cityId, "investment-return"));
            if (!card.Contains("الحد المرجعي للمراقبة: أقل من 6%")) violations.Add($"{period}/{cityId}: reference line missing: {text}");
            if (!text.Contains('—') && !card.Contains(status)) violations.Add($"{period}/{cityId}: status should be «{status}»: {text}");
            if (Regex.IsMatch(text, "[▲▼↑↓]")) violations.Add($"{period}/{cityId}: arrow on the yield card: {text}");
            if (text.Contains("مستقر فوق الحد الأدنى")) violations.Add($"{period}/{cityId}: invented status: {text}");
        }
        Rv.NoViolations(violations, "yield card");
    }

    // 4.3  Actual-rent card status is the platform's assessment (never an invented one).
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "4.3")]
    public void R4_3_actual_rent_card_status_is_the_platforms_assessment(string period)
    {
        var violations = new List<string>();
        foreach (var cityId in Rv.CityIds(period))
        {
            var card = Card(Rv.City(period, cityId), "الإيجار الفعلي للمسكن");
            var assessment = Movement(Rv.Portal(period, cityId), "الإيجار الفعلي للمسكن");
            if (assessment != "—" && !card.Contains(assessment)) violations.Add($"{period}/{cityId}: status should be «{assessment}»: {string.Join(" | ", card)}");
            if (assessment != "معتدل" && card.Contains("معتدل")) violations.Add($"{period}/{cityId}: «معتدل» is not a platform status here: {string.Join(" | ", card)}");
        }
        Rv.NoViolations(violations, "actual-rent card");
    }

    // 4.4  CAGR card names its reference for monitoring (label only; the value stays the platform's).
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "4.4")]
    public void R4_4_growth_card_reference_is_named_for_monitoring(string period)
    {
        var violations = new List<string>();
        foreach (var cityId in Rv.CityIds(period))
        {
            var card = Card(Rv.City(period, cityId), "معدل النمو");
            if (!card.Any(p => p.StartsWith("الحد المرجعي للمراقبة: "))) violations.Add($"{period}/{cityId}: {string.Join(" | ", card)}");
        }
        Rv.NoViolations(violations, "growth card reference");
    }

    // 4.5  Executive-summary points: no «الوحيدة», and the Madinah demand point as the platform's figures.
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "4.5")]
    public void R4_5_summary_points_are_precise(string period)
    {
        var violations = Rv.Scan(Rv.CityDocs(period), ("«الوحيدة»", new Regex("الوحيدة")), ("baseline claim", new Regex("(?:التوازن المستهدف الذي تقاس عليه|كمرجع لبقية المدن)")));
        Rv.NoViolations(violations, "summary wording");

        var portal = Rv.Portal(period, "madinah");
        var deals = Rv.Num(portal.City.MarketMovementTable.FirstOrDefault(m => m.Indicator.StartsWith("عدد الصفقات الإيجارية"))?.Change);
        var summary = Section(Rv.City(period, "madinah"), "الملخص التنفيذي", "لوحة المؤشرات");
        var bullet = summary.FirstOrDefault(p => p.Contains("الصفقات الإيجارية") && p.Contains("ألف صفقة"));
        Assert.True(bullet is not null, "Madinah: no point states the rental deals as «ارتفعت الصفقات الإيجارية في {الفترة} إلى نحو N ألف صفقة بنمو سنوي P%، وتصدرتها …»");
        Assert.Matches(@"ارتفعت الصفقات الإيجارية في .+ إلى نحو \d+ ألف صفقة بنمو سنوي \d+%، وتصدرتها", bullet!);
        if (deals is not null) Assert.Contains($"{Math.Abs(Math.Round(deals.Value))}%", bullet!);
    }

    // 4.6  Indicator-panel points: no wrong causal claim, the growth path described as the platform does, no «all within limits».
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "4.6")]
    public void R4_6_indicator_points_are_accurate(string period)
    {
        var violations = Rv.Scan(Rv.CityDocs(period), ("wrong cause", new Regex("ما يضع .* في مسار استباقي")),
            ("unsupported claim", new Regex("جميع المؤشرات ضمن حدودها")));
        foreach (var cityId in Rv.CityIds(period))
        {
            var classification = ReportDoc.Norm(Rv.Portal(period, cityId).Overview.Classification);
            var first = Section(Rv.City(period, cityId), "لوحة المؤشرات للضوابط الإيجارية", "المؤشر").FirstOrDefault(p => p.Contains("معدل النمو"));
            if (first is null || !first.Contains(classification))
                violations.Add($"{period}/{cityId}: the growth point should end with the platform's path «{classification}»: {first}");
        }
        Rv.NoViolations(violations, "indicator points");
    }

    // 4.7  Indicator table: symbols in the reference column, the platform's inflation, space before the next section.
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "4.7")]
    public void R4_7_indicator_table_is_consistent(string period)
    {
        var symbolic = new Regex(@"^(?:[<>≥≤=]\s*)?\d+(?:\.\d+)?%(?:\s*[–-]\s*\d+(?:\.\d+)?%)?$|^—$");
        var violations = new List<string>();
        foreach (var cityId in Rv.CityIds(period))
        {
            var doc = Rv.City(period, cityId);
            var table = doc.Tables.First(t => t[0].Count == 4 && t[0][0] == "المؤشر");
            foreach (var row in table.Skip(1))
            {
                var reference = ReportDoc.Norm(row[2]).Replace("(", "").Replace(")", "");
                if (!symbolic.IsMatch(reference)) violations.Add($"{period}/{cityId}: reference «{reference}» for «{ReportDoc.Norm(row[0])}» is not symbolic");
            }
            var inflation = table.FirstOrDefault(r => ReportDoc.Norm(r[0]).StartsWith("التضخم العام"));
            var platform = Rv.Num(Rv.LastFilled(Rv.Portal(period, cityId).City, "التضخم العام")?.Value)
                ?? Rv.Num(Rv.Portal(period, cityId).City.MarketMovementTable.FirstOrDefault(m => m.Indicator == "التضخم العام (CPI)")?.Change);
            if (inflation is not null && platform is not null && Math.Abs((Rv.Num(inflation[1]) ?? double.NaN) - platform.Value) > 0.051)
                violations.Add($"{period}/{cityId}: inflation shows {inflation[1]}, the platform shows {platform}");

            var body = doc.BodyElements.ToList();
            var tableAt = body.FindIndex(e => e is Table t && t.InnerText.Contains("الحد المرجعي للمراقبة"));
            var headingAt = body.FindIndex(e => e is Paragraph p && ReportDoc.Norm(p.InnerText) == "حركة السوق والأسعار");
            if (tableAt < 0 || headingAt < tableAt) { violations.Add($"{period}/{cityId}: table or heading not found"); continue; }
            var gap = body.Skip(tableAt + 1).Take(headingAt - tableAt).OfType<Paragraph>().Sum(p =>
            {
                var s = p.ParagraphProperties?.SpacingBetweenLines;
                int N(string? v) => int.TryParse(v, out var n) ? n : 0;
                return N(s?.Before?.Value) + N(s?.After?.Value) + (s?.Line?.Value is { } l && p.InnerText.Length == 0 ? N(l) : 0);
            });
            if (gap < 200) violations.Add($"{period}/{cityId}: only {gap} twips between the indicator table and «حركة السوق والأسعار»");
        }
        Rv.NoViolations(violations, "indicator table");
    }

    // 4.8  Market section: correct rental-market rank, no nested sentences, averages with the market points.
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "4.8")]
    public void R4_8_market_section_matches_the_platform(string period)
    {
        var ordinals = new Dictionary<string, int> { ["ثاني"] = 2, ["ثالث"] = 3, ["رابع"] = 4, ["خامس"] = 5, ["سادس"] = 6, ["سابع"] = 7, ["ثامن"] = 8, ["تاسع"] = 9, ["عاشر"] = 10 };
        var ranked = Rv.Period(period).Aggregation.GetMarketAnalysis().Cities.OrderByDescending(c => c.RentalTransactionCount ?? -1).Select(c => c.Id).ToList();
        var violations = Rv.Scan(Rv.CityDocs(period), ("nested sentence", new Regex("يبرز الدور بارتفاعه إيجارا وبيعا")),
            ("price pressure without «للرقم القياسي»", new Regex("الضغط السعري يتركز")),
            ("«رقمها القياسي» inside the figure", new Regex(@"\(\+?\d+%\s*رقمها القياسي\)")));
        foreach (var cityId in Rv.CityIds(period))
        {
            var doc = Rv.City(period, cityId);
            foreach (var p in doc.Paragraphs.Select(ReportDoc.Norm))
                if (Regex.Match(p, "(ثاني|ثالث|رابع|خامس|سادس|سابع|ثامن|تاسع|عاشر) أكبر مدينة من حيث الصفقات الإيجارية") is { Success: true } m
                    && ordinals[m.Groups[1].Value] != ranked.IndexOf(cityId) + 1)
                    violations.Add($"{period}/{cityId}: says «{m.Value}» but ranks {ranked.IndexOf(cityId) + 1} by rental deals on the platform");
            var averages = doc.ParagraphIndex("متوسط قيمة الصفقة الإيجارية");
            var productsHeader = doc.Paragraphs.ToList().FindIndex(p => p == "المنتج");
            var supply = doc.Paragraphs.ToList().FindIndex(p => ReportDoc.Norm(p) == "العرض والاستثمار");
            if (averages < 0 || averages > productsHeader || averages > supply)
                violations.Add($"{period}/{cityId}: the averages point must be the last market point, before the products table");
        }
        Rv.NoViolations(violations, "market section");
    }

    // 4.9  «العرض والاستثمار» cards: the platform's period and statuses, no tags.
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "4.9")]
    public void R4_9_supply_and_investment_cards_show_period_and_platform_statuses(string period)
    {
        var violations = new List<string>();
        foreach (var cityId in Rv.CityIds(period))
        {
            var portal = Rv.Portal(period, cityId);
            var doc = Rv.City(period, cityId);
            var table = doc.TableElements.FirstOrDefault(t => ReportDoc.Norm(t.Elements<TableRow>().First().InnerText) == "العرض والاستثمار");
            if (table is null) { violations.Add($"{period}/{cityId}: section table missing"); continue; }
            var cards = table.Elements<TableRow>().Last().Elements<TableCell>().Select(c => ReportDoc.Norm(c.InnerText)).ToList();
            (string Starts, string Word, string Status)[] expected =
            {
                ("الشواغر الإيجارية", Rv.PeriodWord(portal.Panels.First(p => p.Id == "vacancy").PeriodLabel), StatusOf(portal, cityId, "vacancy")),
                ("الإيجار الفعلي للمسكن", Rv.PeriodWord(Rv.LastFilled(portal.City, "الإيجار الفعلي للمسكن")?.Month ?? ""), Movement(portal, "الإيجار الفعلي للمسكن")),
                ("العائد الإيجاري", Rv.PeriodWord(portal.Panels.First(p => p.Id == "investment-return").PeriodLabel), StatusOf(portal, cityId, "investment-return")),
            };
            foreach (var (starts, word, status) in expected)
            {
                var card = cards.FirstOrDefault(c => c.StartsWith(starts));
                if (card is null) { violations.Add($"{period}/{cityId}: card «{starts}» missing"); continue; }
                if (word.Length > 0 && !card.Contains(word)) violations.Add($"{period}/{cityId}: card «{starts}» should show «{word}»: {card}");
                if (status != "—" && !card.Contains(status)) violations.Add($"{period}/{cityId}: card «{starts}» should show status «{status}»: {card}");
                // 2.2 applied to the cards: the platform's reference, labelled «الحد المرجعي للمراقبة».
                var panelId = starts == "الشواغر الإيجارية" ? "vacancy" : starts == "العائد الإيجاري" ? "investment-return" : null;
                if (panelId is not null && !string.IsNullOrWhiteSpace(portal.Panels.First(p => p.Id == panelId).ThresholdSummary) && !card.Contains("الحد المرجعي للمراقبة"))
                    violations.Add($"{period}/{cityId}: card «{starts}» lacks «الحد المرجعي للمراقبة»: {card}");
            }
        }
        Rv.NoViolations(violations, "supply and investment cards");
    }

    // ---- page 2 ------------------------------------------------------------------------------------------------------

    // 5.1  Media impression line: each impression in its platform colour, apart from the others.
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "5.1")]
    public void R5_1_media_impressions_are_coloured_individually(string period)
    {
        var violations = new List<string>();
        foreach (var cityId in Rv.CityIds(period))
        {
            if (!Rv.Portal(period, cityId).City.MediaMonitoring.Available) continue;
            var runs = Rv.City(period, cityId).Runs;
            foreach (var (word, test, colour) in new (string, Func<string?, bool>, string)[] { ("إيجابي", Rv.IsGreen, "green"), ("محايد", Rv.IsGrey, "grey"), ("سلبي", Rv.IsRed, "red") })
            {
                var run = runs.FirstOrDefault(r => Regex.IsMatch(r.Text, $@"{word}\s*\d+%") && !Regex.IsMatch(r.Text, @"(إيجابي|محايد|سلبي)\s*\d+%.*(إيجابي|محايد|سلبي)\s*\d+%"));
                if (run is null) violations.Add($"{period}/{cityId}: «{word} N%» is not a run of its own");
                else if (!test(run.Color)) violations.Add($"{period}/{cityId}: «{run.Text.Trim()}» is {run.Color ?? "uncoloured"}, expected {colour}");
            }
        }
        Rv.NoViolations(violations, "media impression line");
    }

    // 5.2  Conclusion points: no unclear or non-platform wording.
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "5.2")]
    public void R5_2_conclusion_points_are_clear_and_platform_worded(string period)
    {
        var violations = Rv.Scan(Rv.CityDocs(period),
            ("unclear baseline", new Regex("خط الأساس الوطني")), ("pressure file", new Regex("ملف ضاغط")),
            ("nested sentence", new Regex("تفاوت داخلي، الشريحة الدنيا مضغوطة")), ("non-platform term", new Regex("توصيفية")));
        Rv.NoViolations(violations, "conclusion points");
    }

    // 5.3  Axes table: full affordability name, coloured status badges, investment status as the platform's.
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "5.3")]
    public void R5_3_axes_table_has_badges_and_correct_statuses(string period)
    {
        var violations = new List<string>();
        foreach (var cityId in Rv.CityIds(period))
        {
            var doc = Rv.City(period, cityId);
            var at = doc.Tables.ToList().FindIndex(t => t[0].Count == 2 && t[0][0] == "المحور");
            if (at < 0) { violations.Add($"{period}/{cityId}: axes table missing"); continue; }
            var rows = doc.TableElements[at].Elements<TableRow>().Skip(1).ToList();
            if (!rows.Any(r => ReportDoc.Norm(r.Elements<TableCell>().First().InnerText) == "القدرة على تحمل السكن لذوي الدخل المحدود"))
                violations.Add($"{period}/{cityId}: «القدرة على تحمل السكن لذوي الدخل المحدود» missing");
            // Badge = the whole status cell tinted and centred, so every row's box has the same size (as in the national table).
            foreach (var row in rows)
            {
                var status = row.Elements<TableCell>().Last();
                if (status.TableCellProperties?.Shading?.Fill?.Value is not { Length: > 0 } || !status.Descendants<Justification>().Any(j => j.Val?.Value == JustificationValues.Center))
                    violations.Add($"{period}/{cityId}: status of «{ReportDoc.Norm(row.Elements<TableCell>().First().InnerText)}» is not a tinted, centred badge cell");
            }
            var investment = rows.FirstOrDefault(r => ReportDoc.Norm(r.Elements<TableCell>().First().InnerText) == "الاستثمار");
            if (StatusOf(Rv.Portal(period, cityId), cityId, "investment-return") == "دون الحد" && investment is not null
                && !ReportDoc.Norm(investment.Elements<TableCell>().Last().InnerText).Contains("دون الحد المرجعي للمراقبة"))
                violations.Add($"{period}/{cityId}: investment status should say «دون الحد المرجعي للمراقبة»: {ReportDoc.Norm(investment.InnerText)}");
        }
        Rv.NoViolations(violations, "axes table");
    }

    // 5.4  Recommendation follows the platform's status; Madinah's wording from the document (current quarter and month).
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "5.4")]
    public void R5_4_recommendation_follows_the_platform_status(string period)
    {
        var stems = new Dictionary<string, string> { ["يتطلب دراسة استباقية"] = "دراسة (?:ال)?استباقية", ["يتطلب دراسة تفصيلية"] = "دراسة (?:ال)?تفصيلية", ["الضوابط مفعلة"] = "الضوابط مفعل", ["لا إجراء"] = "إجراء" };
        var violations = new List<string>();
        foreach (var cityId in Rv.CityIds(period))
        {
            var status = Rv.Portal(period, cityId).Overview.OperationalStatus;
            var section = Section(Rv.City(period, cityId), "التوصية", "ما يجب مراقبته");
            var text = string.Join(" | ", section);
            if (stems.TryGetValue(status, out var stem) && !Regex.IsMatch(ReportDoc.Norm(text), stem)) violations.Add($"{period}/{cityId}: recommendation should rest on «{status}»: {text}");
            if (status != "لا إجراء" && text.Contains("لا حاجة لأي إجراء")) violations.Add($"{period}/{cityId}: «لا حاجة لأي إجراء» contradicts «{status}»");
        }
        if (period is "2026-Q3" or "2026-09")
        {
            var madinah = string.Join(" | ", Section(Rv.City(period, "madinah"), "التوصية", "ما يجب مراقبته"));
            if (!madinah.Contains("تركز الدراسة الاستباقية على العبء السكني لذوي الدخل المحدود وضعف جاذبية العرض"))
                violations.Add($"{period}/madinah: the document's wording is missing: {madinah}");
            if (madinah.Contains("مراقبة العائد")) violations.Add($"{period}/madinah: old wording «مراقبة العائد …» still present");
        }
        Rv.NoViolations(violations, "recommendation");
    }

    // 5.5  Watch points use the full names; the heading follows 2.7.
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "5.5")]
    public void R5_5_watch_points_use_the_full_names(string period)
    {
        var violations = new List<string>();
        foreach (var (name, doc) in Rv.CityDocs(period))
            foreach (var p in Section(doc, "ما يجب مراقبته", "الهيئة"))
            {
                if (Regex.IsMatch(p, "تعاقد")) violations.Add($"{name}: «تعاقد» in: {p}");
                if (Regex.IsMatch(p, "(?<![\\p{L}])(?:ال)?عبء(?!\\s+(?:ال)?سكني)")) violations.Add($"{name}: short burden name in: {p}");
                if (Regex.IsMatch(p, "(?:ال)?إيجار (?:ال)?فعلي(?!\\s*للمسكن\\s*\\(?CPI)")) violations.Add($"{name}: actual-rent name incomplete in: {p}");
            }
        Rv.NoViolations(violations, "watch points");
    }

    // 5.6  Two line charts at the end of page 2 (rental increase per quarter, actual rent CPI per month); values checkable (native Word charts).
    private static readonly double[] RiyadhIncreaseQuarterly = { 17, 17, 19, 17, 14, 9, -1 };
    private static readonly double[] RiyadhActualRentSample = { 18.1, 18.3, 18.6, 18.6, 18.4, 17.6, 16.8, 15.7, 13.8, 12.3, 11.7, 10.9, 10.2, 9.2, 8.2, 7.3, 6.6, 5.9, 5.3, 4.5, 4.0 };

    private static List<double> Values(string chartXml) => Regex.Matches(Regex.Match(chartXml, @"<c:val>.*?</c:val>", RegexOptions.Singleline).Value, @"<c:v>([^<]+)</c:v>")
        .Select(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToList();

    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "5.6")]
    public void R5_6_page_two_ends_with_the_two_monthly_charts(string period)
    {
        var violations = new List<string>();
        foreach (var cityId in Rv.CityIds(period))
        {
            var doc = Rv.City(period, cityId);
            var portal = Rv.Portal(period, cityId);
            var name = ReportDoc.Norm(portal.City.CityName);
            var texts = doc.Paragraphs.Select(p => ReportDoc.Norm(p).Replace("(", "").Replace(")", "")).ToList();
            var first = texts.FindIndex(t => t == $"الزيادة الإيجارية في {name}");
            var second = texts.FindIndex(t => t == $"الإيجار الفعلي للمسكن CPI في {name}");
            if (first < 0 || second < 0 || second < first) violations.Add($"{period}/{cityId}: chart titles «الزيادة الإيجارية في {name}» then «الإيجار الفعلي للمسكن CPI في {name}» not found in this order");
            if (first >= 0 && first < texts.FindIndex(t => t.StartsWith("ما يجب مراقبته"))) violations.Add($"{period}/{cityId}: charts must come after the watch section");
            if (doc.ChartPartCount < 2 && !doc.NormText.Contains("لا تتوفر بيانات لهذه الفترة")) violations.Add($"{period}/{cityId}: {doc.ChartPartCount} native chart(s) — two are required so the plotted values can be verified");
            var decision = (doc.NormText + string.Concat(doc.ChartXml)).Contains("قرار تطبيق الضوابط الإيجارية");
            var enabled = portal.Overview.OperationalStatus == "الضوابط مفعلة";
            if (enabled != decision) violations.Add($"{period}/{cityId}: the «قرار تطبيق الضوابط الإيجارية» line must appear exactly for enabled cities (enabled={enabled})");
        }
        Rv.NoViolations(violations, "charts");
    }

    // The client's mock plots both charts monthly (January 2025 – September 2026), but the rental increase is a quarterly
    // platform indicator, so its chart plots the published quarters; the actual rent CPI stays monthly (hand-copied values).
    [Theory, Trait("Requirement", "5.6")]
    [InlineData("2026-09"), InlineData("2026-Q3")]
    public void R5_6_riyadh_charts_plot_the_documents_sample_values(string period)
    {
        var doc = Rv.City(period, "riyadh");
        Assert.True(doc.ChartXml.Count >= 2, "Riyadh: native charts are missing");
        Assert.Equal(RiyadhIncreaseQuarterly, Values(doc.ChartXml[0]));
        var quarters = Categories(doc.ChartXml[0]);
        Assert.Equal(7, quarters.Count);
        Assert.Contains("الربع الأول", quarters[0]);
        Assert.Contains("الربع الثالث", quarters[^1]);
        Assert.Equal(RiyadhActualRentSample.Length, Values(doc.ChartXml[1]).Count);
        Assert.All(RiyadhActualRentSample.Zip(Values(doc.ChartXml[1])), p => Assert.InRange(p.Second, p.First - 0.06, p.First + 0.06));
        var months = Categories(doc.ChartXml[1]);
        Assert.Equal(21, months.Count);
        Assert.Contains("يناير", months[0]);
        Assert.Contains("سبتمبر", months[^1]);
    }

    private static List<string> Categories(string chartXml) => Regex.Matches(chartXml, @"<c:cat>.*?</c:cat>", RegexOptions.Singleline).Cast<Match>()
        .SelectMany(m => Regex.Matches(m.Value, @"<c:v>([^<]+)</c:v>").Select(x => x.Groups[1].Value)).ToList();
}

// Every numbered requirement of the document has at least one check (so none is forgotten when the fixes are made).
public class RequirementsCoverageTests
{
    private static readonly string[] All =
        Enumerable.Range(1, 8).Select(i => $"2.{i}")
            .Concat(Enumerable.Range(1, 13).Select(i => $"3.{i}"))
            .Concat(Enumerable.Range(1, 9).Select(i => $"4.{i}"))
            .Concat(Enumerable.Range(1, 6).Select(i => $"5.{i}")).ToArray();

    [Fact, Trait("Requirement", "coverage")]
    public void R0_every_requirement_has_a_check()
    {
        var covered = typeof(RequirementsCoverageTests).Assembly.GetTypes().Where(t => t.Name.StartsWith("Requirements"))
            .SelectMany(t => t.GetMethods())
            .SelectMany(m => m.GetCustomAttributesData())
            .Where(a => a.AttributeType == typeof(TraitAttribute) && (string?)a.ConstructorArguments[0].Value == "Requirement")
            .Select(a => (string?)a.ConstructorArguments[1].Value).ToHashSet();
        Assert.Empty(All.Where(id => !covered.Contains(id)));
    }
}
