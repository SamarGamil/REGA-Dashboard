# 07 — دليل الـ Frontend

المسار: [Front/](../../Front/) — Angular 19، مكوّنات standalone، TypeScript بوضع `strict`، Tailwind، ECharts.

## 1) خريطة المجلدات

```
Front/
├─ public/config.json            إعدادات وقت التشغيل: apiUrl, showDataAssistant
├─ src/
│  ├─ main.ts                    يجلب config.json ثم يقلع التطبيق
│  ├─ index.html                 lang="ar" dir="rtl" + خط Tajawal + Remix Icon (CDN)
│  ├─ environments/              environment.ts (إنتاج، يقرأ config.json) / environment.development.ts
│  └─ app/
│     ├─ app.routes.ts           المسارات (lazy-loaded) + authGuard
│     ├─ app.config.ts           provideRouter, provideHttpClient(authInterceptor), ECharts
│     ├─ config/navigation.ts    تعريف المنتجين والقوائم الجانبية
│     ├─ guards/ interceptors/   authGuard, authInterceptor
│     ├─ services/               13 خدمة (API، حالة، تصدير…)
│     ├─ models/                 rental-controls.ts, reports.ts (أنواع TypeScript)
│     ├─ utils/                  chart-theme, format-number, period-options, status-recommendations…
│     ├─ components/
│     │   ├─ base/               بطاقات عامة (kpi-card, chart-card, chart-empty-state…)
│     │   └─ feature/            layout, sidebar, top-filter-bar, period-filter, data-assistant…
│     ├─ pages/                  صفحات تملك الأجانب (8) + rental-controls/ (6 صفحات بمكوّنات فرعية)
│     └─ mocks/dashboard.ts      بيانات تجريبية (لا تستخدمها صفحات المنتج)
├─ tests/*.test.ts               اختبارات وحدة للـ utils (node --test)
└─ scripts/generate-version.mjs  يولّد version.ts قبل البناء (prebuild)
```

- **الـ alias:** `@/*` ← `src/app/*` (في [tsconfig.json](../../Front/tsconfig.json)). تجد `import … from '@/services/…'` في صفحات الإيجار، بينما الصفحات الأقدم تستخدم مسارات نسبية.

## 2) الإقلاع والإعدادات

1. [main.ts](../../Front/src/main.ts) يجلب `config.json` (بدون كاش) ويضعه في `window.__APP_CONFIG__`، ثم يستدعي `bootstrapApplication`.
2. [environment.ts](../../Front/src/environments/environment.ts) (الإنتاج) يقرأ `apiUrl` و`showDataAssistant` من هذا الكائن عند الاستخدام (`get apiUrl()`)، وإلا يعود لقيمة افتراضية (`https://localhost:9060/api`).
3. أثناء `npm start` تُستبدل بملف `environment.development.ts` (`http://localhost:5032/api`).

> الفائدة: يمكن تغيير عنوان الـ API دون إعادة بناء الصورة. **لا تضع أسراراً في `config.json`** (يُجلب علناً).

## 3) المسارات والمنتجات

في [app.routes.ts](../../Front/src/app/app.routes.ts):

- `/login` — بلا حارس.
- `/` → يعيد التوجيه إلى `rental-indicators/city-status`.
- صفحات تملك الأجانب تُولَّد من مصفوفة `foPages` تحت `foreign-ownership/<page>`، وكل مسار قديم بلا بادئة (`/executive-summary`…) يعيد التوجيه إليها.
- صفحات الإيجار تحت `rental-indicators/{city-status, indicators, city-analysis, market-analysis, reports, methodology}`.
- `/methodology` يعيد التوجيه إلى `rental-indicators/methodology`.
- `**` → صفحة 404.
- كل الصفحات (عدا login و404) تحت `canActivate: [authGuard]`.

**ملاحظة تسمية:** المسار في الواجهة `rental-indicators`، بينما الـ API `rental-controls`، ومجلد الصفحات `pages/rental-controls`.

**المنتج النشط** يُستنتج من المسار: [navigation.ts](../../Front/src/app/config/navigation.ts) `getActiveProductId` (إن بدأ بـ `/rental-indicators` فهو الإيجار، وإلا تملك الأجانب)، و[ProductService](../../Front/src/app/services/product.service.ts) يوفره كـ signals للقائمة الجانبية.

