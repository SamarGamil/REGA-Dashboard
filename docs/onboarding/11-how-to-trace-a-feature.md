# 11 — كيف تتبع ميزة، وأين تعدّل لكل نوع طلب

## 1) وصفة التتبع (Trace Recipe) — 7 خطوات

لأي ميزة أو خطأ، اتبع هذا الترتيب. في كل خطوة سجّل **الملف والدالة**.

1. **الشاشة:** أي صفحة وأي مكوّن يعرض الرقم/النص؟ (ابحث عن النص العربي في `Front/src` بـ grep.)
2. **المسار:** في [app.routes.ts](../../Front/src/app/app.routes.ts) أي مكوّن؟ وهل خلف `authGuard`؟
3. **الاستدعاء:** أي خدمة وأي دالة تطلب البيانات؟ ومع أي معاملات (`period`، فلاتر)؟ — DevTools → Network يؤكد.
4. **الـ Controller:** أي endpoint ([06](./06-backend-guide.md) §2)؟ ماذا يفعل بالـ `period`؟
5. **التجميع:** أي `Projection`/`AggregationService` يبني الـ DTO؟
6. **المصدر:** من أين جاء الرقم الخام؟ (جدول في `tables/live`؟ إصدار ربع؟ ملف Excel؟ `transactions.json`؟)
7. **القواعد الوسيطة:** هل هناك قناع فترة (`PeriodValueMask`)، تصحيح (`corrections`)، تقريب، أو عتبة غيّرت القيمة؟

## 2) مثال محلول (أ): «حالة المدن» — مؤشرات الإيجار

| الخطوة | أين | ماذا يحدث |
|---|---|---|
| الشاشة | [city-status.component.ts](../../Front/src/app/pages/rental-controls/city-status/city-status.component.ts) + مكوّنات `components/` | بطاقات مدن + ملخص وطني + مراحل تشغيلية + أثر + توصيات |
| المسار | `rental-indicators/city-status` (وهو مسار الجذر `/` أيضاً) | `authGuard` |
| الاستدعاء | الـ constructor: `portal.connect(period => forkJoin({ overview: api.getOverview(period), riyadh: api.getCity('riyadh', period).pipe(catchError(() => of(null))) }))` | يعاد التنفيذ عند تغيّر الفترة المشتركة |
| الطلب | [rental-controls.service.ts](../../Front/src/app/services/rental-controls.service.ts) | `GET {apiUrl}/rental-controls/overview?period=…` و`/cities/riyadh?period=…` |
| الـ Controller | [RentalControlsController.GetOverview](../../Back/AqarInsights.Api/Controllers/RentalControlsController.cs) | `ViewAsync(period)` → `IRentalControlsPeriodService.ResolveAsync` → `view.Aggregation.GetOverview()`؛ فترة مجهولة → 404 |
| التجميع | [RentalControlsAggregationService.Recompute](../../Back/AqarInsights.Api/Services/RentalControls/RentalControlsAggregationService.cs) → [OverviewProjection.Build](../../Back/AqarInsights.Api/Services/RentalControls/Projections/OverviewProjection.cs) | تحويل `CityRecord` إلى `CityData` (نسب مُنسَّقة، تصنيف من «المسار»، حالة تشغيلية، ملخص وطني، مراحل) |
| المصدر | `tables/live/cities.json` + `city_status.json` + سلاسل المؤشرات `observations.json` ← مولَّدة من `لوحة الضوابط الايجارية للمدن.xlsx` وأوراق المدن بواسطة `RentalControlsConverterService` | تُقرأ عند الإقلاع |
| القواعد الوسيطة | عائد إيجاري بخانة عشرية (`Yield`)، تقريب نصف بعيداً عن الصفر، `—` للقيم الفارغة | |
| العرض | فلاتر الحالة/التصنيف/المدينة **على العميل** (`filteredCities` computed) | لا طلب جديد عند الفلترة |

