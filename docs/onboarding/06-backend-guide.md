# 06 — دليل الـ Backend

المسار: [Back/AqarInsights.Api/](../../Back/AqarInsights.Api/) — حل `AqarInsights.Api.sln` فيه مشروعان: الـ API والاختبارات. (يوجد أيضاً أداة جانبية `Back/tools/mm-extract` لاستخراج بيانات Excel؛ ليست جزءاً من الـ runtime.)

## 1) خريطة المجلدات

```
AqarInsights.Api/
├─ Program.cs                      نقطة الدخول: الإعداد، أوامر CLI، تحميل البيانات، الـ middleware
├─ Controllers/                    7 controllers (طبقة HTTP رقيقة)
├─ Extensions/                     تسجيل الخدمات لكل مجال + DataPaths (مسارات الملفات)
├─ Models/                         DTOs + إعدادات (Options) + كائنات خام (Raw) + صفوف جداول
│   ├─ ForeignOwnership/
│   ├─ RentalControls/ (Raw/, Tables/, Reports/, Snapshots/)
│   ├─ Assistant/
│   └─ Auth*, UserAccount, ConversionReport…
├─ Services/
│   ├─ ForeignOwnership/           استيراد، تحميل، تجميع (Projections/)، خيارات الفلاتر
│   ├─ RentalControls/             استيراد، قراءة workbook، Store، Projections، Reports، Snapshots، Tables، MonthlyUpdate
│   ├─ Assistant/                  المساعد (سياق + استدعاء LLM + بث)
│   └─ Auth*, JsonUserStore, InMemoryTokenRevocationStore, QuarterLabel, JsonDefaults
├─ Utilities/                      ArabicNormalizer, NationalityNormalizer
├─ Tools/                          DataNormalizationScript (--normalize-data)
├─ Resources/Fonts/                Tajawal (مضمّنة في الـ assembly لأجل PDF)
└─ Data/                           ملفات البيانات (انظر 08)
```

## 2) الـ Controllers والـ API

كلها `[Authorize]` ما عدا `login` والـ health-check. المسارات تبدأ بـ `/api`.

| Controller | الـ endpoints | ملاحظات |
|---|---|---|
| [AuthController](../../Back/AqarInsights.Api/Controllers/AuthController.cs) | `POST auth/login` (مجهول، rate-limited) · `POST auth/logout` | `401 ProblemDetails` عند فشل الدخول؛ logout يعيد 204 |
| [DashboardController](../../Back/AqarInsights.Api/Controllers/DashboardController.cs) | `GET dashboard/{summary,regions,nationalities,properties,areas,financial,strategic}` · `GET dashboard/reporting-period` | يقبل `DashboardFilterParams` من query |
| [FiltersController](../../Back/AqarInsights.Api/Controllers/FiltersController.cs) | `GET filters` | شجرة الجغرافيا والقوائم |
| [ImportController](../../Back/AqarInsights.Api/Controllers/ImportController.cs) | `POST import` (xlsx حتى 30MB) · `GET import/inspect` | `422` إن فشل التحويل. **أي مستخدم موثّق يستطيع استدعاؤه** |
| [RentalControlsController](../../Back/AqarInsights.Api/Controllers/RentalControlsController.cs) | `GET rental-controls/{overview,indicators,cities,cities/{cityId}}?period=` · `GET market-analysis` · `GET reporting-period` | `404 ProblemDetails` لفترة غير معروفة؛ `market-analysis` و`reporting-period` بلا `period` |
| [ReportsController](../../Back/AqarInsights.Api/Controllers/ReportsController.cs) | `GET reports/periods` · `POST reports/national?format=&record=&period=` · `POST reports/city/{id}?…` · `GET reports/history` | `format=pdf\|word\|docx`؛ يعيد ملفاً |
| [AssistantController](../../Back/AqarInsights.Api/Controllers/AssistantController.cs) | `GET assistant/status` · `POST assistant/ask` (SSE) | `503` إن لم يُضبط المحرك |
| (في Program.cs) | `GET /v1/api/health-check` | مجهول؛ مسار لا يبدأ بـ `/api` |

اتفاقيات عامة:
- الأخطاء بصيغة `ProblemDetails`.
- `period` اختياري: `yyyy-MM` أو `yyyy-Qn`؛ غيابه = ربع live الحالي.
- مفاتيح JSON camelCase كما تستهلكها الواجهة (انظر [models/rental-controls.ts](../../Front/src/app/models/rental-controls.ts) كمرآة لـ DTOs الإيجار).
- Scalar UI على `/scalar/v1` في Development فقط.