## 4) الهيكل المشترك: `LayoutComponent`

كل صفحة تلتف بـ `<app-layout …>` ([layout.component.ts](../../Front/src/app/components/feature/layout/layout.component.ts)) الذي يضم: الشريط الجانبي، شريط الفلاتر العلوي، ترويسة الصفحة (عنوان/فتات خبز/فترة التقرير)، تذييل، والمساعد.

مدخلات مهمة: `title`, `subtitle`, `breadcrumbs`, `showGlobalFilter` (صفحات الإيجار تخفيه لأن لها فلتر فترة خاصاً), `showPageHeader`, `portalPeriod`.

## 5) إدارة الحالة: نمطان مختلفان

### أ) تملك الأجانب — نمط أقدم (فلاتر عامة + Subject)

- [FilterStateService](../../Front/src/app/services/filter-state.service.ts) يحمل الاختيارات (region, city, district, nationalities, transactionType, ownerType, propertyType, period) كحقول عادية، ويوفر `filterChanged$` (Subject) و`toQueryParams()`.
- كل صفحة: `ngOnInit` → `loadData()` + اشتراك في `filterChanged$` لإعادة التحميل، و`ngOnDestroy` يلغي الاشتراك.
- الاستجابات من [DashboardApiService](../../Front/src/app/services/dashboard-api.service.ts) مكتوبة `any` (لا أنواع).
- [TopFilterBarComponent](../../Front/src/app/components/feature/top-filter-bar/top-filter-bar.component.ts) يحمّل `/api/filters` ويعدّل الحالة (تغيير المنطقة يصفّر المدينة والحي، وتغيير المدينة يصفّر الحي).

### ب) مؤشرات الإيجار — نمط أحدث (Signals + خدمة الفترة المشتركة)

- [PortalPeriodService](../../Front/src/app/services/portal-period.service.ts): signals للفترة المختارة وقائمة الفترات؛ **في الذاكرة فقط** (تضيع عند إعادة تحميل الصفحة) ومشتركة بين الصفحات.
- `connect(load, reload$?)`: يشغّل `load(period)` عند تغيّر الاختيار (ويلغي الطلب الأقدم بـ `switchMap`)، وعند الفشل يعيد الاختيار لآخر فترة نجحت.
- الصفحات: `portal.connect(period => api.getX(period)).pipe(takeUntilDestroyed()).subscribe(...)` ثم `signal`/`computed` للعرض.
- الأنواع في [models/rental-controls.ts](../../Front/src/app/models/rental-controls.ts) و[RentalControlsService](../../Front/src/app/services/rental-controls.service.ts).
- منتقي الفترة: [PeriodFilterComponent](../../Front/src/app/components/feature/period-filter/period-filter.component.ts) مع منطق الاختيارات في [period-options.ts](../../Front/src/app/utils/period-options.ts) (سنة ← ربع ← شهر؛ الأشهر/الأرباع بلا بيانات تُعطَّل؛ الافتراضي أحدث بيانات).

> عند إضافة صفحة جديدة للإيجار قلّد النمط (ب). وللصفحات القديمة لا تعد كتابتها إلا ضمن مهمة واضحة.

## 6) الخدمات (`services/`)

| الخدمة | الوظيفة |
|---|---|
| `auth.service.ts` | login/logout، تخزين JWT في `localStorage` (`aqar_token`)، `isAuthenticated()` يفحص `exp` |
| `dashboard-api.service.ts` | استدعاءات تملك الأجانب |
| `rental-controls.service.ts` | استدعاءات الإيجار (كلها `period?`) عدا market-analysis |
| `reports.service.ts` | الفترات، السجل، توليد التقارير كـ blob + قراءة اسم الملف من `Content-Disposition` |
| `reporting-period.service.ts` | «الفترة الحالية/آخر تحديث» لكل منتج (يُجلب مرة ويُخبَّأ) |
| `portal-period.service.ts` | الفترة المختارة في صفحات الإيجار |
| `filter-state.service.ts` | فلاتر تملك الأجانب |
| `product.service.ts` / `sidebar.service.ts` | المنتج النشط وحالة الشريط الجانبي |
| `export.service.ts` | تصدير Excel (`write-excel-file`) |
| `assistant*.ts`, `chat-message.ts` | المساعد وبث الردود |

## 7) المصادقة في الواجهة