**ما تتعلمه:** (1) لا حساب سياسة هنا؛ التصنيف والحالة من الملف. (2) الفترة تغيّر الطلب نفسه. (3) بعض أجزاء الصفحة مصطنعة (الأثر).

## 3) مثال محلول (ب): «الملخص التنفيذي» — تملك الأجانب

| الخطوة | أين | ماذا يحدث |
|---|---|---|
| الشاشة | [executive-summary.component.ts](../../Front/src/app/pages/executive-summary/executive-summary.component.ts) | KPIs + رسمان (حجم/قيمة) |
| المسار | `foreign-ownership/executive-summary` | |
| الاستدعاء | `ngOnInit` → `loadData()` + اشتراك في `filterState.filterChanged$` | `api.getSummary(filterState.toQueryParams())` |
| الطلب | [dashboard-api.service.ts](../../Front/src/app/services/dashboard-api.service.ts) | `GET /api/dashboard/summary?period=monthly&region=…` |
| الـ Controller | [DashboardController.GetSummary](../../Back/AqarInsights.Api/Controllers/DashboardController.cs) | `[FromQuery] DashboardFilterParams` → `aggregation.GetSummary(filters)` |
| التجميع | [AggregationService.GetSummary](../../Back/AqarInsights.Api/Services/ForeignOwnership/AggregationService.cs) → `SummaryProjection.Build` | snapshot إن لم توجد فلاتر والفترة شهرية، وإلا إعادة حساب من الصفقات الخام بعد `ApplyFilters` و`FilterByLast6Periods` |
| المصدر | `Data/transactions.json` ← من Excel بـ `ExcelConverterService` | تُحمَّل عند الإقلاع أو بعد الاستيراد |
| الاستجابة | `kpi`, `monthlySummary`, `growthRates`… | `hasData = monthlySummary.length > 0` |
| العرض | `buildCharts(...)` ← ECharts مع `applyRtl` | |

## 4) «من أين جاء هذا الرقم؟» — شجرة قرار سريعة

```
الرقم خاطئ/غير متوقع؟
├─ هل الفترة المختارة صحيحة؟ (شهر أم ربع؟ live أم إصدار؟)           → Network: ?period=
├─ هل القيمة شهرية داخل ربع؟ (قد تكون "—" أو مقنّعة PeriodValueMask)   → 02 §9
├─ هل هي من إصدار مجمّد؟ (لن تتغير بعد استيراد جديد)                    → tables/<yyyy-Qn> + corrections
├─ هل غُيّرت بتصحيح؟                                                   → tables/corrections/<release>.json
├─ هل هي نص من تقرير/ملخص؟ (مكتوب يدوياً)                              → city-reports/<city>.json
├─ هل تقريب/تنسيق؟ (نصف بعيداً عن الصفر، خانة عشرية)                    → RentalControlsMappings.Percent
├─ في تملك الأجانب: هل العدد مرجّح بالحصة؟ هل الفلاتر مطبقة؟            → 03 §3 و§5
└─ هل الملف المصدر نفسه (Excel) يحتوي هذا الرقم؟                       → افتحه وقارن
```

## 5) كتالوج التعديلات الشائعة

### أ) تغيير نص ثابت في صفحة
1. ابحث عن النص: `grep` في `Front/src/app`.
2. عدّل القالب/المكوّن. تذكّر RTL: لف الأرقام بـ `<span dir="ltr">` عند الحاجة.
3. إن كان النص قادماً من الـ API (حالة، تسمية مؤشر) فابحث في `Back` (قد يكون مكرراً في الواجهة أيضاً).
4. شغّل الاختبارات المتعلقة بالنص (الإيجار: `AllPeriodsWordingStandardsTests`, `ReportReferenceStandardsTests`).

