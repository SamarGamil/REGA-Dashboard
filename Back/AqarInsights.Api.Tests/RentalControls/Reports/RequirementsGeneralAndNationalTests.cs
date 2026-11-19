using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Wordprocessing;
using AqarInsights.Api.Models.RentalControls;
using AqarInsights.Api.Services.RentalControls.Snapshots;

namespace AqarInsights.Api.Tests.RentalControls.Reports;

// VERIFICATION of "متطلبات تعديل منصة الضوابط الإيجارية" (REGA, 04/11/2026), sections 2 and 3.
// One test per numbered requirement ([Trait("Requirement", "x.y")]); each runs against the generated Word reports of every
// reportable period (months and quarters) and compares them with the platform's own figures or with literals taken from
// the requirements document. Written BEFORE the fixes: a red test names a requirement that is not met yet.
public class RequirementsGeneralTests
{
    public static IEnumerable<object[]> Periods() => Rv.AllPeriods();

    // 2.1  Show the actual period instead of the generic frequency tag.
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "2.1")]
    public void R2_1_generic_frequency_tags_are_gone(string period)
    {
        var tag = new Regex("\u00a0(شهري|ربعي|سنوي)\u00a0|[-–]\\s*سنوي");
        var violations = Rv.ScanRaw(Rv.AllDocs(period), ("generic tag", tag));
        Rv.NoViolations(violations, "a «شهري/ربعي/سنوي» tag is still printed");
    }

    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "2.1")]
    public void R2_1_every_city_indicator_shows_the_period_the_platform_gives_it(string period)
    {
        var violations = new List<string>();
        foreach (var cityId in Rv.CityIds(period))
        {
            var portal = Rv.Portal(period, cityId);
            var table = Rv.City(period, cityId).Tables.First(t => t[0].Count == 4 && t[0][0] == "المؤشر");
            (string Row, string Word)[] expected = new[]
            {
                ("الزيادة الإيجارية", "rental-increase"), ("نسبة الإيجار إلى الدخل", "rent-to-income"), ("العبء السكني", "housing-burden"),
            }.Select(x => (x.Item1, Rv.PeriodWord(portal.Panels.FirstOrDefault(p => p.Id == x.Item2)?.PeriodLabel ?? ""))).ToArray();
            foreach (var (row, word) in expected)
            {
                var cell = table.FirstOrDefault(r => ReportDoc.Norm(r[0]).StartsWith(row, StringComparison.Ordinal))?[0] ?? "";
                if (word.Length > 0 && !ReportDoc.Norm(cell).Contains(word, StringComparison.Ordinal))
                    violations.Add($"{period}/{cityId}: row «{row}» should show «{word}» but reads «{ReportDoc.Norm(cell)}»");
            }
            foreach (var (row, column) in new[] { ("الإيجار الفعلي للمسكن", "الإيجار الفعلي للمسكن"), ("التضخم العام", "التضخم العام") })
            {
                var month = Rv.LastFilled(portal.City, column)?.Month;
                var cell = table.FirstOrDefault(r => ReportDoc.Norm(r[0]).StartsWith(row, StringComparison.Ordinal))?[0] ?? "";
                if (month is not null && !ReportDoc.Norm(cell).Contains(Rv.PeriodWord(month), StringComparison.Ordinal))
                    violations.Add($"{period}/{cityId}: row «{row}» should show «{Rv.PeriodWord(month)}» but reads «{ReportDoc.Norm(cell)}»");
            }
        }
        Rv.NoViolations(violations, "city indicator rows without the platform's period");
    }

    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "2.1")]
    public void R2_1_national_cards_and_columns_show_the_period(string period)
    {
        var doc = Rv.National(period);
        var panels = Rv.Panels(period);
        string Quarter(string id) => Rv.PeriodWord(panels.FirstOrDefault(p => p.Id == id)?.PeriodLabel ?? "");
        var cpiMonth = Rv.CityIds(period).Select(c => Rv.LastFilled(Rv.Portal(period, c).City, "الإيجار الفعلي للمسكن")?.Month)
            .FirstOrDefault(m => m is not null) ?? "";
        var violations = new List<string>();
        void Expect(string where, string text, string word)
        {
            if (word.Length > 0 && !ReportDoc.Norm(text).Contains(word, StringComparison.Ordinal))
                violations.Add($"{period}: {where} should show «{word}» but reads «{ReportDoc.Norm(text)}»");
        }
        Expect("card «أعلى إيجار فعلي»", doc.Paragraphs.FirstOrDefault(p => ReportDoc.Norm(p).Contains("أعلى إيجار فعلي")) ?? "", Rv.PeriodWord(cpiMonth));
        Expect("card «أدنى عائد إيجاري»", doc.Paragraphs.FirstOrDefault(p => ReportDoc.Norm(p).Contains("أدنى عائد إيجاري")) ?? "", Quarter("investment-return"));
        Expect("card «أعلى عبء سكني»", doc.Paragraphs.FirstOrDefault(p => ReportDoc.Norm(p).Contains("أعلى عبء سكني")) ?? "", Quarter("housing-burden"));
        var header = doc.Tables.First(t => t[0].Any(c => ReportDoc.Norm(c) == "المدينة"))[0];
        Expect("column «العبء السكني»", header.FirstOrDefault(c => ReportDoc.Norm(c).Contains("العبء السكني")) ?? "", Quarter("housing-burden"));
        Expect("column «الإيجار الفعلي»", header.FirstOrDefault(c => ReportDoc.Norm(c).Contains("الإيجار الفعلي")) ?? "", Rv.PeriodWord(cpiMonth));
        Rv.NoViolations(violations, "national report without the platform's period");
    }

    // 2.2  «الحد المرجعي» is always «الحد المرجعي للمراقبة» (cards, bullets, legend, narrative).
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "2.2")]
    public void R2_2_reference_limit_is_always_named_for_monitoring(string period)
    {
        var violations = Rv.Scan(Rv.AllDocs(period), ("short name", new Regex("الحد المرجعي(?! للمراقبة)")));
        Rv.NoViolations(violations, "«الحد المرجعي» without «للمراقبة»");
    }

    // 2.3  Full, platform names for the indicators.
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "2.3")]
    public void R2_3_indicator_names_are_complete(string period)
    {
        var violations = Rv.Scan(Rv.AllDocs(period),
            ("«تعاقد»", new Regex("تعاقد")),
            ("«عبء» alone", new Regex("(?<![\\p{L}])(?:ال)?عبء(?!\\s+(?:ال)?سكني)")),
            ("«الشواغر» alone", new Regex("الشواغر(?!\\s+الإيجارية)")),
            ("burden without «لذوي»", new Regex("عبء سكني للدخل|العبء السكني للدخل")),
            ("«عائد» alone", new Regex("(?<![\\p{L}])(?:ال)?عائد(?!\\s+إيجاري|\\s+الإيجاري)")));
        Rv.NoViolations(violations, "abbreviated or non-platform indicator names");
    }

    // 2.4  The actual-rent indicator carries its full name wherever it is mentioned.
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "2.4")]
    public void R2_4_actual_rent_always_carries_its_full_name(string period)
    {
        var violations = Rv.Scan(Rv.AllDocs(period),
            ("short actual-rent name", new Regex("(?:ال)?إيجار (?:ال)?فعلي(?!\\s*للمسكن\\s*\\(?CPI)")));
        Rv.NoViolations(violations, "«الإيجار الفعلي» without «للمسكن CPI»");
    }

    // 2.5  A high actual rent is shown in red (the colour of its status on the platform).
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "2.5")]
    public void R2_5_high_actual_rent_is_red_in_the_cards(string period)
    {
        var violations = new List<string>();
        foreach (var cityId in Rv.CityIds(period))
        {
            var portal = Rv.Portal(period, cityId);
            var status = portal.City.MarketMovementTable.FirstOrDefault(m => m.Indicator.StartsWith("الإيجار الفعلي للمسكن"))?.Assessment;
            if (status != "ارتفاع") continue;
            var doc = Rv.City(period, cityId);
            foreach (var card in doc.TableElements.SelectMany(t => t.Descendants<TableCell>())
                .Where(c => ReportDoc.Norm(c.InnerText).Contains("الإيجار الفعلي للمسكن") && c.InnerText.Contains('%') && !c.Descendants<Table>().Any()))
                if (Rv.PercentRunColors(card).Any(c => !Rv.IsRed(c)))
                    violations.Add($"{period}/{cityId}: card «{ReportDoc.Norm(card.InnerText)}» shows its figure in {string.Join(",", Rv.PercentRunColors(card))}, not red");
        }
        Rv.NoViolations(violations, "high actual-rent figure not red");
    }

    // 2.6  «فيشر» is removed from the index column title.
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "2.6")]
    public void R2_6_index_column_is_titled_without_fisher(string period)
    {
        var violations = new List<string>();
        foreach (var (name, doc) in Rv.CityDocs(period))
        {
            var header = doc.Tables.FirstOrDefault(t => t[0].Count > 0 && t[0][0] == "المنتج")?[0];
            if (header is null) { violations.Add($"{name}: products table not found"); continue; }
            if (header[^1] != "الرقم القياسي") violations.Add($"{name}: last column title is «{header[^1]}», expected «الرقم القياسي»");
            if (doc.FullText.Contains("فيشر")) violations.Add($"{name}: «فيشر» still printed");
        }
        Rv.NoViolations(violations, "index column title");
    }

    // 2.7  The watch section has no «next month / next quarter» wording.
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "2.7")]
    public void R2_7_watch_section_heading_has_no_next_period_wording(string period)
    {
        var violations = new List<string>();
        foreach (var (name, doc) in Rv.AllDocs(period))
        {
            var heading = doc.Paragraphs.Select(ReportDoc.Norm).FirstOrDefault(p => Regex.IsMatch(p, @"^ما (?:يجب|يلزم) مراقبته"));
            if (heading is null) { violations.Add($"{name}: watch heading not found"); continue; }
            if (heading.Contains("القادم")) violations.Add($"{name}: heading still says «القادم»: {heading}");
            if (!heading.Contains("كمتابعة دورية")) violations.Add($"{name}: heading lacks «كمتابعة دورية»: {heading}");
            if (!name.EndsWith("national") && !heading.StartsWith("ما يجب مراقبته كمتابعة دورية"))
                violations.Add($"{name}: city heading should read «ما يجب مراقبته كمتابعة دورية»: {heading}");
        }
        Rv.NoViolations(violations, "watch section heading");
    }

    // 2.8  Every value in the cards and the indicator table is the platform's value, rounded as the platform shows it.
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "2.8")]
    public void R2_8_city_figures_equal_the_platform_figures(string period)
    {
        var violations = new List<string>();
        foreach (var cityId in Rv.CityIds(period))
        {
            var portal = Rv.Portal(period, cityId);
            var doc = Rv.City(period, cityId);
            var table = doc.Tables.First(t => t[0].Count == 4 && t[0][0] == "المؤشر");
            foreach (var (row, panelId) in new[] { ("الزيادة الإيجارية", "rental-increase"), ("نسبة الإيجار إلى الدخل", "rent-to-income"), ("العبء السكني", "housing-burden") })
            {
                var shown = Rv.Num(table.FirstOrDefault(r => ReportDoc.Norm(r[0]).StartsWith(row))?[1]);
                var panel = portal.Panels.First(p => p.Id == panelId).Cities.FirstOrDefault(c => c.CityId == cityId);
                var platform = Rv.Num(panel?.DisplayValue);
                if (platform is not null && shown != platform)
                    violations.Add($"{period}/{cityId}: «{row}» shows {shown}, the platform shows {panel!.DisplayValue}");
            }
            var yieldCard = doc.TableElements.SelectMany(t => t.Descendants<TableCell>())
                .FirstOrDefault(c => ReportDoc.Norm(c.InnerText).StartsWith("العائد الإيجاري") && !c.Descendants<Table>().Any());
            var yieldShown = Rv.Num(Regex.Match(ReportDoc.Norm(yieldCard?.InnerText ?? ""), @"\d+(?:\.\d+)?%").Value);
            var yieldPlatform = Rv.Num(portal.Panels.First(p => p.Id == "investment-return").Cities.FirstOrDefault(c => c.CityId == cityId)?.DisplayValue);
            if (yieldPlatform is not null && yieldShown != yieldPlatform)
                violations.Add($"{period}/{cityId}: yield shows {yieldShown}, the indicators page shows {yieldPlatform}");
            // The document's example (Dammam): the city page shows 5% while the report printed 5.4%.
            var cityPageYield = Rv.Num(portal.City.Kpis.FirstOrDefault(k => k.Label == "العائد الإيجاري")?.Value);
            if (cityPageYield is not null && yieldShown != cityPageYield)
                violations.Add($"{period}/{cityId}: yield shows {yieldShown}, the city page shows {cityPageYield}");
        }
        Rv.NoViolations(violations, "figures that differ from the platform");
    }
}