- [authGuard](../../Front/src/app/guards/auth.guard.ts): إن لم يوجد رمز صالح → `/login`.
- [authInterceptor](../../Front/src/app/interceptors/auth.interceptor.ts): يضيف `Authorization: Bearer`، وعند أي `401` (غير login/logout) ينفّذ `logout()`.
- `logout()` يُلغي الرمز في الخادم (بأفضل جهد) ثم يمسح التخزين المحلي ويحوّل إلى `/login`.

## 8) الرسوم البيانية، الاتجاه RTL، التنسيق

- ECharts عبر `NgxEchartsDirective`؛ كل رسم يبني `EChartsOption`.
- [chart-theme.ts](../../Front/src/app/utils/chart-theme.ts): `CHART_COLORS`, `baseChartTheme()` (خط Tajawal)، و**`applyRtl(option)`** الذي يعكس المحاور والـ borderRadius لتناسب RTL. استخدمه مع كل رسم جديد.
- [format-number.ts](../../Front/src/app/utils/format-number.ts): تنسيق الأعداد والريال والنسب (النسبة الفارغة تُعرض `—`).
- الأرقام داخل نص عربي تُلفّ بـ `<span dir="ltr">` (مثال صفحة المنهجية) كي لا ينعكس ترتيبها.
- Tailwind مع ألوان مكتوبة مباشرة كثيراً (`#0F2A44`، `#0E8C79`…)؛ لا يوجد نظام design tokens مركزي بعد.

## 9) مكوّنات صفحات الإيجار (نظرة سريعة)

| الصفحة | مكوّنات فرعية رئيسية |
|---|---|
| city-status | filter-bar, national-summary-cards, operational-stages-bar, classification-legend, city-card-grid/city-card, impact-panel/impact-line-chart, recommendation-panel, summary-collapsible |
| indicators | indicator-tabs/indicator-panel, historical-heatmap/historical-comparison, supply-demand-gap (+`gap-chart-options.ts`, `gap-insights.utils.ts`), micro-summary, legend, executive-insight |
| city-analysis | header, executive-summary, kpi-cards, rental-indicator-charts, market-movement, market-analysis-tabs, media-monitoring (+modal), executive-recommendations, what-to-monitor, city-journey |
| market-analysis | indicator-explorer, relationship-analysis, executive-recommendations |
| reports | توليد التقارير والسجل |

## 10) الاختبارات

- `Front/tests/*.test.ts`: اختبارات لدوال `utils` فقط (chart-theme, gap-chart-options, gap-insights, impact-timeline, indicator-labels, input-history, pagination, period-options, status-recommendations).
- الأمر `npm run test:unit` (= `node --test "tests/**/*.test.ts"`). ملفات الاختبار تستورد ملفات `.ts` مباشرةً (مثل `../src/app/utils/pagination.ts`) فتحتاج Node يدعم تشغيل TypeScript (type stripping) وتوسيع الـ glob. **على Node 20.18 فشل الأمر عندنا** بالرسالة `Could not find '…\tests\**\*.test.ts'` — انظر [09-local-setup-and-testing.md](./09-local-setup-and-testing.md).
- لا توجد اختبارات للمكوّنات/الصفحات. `npm test` (= `ng test`) **لن يعمل**: [angular.json](../../Front/angular.json) يعرّف target `build` و`serve` فقط (تحققنا). وكذلك `Front/README.md` الذي يذكر Karma/Jasmine قديم.

## 11) أماكن بيانات «مصطنعة» (Fabricated) في الواجهة

مُعلَّمة في الكود بكلمة `FABRICATED` لأنها **ليست من الـ API**:

| الملف | ماذا |
|---|---|
| [city-status.component.ts](../../Front/src/app/pages/rental-controls/city-status/city-status.component.ts) | نسبة نمو الرياض «قبل التفعيل» (`15.7%`) وقيمة ربع سابق (`9.7%` لـ 2026-Q3) ثابتتان |
| [impact-timeline.ts](../../Front/src/app/utils/impact-timeline.ts) | الأرباع الوسيطة تُولَّد حسابياً من قيمتين |
| [impact-panel.component.ts](../../Front/src/app/pages/rental-controls/city-status/components/impact-panel.component.ts) | نقاط رؤى مكتوبة يدوياً |

تعامل معها كعرض توضيحي حتى يتوفر مصدر بيانات حقيقي.