## 3) نمط المجال: Store → Aggregation → Projections → Controller

### تملك الأجانب

```
transactions.json ──► DataLoaderService ──► AggregationService ──► SummaryProjection, RegionsProjection, …
                                             │  Recompute(): Snapshot (آخر 6 فترات، شهري، بلا فلاتر)
                                             └─ GetXxx(filters): snapshot أو إعادة حساب عند وجود فلاتر/فترة مختلفة
```
- `Periods`: `monthly | quarterly | yearly`.
- `PeriodMath`, `ShareWeighting`, `ValueBuckets` أدوات مشتركة بين الـ Projections.

### مؤشرات الإيجار

```
tables/<release>/*.json ──► JsonTablesRepository (IRentalControlsRepository)
        │                            │ ReleaseMapper (صفوف → نموذج في الذاكرة)
        ▼                            ▼
 TableRentalControlsStore (IRentalControlsStore + ICityReportContentStore)
        │
        ▼
 RentalControlsAggregationService.Recompute() ──► OverviewProjection, IndicatorsProjection,
        │                                         CityProjection, MarketAnalysisProjection
        ▼
 RentalControlsPeriodService.ResolveAsync(period) ──► (Aggregation, Content, PeriodScope) للفترة المطلوبة
```

أدوار الأجزاء:

| الجزء | المسؤولية |
|---|---|
| `IRentalControlsRepository` / `JsonTablesRepository` | قراءة وكتابة أقسام الجداول (partitions) والـ monthly_movement والـ corrections. هدف التصميم: يمكن استبداله بقاعدة بيانات دون لمس بقية الكود |
| `ReleaseMapper` / `MappedRelease` | تحويل صفوف التخزين (بنمط جداول) إلى نموذج الذاكرة |
| `TableRentalControlsStore` | يخدم البيانات الحية (يُحمَّل عند الإقلاع)، وهو أيضاً مصدر نصوص التقارير |
| `RentalControlsAggregationService` | يبني Snapshot كاملاً: overview + indicators + مدن + تحليل سوق |
| **`RentalControlsPeriodService`** | **قلب منطق الفترات**: يحدد المصدر (live أو إصدار)، يبني عرض شهر عبر `CappedRentalControlsStore`، يخبّئ عروض الإصدارات، ويحمّل الربع/الشهر السابق للاتجاهات |
| `PeriodScope` / `PeriodValueMask` | تطبيق قواعد الفترة على القيم والنصوص |
| `Projections/*` | دوال static تحسب DTO معيّناً (أسهل جزء للاختبار) |
| `Reading/*` | قراءة Excel بالتفصيل (`CityReader`, `OverviewReader`, `VacancyReader`, `MediaMonitoringReader`, `SupplyDemandGapReader`, `SheetReader`) |
| `RentalControlsConverterService` | منسّق الاستيراد: قراءة الملفات، فحوصات `ImportChecks`، كتابة الإصدار عبر `ReleaseWriter`، وتجميد الربع السابق تلقائياً |
| `MonthlyUpdate/*` | تعديل ملفات المصدر من مجلد التحديث الشهري (انظر 08) |
| `Reports/*` | نماذج التقارير والـ renderers (PDF/Word) والمدقق وسجل التاريخ |

### كيف تُحلّ الفترة؟ (`RentalControlsPeriodService.ResolveCoreAsync`)

| الطلب | النتيجة |
|---|---|
| فارغ | عرض ربع live الحالي (أو الحي مباشرةً إن لم توجد أرباع) |
| `yyyy-MM` | يجب أن يكون الشهر متاحاً؛ يؤخذ مصدر ربعه (live أو إصدار) ويُقصّ عند الشهر (`capAt`). شهر بعد أحدث ربع → من بياناته الخاصة كما هي |
| `yyyy-Qn` | الربع الحالي → live، الربع المُغلق → إصدار، ويُقصّ عند آخر شهر في الربع |
| غير ذلك/غير متاح | `null` → الـ controller يعيد 404 |

وكل نتيجة تحمل `PreviousQuarter` و`PreviousMonth` لأسهم الاتجاه في التقارير.

## 4) أنماط تصميم متكررة