### ب) تغيير عتبة (مثلاً حد الشواغر)
1. حدّد أي عتبة تقصد: `KpiThresholds` أم `RentalPolicy` أم المنهجية أم التقارير ([10](./10-gotchas-and-known-issues.md) §ج).
2. عدّل `appsettings*.json` (أو القيمة الافتراضية في الـ Options) و**الصفحة الثابتة** إن لزم.
3. أعد تشغيل الـ API. أعد توليد الـ golden عن قصد وراجع الفرق.

### ج) إضافة حقل إلى DTO الإيجار
1. عدّل النموذج في `Models/RentalControls/*`.
2. عبّئه في `Projections/*` (+ المصدر إن كان جديداً).
3. أضف الحقل إلى [models/rental-controls.ts](../../Front/src/app/models/rental-controls.ts) واعرضه في المكوّن.
4. حدّث اختبارات `…SerializationTests` والـ golden.
5. إن كان **شهرياً** فتأكد من سلوكه مع `PeriodValueMask` و`CappedRentalControlsStore`.

### د) مقياس جديد في تملك الأجانب
1. أضف الحقل في `DashboardDtos.cs`.
2. احسبه في `Projections/<X>Projection.cs` (استخدم `ShareWeighting` للأعداد).
3. اعرضه في الصفحة (وفكّر في إضافة نوع TypeScript بدل `any`).
4. حدّث `ForeignOwnershipGoldenTests` fixtures بعد مراجعة الفرق.

### هـ) صفحة جديدة في مؤشرات الإيجار
1. مكوّن standalone في `pages/rental-controls/<name>/` بنمط `PortalPeriodService.connect`.
2. مسار جديد في `app.routes.ts` مع `canActivate: [authGuard]`.
3. عنصر في `config/navigation.ts` (`PRODUCTS`).
4. دالة في `rental-controls.service.ts` + endpoint في `RentalControlsController` (مع `period` عبر `ViewAsync`).
5. غلّفها بـ `<app-layout [showGlobalFilter]="false" …>` واستعمل `app-period-filter` إن كانت تتبع الفترة.

### و) مدينة جديدة
نادر ومتعدد الأماكن: `RentalControlsMappings` (الـ id، رمز اسم الملف)، ملفات Excel، `city-reports/<id>.json`، اختبارات golden وfixtures لكل فترة. خطّط له كمهمة منفصلة.

### ز) نشر شهر جديد / إغلاق ربع
اتبع [08-data-and-operations.md](./08-data-and-operations.md) حرفياً.

## 6) مهام صغيرة آمنة للبداية

| المهمة | الهدف التعليمي |
|---|---|
| اكتب اختبار وحدة صغيراً لدالة في `Front/src/app/utils` (بعد حل مشكلة Node) | بنية الاختبارات في الواجهة |
| أضف اختباراً لحالة KPI حدّية في `CityAnalysisDeriver` (مثلاً زيادة = 7 بالضبط) | منطق العتبات |
| وثّق (بدون تعديل كود) كل مواضع عتبة الشواغر وأرسلها لمالك المنتج | تعدد المصادر |
| استبدل `any` بنوع في صفحة تملك الأجانب واحدة | الأنواع مقابل الـ DTO |
| اقرأ تقرير مدينة PDF مولَّداً وقارنه بصفحة المدينة | الاتساق بين اللوحة والتقرير |

## 7) أدوات تساعدك

- **Scalar** (`/scalar/v1`) لتجربة الـ endpoints (يلزم Authorize بالـ Bearer).
- **DevTools → Network** لرؤية `period` والمعاملات والاستجابة الخام.
- **تعيين breakpoint** في `RentalControlsPeriodService.ResolveCoreAsync` لفهم الفترات.
- **`git log -p -- Back/AqarInsights.Api/Data/rental-controls/tables/live`** لرؤية تاريخ تغييرات البيانات.
- **grep** على النصوص العربية: الأسرع لإيجاد مكان نص في الواجهة أو الـ API.