public class RequirementsNationalTests
{
    public static IEnumerable<object[]> Periods() => Rv.AllPeriods();
    public static IEnumerable<object[]> Monthly() => Rv.MonthlyPeriods();

    private static Table CityTable(ReportDoc doc) => doc.TableElements.First(t => t.Elements<TableRow>().First().Elements<TableCell>().Any(c => ReportDoc.Norm(c.InnerText) == "المدينة"));

    private static List<List<string>> RowsOf(Table t) => t.Elements<TableRow>()
        .Select(r => r.Elements<TableCell>().Select(c => ReportDoc.Norm(c.InnerText)).ToList()).ToList();

    // 3.1
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "3.1")]
    public void R3_1_title_names_the_cities_status(string period)
    {
        var doc = Rv.National(period);
        Assert.Contains("التقرير الوطني لحالة المدن للضوابط الإيجارية", ReportDoc.Norm(doc.HeaderText));
    }

    // 3.2
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "3.2")]
    public void R3_2_subtitle_gains_the_assessment_line_with_the_report_period(string period)
    {
        var doc = Rv.National(period);
        var label = Rv.Period(period).Label;
        var text = Regex.Replace(ReportDoc.Norm(doc.HeaderText), "[–—-]", "–");
        Assert.Contains($"تقييم حالة المدن استنادا إلى مؤشرات الضوابط الإيجارية – {label}", text);
        Assert.DoesNotContain("مؤشرات عواصم المناطق الإدارية", text); // subheader removed at the client's request
    }

    // 3.3  a new executive-summary point on the actual rent, computed from the platform (every report). Client review
    //      (national, August 2026, note 2): the count is the table's own ↓ arrows and the named city's value is the one
    //      the table prints («تباطؤ … في 10 مدن، وانخفض في الرياض إلى +4.5%», not «5% بعد التقريب»).
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "3.3")]
    public void R3_3_summary_reports_how_many_cities_slowed_in_actual_rent(string period)
    {
        var table = RowsOf(CityTable(Rv.National(period)));
        var name = table[0].FindIndex(c => c.Contains("المدينة"));
        var rent = table[0].FindIndex(c => c.Contains("الإيجار الفعلي"));
        var fell = table.Skip(1).Where(r => Regex.IsMatch(r[rent], "[↓▼]")).ToList();
        var bullet = Rv.National(period).Paragraphs.Select(ReportDoc.Norm)
            .FirstOrDefault(p => p.StartsWith("●") && p.Contains("تباطؤ الإيجار الفعلي للمسكن CPI"));
        if (fell.Count == 0)
        {
            Assert.True(bullet is null, $"no ↓ in the actual-rent column, yet the summary says: {bullet}");
            return;
        }
        Assert.True(bullet is not null, $"the table shows {fell.Count} ↓ in the actual-rent column but the summary has no «تباطؤ الإيجار الفعلي للمسكن CPI في …» point");
        Assert.Matches($@"في (?:{fell.Count} (?:مدينة|مدن)|مدينتين|مدينة واحدة)", bullet!);
        Assert.DoesNotContain("بعد التقريب", bullet);
        var named = Regex.Match(bullet!, @"وانخفض في (?<city>.+?) إلى (?<value>[+-]?\d+(?:\.\d+)?%)");
        Assert.True(named.Success, $"the point names no city and value: {bullet}");
        var row = fell.FirstOrDefault(r => r[name] == named.Groups["city"].Value);
        Assert.True(row is not null, $"«{named.Groups["city"].Value}» is named but has no ↓ in the table");
        if (fell.Any(r => r[name] == "الرياض")) Assert.Equal("الرياض", named.Groups["city"].Value);
        Assert.Contains(named.Groups["value"].Value, row![rent]);
    }

    // 3.4
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "3.4")]
    public void R3_4_top_actual_rent_card_has_the_full_name_and_the_month(string period)
    {
        var doc = Rv.National(period);
        var card = doc.Paragraphs.Select(ReportDoc.Norm).FirstOrDefault(p => p.Contains("أعلى إيجار فعلي")) ?? "";
        Assert.Matches(@"^أعلى إيجار فعلي للمسكن\s*\(?CPI\)?", card);
        var month = Rv.CityIds(period).Select(c => Rv.LastFilled(Rv.Portal(period, c).City, "الإيجار الفعلي للمسكن")?.Month).FirstOrDefault(m => m is not null);
        if (month is not null) Assert.Contains(Rv.PeriodWord(month), card);
    }

    // 3.5  (reports only) cities sorted by the compound rental growth rate, and the note says so.
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "3.5")]
    public void R3_5_cities_are_sorted_by_compound_growth_with_the_matching_note(string period)
    {
        var doc = Rv.National(period);
        var violations = new List<string>();
        if (!ReportDoc.Norm(string.Join("\n", doc.Paragraphs)).Contains("المدن مرتبة تنازليا حسب معدل النمو الإيجاري المركب"))
            violations.Add($"{period}: the ordering note does not say «المدن مرتبة تنازليًا حسب معدل النمو الإيجاري المركب»");
        if (doc.NormText.Contains("الضغط المركب")) violations.Add($"{period}: the old basis «الضغط المركب» is still printed");
        var table = RowsOf(CityTable(doc));
        var nameColumn = table[0].FindIndex(c => c.Contains("المدينة"));
        var cagr = Rv.Period(period).Aggregation.GetMarketAnalysis().Cities.ToDictionary(c => c.Name, c => c.Cagr ?? double.MinValue);
        var order = table.Skip(1).Select(r => cagr[r[nameColumn]]).ToList();
        if (!order.SequenceEqual(order.OrderByDescending(v => v))) violations.Add($"{period}: rows are not in descending compound-growth order: {string.Join(", ", order)}");
        Rv.NoViolations(violations, "national sort");
    }

    // 3.6
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "3.6")]
    public void R3_6_region_column_is_removed(string period)
    {
        var header = RowsOf(CityTable(Rv.National(period)))[0];
        Assert.DoesNotContain(header, c => c.Contains("الإقليم"));
    }

    // 3.7  Four indicator columns, in this order.
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "3.7")]
    public void R3_7_indicator_columns_are_the_four_in_order(string period)
    {
        var table = RowsOf(CityTable(Rv.National(period)));
        var header = table[0].Select(h => h.Replace("(", "").Replace(")", "")).ToList();
        var expected = new[] { "الزيادة الإيجارية", "نسبة الإيجار إلى الدخل", "العبء السكني لذوي الدخل المحدود", "الإيجار الفعلي للمسكن CPI" };
        var at = expected.Select(e => header.FindIndex(h => h.Contains(e))).ToArray();
        Assert.True(at.All(i => i >= 0), $"columns missing; header is: {string.Join(" | ", table[0])}");
        Assert.Equal(at.OrderBy(i => i).ToArray(), at);
        Assert.All(table.Skip(1), r => Assert.All(at, i => Assert.False(string.IsNullOrWhiteSpace(r[i]))));
    }

    // 3.8  Trend arrows on exactly two columns: العبء السكني (quarterly) against the previous quarter and الإيجار الفعلي
    //      (monthly) against the previous month. ↑ when it rose, ↓ when it fell, «—» when unchanged (client review,
    //      national August 2026, note 4: a symbol beside every value); nothing when the previous value is not available.
    //      No other column carries an arrow.
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "3.8")]
    public void R3_8_arrows_follow_the_change_from_the_previous_period(string period)
    {
        var data = Rv.Period(period);
        var quarter = data.Scope.Quarter!.Value;
        var prevQuarter = quarter.Quarter == 1 ? new YearQuarter(quarter.Year - 1, 4) : new YearQuarter(quarter.Year, quarter.Quarter - 1);
        var month = data.Scope.Month ?? quarter.LastMonth;
        var prevMonth = month.Month == 1 ? new YearMonth(month.Year - 1, 12) : new YearMonth(month.Year, month.Month - 1);
        var prevQ = Rv.PeriodOrNull(prevQuarter.Id)?.Aggregation.GetMarketAnalysis().Cities.ToDictionary(c => c.Name);
        var prevM = Rv.PeriodOrNull(prevMonth.Id)?.Aggregation.GetMarketAnalysis().Cities.ToDictionary(c => c.Name);
        var now = data.Aggregation.GetMarketAnalysis().Cities.ToDictionary(c => c.Name);

        var table = RowsOf(CityTable(Rv.National(period)));
        var name = table[0].FindIndex(c => c.Contains("المدينة"));
        var arrowColumns = new (string Column, Func<CityMarketMetric, double?> Pick, Dictionary<string, CityMarketMetric>? Prev)[]
        {
            ("العبء السكني", k => k.HousingBurden, prevQ),
            ("الإيجار الفعلي", k => k.ActualRentalGrowth, prevM),
        };
        var violations = new List<string>();
        var verified = 0;
        foreach (var (column, pick, prev) in arrowColumns)
        {
            var index = table[0].FindIndex(h => h.Contains(column));
            if (index < 0) { violations.Add($"{period}: column «{column}» not found"); continue; }
            foreach (var row in table.Skip(1))
            {
                var cell = row[index];
                var up = Regex.IsMatch(cell, "[↑▲]"); var down = Regex.IsMatch(cell, "[↓▼]"); var same = cell.Contains('—');
                var got = up ? "up" : down ? "down" : same ? "unchanged" : "none";
                var hasValue = Regex.IsMatch(cell, @"\d");
                now.TryGetValue(row[name], out var cur);
                CityMarketMetric? old = null;
                prev?.TryGetValue(row[name], out old);
                var (a, b) = (cur is null ? null : pick(cur), old is null ? null : pick(old));
                if (!hasValue) continue;
                if (a is null || b is null)
                {
                    verified++;
                    if (got != "none")
                        violations.Add($"{period}/{row[name]} «{column}»: no previous value → no symbol, cell reads «{cell}»");
                    continue;
                }
                var want = a > b ? "up" : a < b ? "down" : "unchanged";
                verified++;
                if (want != got) violations.Add($"{period}/{row[name]} «{column}»: {a} vs previous {b} → arrow should be {want}, cell reads «{cell}»");
            }
        }
        foreach (var column in new[] { "الزيادة الإيجارية", "نسبة الإيجار إلى الدخل", "العائد الإيجاري", "المسار" })
        {
            var index = table[0].FindIndex(h => h.Contains(column));
            if (index >= 0 && table.Skip(1).Any(r => Regex.IsMatch(r[index], "[↑▲↓▼]")))
                violations.Add($"{period}: column «{column}» must not carry arrows");
        }
        Assert.True(verified > 0, "no arrow cell could be verified");
        Rv.NoViolations(violations, "arrows");
    }

    // 3.9  Track badges share one alignment and one padding in every row.
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "3.9")]
    public void R3_9_track_badges_are_aligned_identically(string period)
    {
        var table = CityTable(Rv.National(period));
        var header = RowsOf(table)[0];
        var index = header.FindIndex(h => h.Contains("المسار"));
        var cells = table.Elements<TableRow>().Skip(1).Select(r => r.Elements<TableCell>().ElementAt(index)).ToList();
        var alignments = cells.Select(c => c.Descendants<Paragraph>().First().ParagraphProperties?.Justification?.Val?.Value.ToString() ?? "none").Distinct().ToList();
        var indents = cells.Select(c => c.Descendants<Paragraph>().First().ParagraphProperties?.Indentation?.OuterXml ?? "").Distinct().ToList();
        var padding = cells.Select(c => (Regex.Match(c.InnerText, @"^[\s\u00a0]*").Length, Regex.Match(c.InnerText, @"[\s\u00a0]*$").Length)).Distinct().ToList();
        Assert.True(alignments.Count == 1, "badge cells use different alignments: " + string.Join(",", alignments));
        Assert.True(indents.Count == 1, "badge cells use different indents");
        Assert.True(padding.Count == 1, "badge texts carry different amounts of padding: " + string.Join(",", padding));
    }

    // 3.10 / 3.11  Intervention priorities: heading, even spacing, last badge.
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "3.10")]
    public void R3_10_intervention_section_is_titled_by_cities_with_even_spacing(string period)
    {
        var doc = Rv.National(period);
        var violations = new List<string>();
        if (!doc.Paragraphs.Select(ReportDoc.Norm).Contains("أولويات التدخل بالمدن")) violations.Add($"{period}: the heading «أولويات التدخل بالمدن» is missing");
        var groups = doc.TableElements.SelectMany(t => t.Descendants<Table>())
            .FirstOrDefault(t => t.Elements<TableRow>().Any(r => r.InnerText.Contains("دراسة تفصيلية") || r.InnerText.Contains("التدخل مفع")));
        if (groups is null) { violations.Add($"{period}: the intervention groups table was not found"); Rv.NoViolations(violations, "intervention section"); return; }
        var labelCells = groups.Elements<TableRow>().Select(r => r.Elements<TableCell>().First()).ToList();
        var widths = labelCells.Select(c => c.TableCellProperties?.TableCellWidth?.Width?.Value ?? "").Distinct().ToList();
        var align = labelCells.Select(c => c.Descendants<Paragraph>().First().ParagraphProperties?.Justification?.Val?.Value.ToString() ?? "none").Distinct().ToList();
        var padding = labelCells.Select(c => (Regex.Match(c.InnerText, @"^[\s\u00a0]*").Length, Regex.Match(c.InnerText, @"[\s\u00a0]*$").Length)).Distinct().ToList();
        if (widths.Count != 1 || align.Count != 1 || padding.Count != 1)
            violations.Add($"{period}: label cells differ: widths={string.Join(",", widths)} alignments={string.Join(",", align)} padding={string.Join(";", padding)}");
        Rv.NoViolations(violations, "intervention section");
    }

    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "3.11")]
    public void R3_11_last_badge_reads_no_action_only(string period)
    {
        var doc = Rv.National(period);
        Assert.DoesNotContain("استباقي روتيني", doc.NormText);
        var anyNoAction = Rv.Period(period).Aggregation.GetOverview().Cities.Any(c => c.OperationalStatus == "لا إجراء");
        if (anyNoAction) Assert.Contains(doc.Paragraphs.Select(ReportDoc.Norm), p => p == "لا إجراء");
    }

    // 3.12
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "3.12")]
    public void R3_12_yield_watch_point_is_replaced(string period)
    {
        var text = Rv.National(period).NormText;
        Assert.DoesNotContain("إشارة مبكرة على ضغط أسعار البيع", text);
        Assert.Contains("تحليل انخفاض العائد الإيجاري بالربط بين حركة الإيجار وأسعار البيع", text);
    }

    // 3.13  New section on the effect of the activated controls, from the platform's impact data.
    [Theory, MemberData(nameof(Periods)), Trait("Requirement", "3.13")]
    public void R3_13_impact_section_shows_the_enabled_cities_effects(string period)
    {
        var doc = Rv.National(period);
        var at = doc.ParagraphIndex("أثر الضوابط الإيجارية المفعلة");
        Assert.True(at >= 0, "section «أثر الضوابط الإيجارية المفعلة» is missing");
        Assert.True(at > doc.ParagraphIndex("أولويات التدخل بالمدن"), "the section must come after «أولويات التدخل»");
        var section = string.Join("\n", doc.Paragraphs.Skip(at).Select(ReportDoc.Norm));
        var enabled = Rv.Period(period).Aggregation.GetOverview().Cities.Where(c => c.OperationalStatus == "الضوابط مفعلة" && c.ImpactData.Available).ToList();
        Assert.NotEmpty(enabled);
        foreach (var city in enabled)
        {
            Assert.Contains(city.Name, section);
            Assert.True(Regex.Matches(section, "●").Count >= 3, $"{city.Name}: fewer than three impact points");
            var change = Rv.Num(city.ImpactData.Change);
            Assert.True(change is null || section.Contains(Math.Abs(change.Value).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)),
                $"{city.Name}: the platform's impact change «{city.ImpactData.Change}» is not reflected");
        }
        // The sketch's three statements («انخفض…», «لوحظ…», «كلا المؤشرين…») read both indicators since the decision.
        Assert.Contains("منذ قرار التطبيق", section);
        Assert.Matches("(?:انخفضت|ارتفعت|ثبتت) الزيادة الإيجارية", section);
    }
}