1. **Snapshot غير قابل للتعديل + `volatile`:** يُبنى كاملاً ثم يُنشر بتبديل مرجع واحد.
2. **Projections كدوال static** تستقبل بيانات وتعيد DTO — لا حالة داخلية.
3. **Options pattern** (`IOptions<T>`) للعتبات: `KpiThresholds`, `RentalPolicy`, `MarketAnalysis`, `NationalReportOptions`, `AssistantSettings`.
4. **المسارات مركزية في `DataPaths`**: لا تكتب مسار ملف في مكان آخر.
5. **الفصل بين المجالات:** كل مجال له `Add…Services` في Extensions.
6. **ثوابت اللغة العربية داخل الكود** (أسماء الحالات والمؤشرات): بعض المنطق يعتمد على **نصوص عربية حرفية** (مثل `"الضوابط مفعلة"`). تغيير نص قد يكسر الفلاتر أو العدّ.
7. **تطبيع النص العربي** (`ArabicNormalizer`) عند مطابقة الأسماء بين الملفات.

## 5) المساعد (Assistant)

- `AssistantContextBuilder` يبني سياقاً ونص نظام من بيانات اللوحة للغة/المنتج/الفترة المطلوبة.
- `AssistantService` يرسل إلى endpoint متوافق مع OpenAI (`Assistant:BaseUrl`)، وآخر 6 رسائل فقط (كل رسالة حتى 4000 حرف)، ويبث الرد.
- الـ controller يخرج SSE: `{delta}` ثم `{done:true}` أو `{error}`.
- يُعطَّل تلقائياً إن لم يُضبط المحرك (503).

## 6) التقارير

- `NationalReportService` و`CityReportService` (Scoped) → `*ModelBuilder` → `*PdfRenderer` / `*WordRenderer`.
- النص العربي في PDF يمر عبر `PdfBidi` لضبط اتجاه الأرقام واللاتيني داخل RTL، وخط Tajawal مضمّن.
- `ReportTerminology`/`ReportThresholds`/`ReportFrequency`/`ReportPeriodText`: توحيد المصطلحات والحدود وتسميات الدورية.
- `NarrativeAuditor`: يفحص ادعاءات النص مقابل الأرقام.
- كل طلب `record=true` يُضاف إلى `report-history.json` (الاسم/النوع/النطاق/الفترة/الصيغة/المستخدم/الوقت).

## 7) الاختبارات (xUnit) — ماذا يغطي ماذا

مجلد [AqarInsights.Api.Tests](../../Back/AqarInsights.Api.Tests/):

| المنطقة | أمثلة |
|---|---|
| Auth | `AuthServiceTests`, `AuthControllerTests`, `JsonUserStoreTests`, `InMemoryTokenRevocationStoreTests` |
| FO | `AggregationServiceTests`, `ForeignOwnershipGoldenTests`, `ShareWeightedCountsTests`, `ExcelConverterService*Tests`, `FilterOptionsServiceTests`, `ImportControllerTests` |
| تطبيع | `ArabicNormalizerTests`, `NationalityNormalizerTests`, `PropertyCategoriesTests`, `GeographyFileTests` |
| الإيجار | `RentalControls/*`: Projections, Readers, Serialization, `GoldenEquivalenceTests`, `RentalControls*EndpointTests`, `PeriodValueMaskTests`… |
| الإيجار (فرعية) | `RentalControls/Snapshots` (الفترات)، `RentalControls/Tables` (التخزين والإصدارات)، `RentalControls/Reports`، `RentalControls/MonthlyUpdate` |
| المساعد | `Assistant/*` |

**Golden tests:** تقارن المخرجات بملفات JSON مرجعية في `Fixtures/`. إن غيّرت سلوكاً عن قصد فأعد توليدها وراجع الـ diff بعناية:
- الإيجار الحي: `RC_GOLDEN_REGEN=1`
- فترات الإيجار: `RC_PERIOD_GOLDEN_WRITE=1`
- تملك الأجانب: `FO_GOLDEN_REGEN=1`
(من [Back/README.md](../../Back/README.md)). لا تفعل ذلك لتجعل اختباراً فاشلاً ينجح دون فهم السبب.

## 8) أماكن تعديل شائعة (جدول سريع)

| أريد… | عدّل |
|---|---|
| عتبة حالة KPI | `appsettings*.json` → `KpiThresholds` أو `RentalPolicy` (ثم أعد التشغيل) |
| نص حالة/تصنيف | `RentalControlsMappings.cs` (وانتبه لمطابقات النص) |
| حقل جديد في بطاقة مدينة | `Models/RentalControls/*` + `Projections/*` + `Front/src/app/models/rental-controls.ts` + المكوّن |
| مقياس جديد لتملك الأجانب | `DashboardDtos.cs` + الـ Projection المناسب + اختبار golden |
| endpoint جديد | Controller جديد/method + تسجيل خدمة في Extensions إن لزم + اختبار |
| قراءة عمود Excel جديد | `Reading/*` أو `ExcelConverterService` + اختبار القراءة |
