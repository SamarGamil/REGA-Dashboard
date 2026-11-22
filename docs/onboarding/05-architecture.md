# 05 — المعمارية العامة (Architecture)

## 1) المخطط العام

```
┌────────────┐   HTTPS + JWT    ┌───────────────────────────┐        ┌─────────────────────────────┐
│  المتصفح   │ ───────────────► │  ASP.NET Core API (.NET10) │ ─────► │  ملفات على القرص (Data/)     │
│ Angular 19 │ ◄─────────────── │  Controllers → Services    │ ◄───── │  JSON / XLSX / users / logs  │
│  (SPA)     │      JSON/SSE    │  Singletons في الذاكرة      │        └─────────────────────────────┘
└─────┬──────┘                  └──────────┬────────────────┘
      │ (قبل الإقلاع)                       │ (اختياري)
      ▼                                     ▼
 config.json                         LLM خارجي متوافق مع OpenAI
 (apiUrl, showDataAssistant)         (مساعد «تحدث مع البيانات»)
```

- **لا قاعدة بيانات.** عند الإقلاع يحمّل الـ API الملفات في الذاكرة ويحسب النتائج الجاهزة.
- **مستودع واحد (mono-repo)** فيه المجلدان: `Back/` و`Front/`، وتُنشران كحاويتين/خدمتين منفصلتين.

## 2) التقنيات

| الطبقة | التقنية | ملاحظة |
|---|---|---|
| Frontend | Angular 19 (standalone components), TypeScript 5.7, RxJS 7.8 | لا يوجد NgModule؛ المكونات standalone |
| التنسيق | Tailwind CSS 3, خط Tajawal, أيقونات Remix Icon | `index.html` بـ `lang="ar" dir="rtl"`؛ الخط والأيقونات من CDN |
| الرسوم | ECharts 5 عبر `ngx-echarts` | ثيم وRTL في [chart-theme.ts](../../Front/src/app/utils/chart-theme.ts) |
| التصدير | `write-excel-file` | في [export.service.ts](../../Front/src/app/services/export.service.ts) |
| Backend | ASP.NET Core على `net10.0` | Controllers (وليس Minimal APIs) |
| Excel | ClosedXML, DocumentFormat.OpenXml | قراءة وكتابة ملفات xlsx/pptx/docx |
| PDF | QuestPDF (رخصة Community) | خطوط Tajawal مُضمَّنة كـ embedded resources |
| Word | DocumentFormat.OpenXml | `*WordRenderer.cs` |
| الأمان | JWT Bearer (HS256), BCrypt.Net-Next | |
| السجلات | Serilog | نص في التطوير، JSON مضغوط خارجه + ملفات يومية |
| الوثائق | OpenAPI + Scalar | في بيئة Development فقط |
| الاختبار | xUnit 2.9 (Back), `node --test` (Front/tests) | انظر 09 |

> `@ngx-translate/*` موجودة في `package.json` لكن لم نجد لها استخداماً في الكود: كل النصوص عربية مكتوبة مباشرة. وأيضاً README الواجهة تذكر ExcelJS وjsPDF بينما المستخدم فعلياً `write-excel-file` (انظر 10).

## 3) تدفق طلب نموذجي (صفحة «حالة المدن»)

1. المتصفح يفتح `/rental-indicators/city-status` → `authGuard` يتحقق من وجود JWT غير منتهٍ (من `localStorage`).
2. الصفحة تستخدم `PortalPeriodService.connect(...)` فتطلب `GET /api/rental-controls/overview` (+ `cities/riyadh`) مع `Authorization: Bearer …` (يضيفه الـ interceptor).
3. الـ API: `JwtBearer` يتحقق من التوقيع/الصلاحية ثم من أن الـ `jti` غير مُلغى → `RentalControlsController.GetOverview`.
4. الـ controller يطلب من `IRentalControlsPeriodService.ResolveAsync(period)` عرض الفترة، ثم يقرأ `Aggregation.GetOverview()` (نتيجة محسوبة مسبقاً).
5. الاستجابة JSON تُعرض في بطاقات المدن.

تفصيل أعمق في [11-how-to-trace-a-feature.md](./11-how-to-trace-a-feature.md).

## 4) حالة التطبيق (State) في الـ Backend

كل شيء **Singleton في الذاكرة**:

| المكوّن | ماذا يحمل | متى يتغير |
|---|---|---|
| `DataLoaderService` | صفقات تملك الأجانب | عند الإقلاع أو بعد `POST /api/import` |
| `AggregationService` (FO) | Snapshot غير قابل للتعديل لكل endpoint | `Recompute()` |
| `FilterOptionsService` | شجرة الفلاتر | عند الإقلاع أو بعد الاستيراد |
| `TableRentalControlsStore` | البيانات الحية للإيجار | عند الإقلاع فقط |
| `RentalControlsAggregationService` | Snapshot لعروض الإيجار | `Recompute()` عند الإقلاع |
| `RentalControlsPeriodService` | كاش لعروض الإصدارات (`ConcurrentDictionary`) | يُبنى عند أول استخدام ويُبطَل بتغير الإصدار |
| `JsonUserStore` | المستخدمون | **مرة واحدة عند الإقلاع** |
| `InMemoryTokenRevocationStore` | الـ jti الملغاة | **يضيع عند إعادة التشغيل** |
| `JsonReportHistoryStore` | سجل التقارير | ملف JSON على القرص |

**النتائج العملية:**
- أي تغيير على بيانات الإيجار أو المستخدمين يتطلب **إعادة تشغيل** الـ API.
- التشغيل على أكثر من نسخة (scale-out) غير مدعوم دون معالجة: كل نسخة لها ذاكرتها وملفاتها (وإلغاء الـ JWT لا يُشارَك بين النسخ).
- نمط النشر الآمن للقراءة: `volatile Snapshot` يُستبدل بمرجع واحد بعد اكتمال البناء (`_snapshot = new Snapshot(...)`) فلا يرى القارئ حالة نصف مبنية.

## 4-ب) ترتيب الإقلاع في [Program.cs](../../Back/AqarInsights.Api/Program.cs)

1. Serilog (bootstrap ثم الإعداد الكامل).
2. `AddControllers`, `ProblemDetails`, `OpenApi`.
3. CORS من `Cors:AllowedOrigins` (**في غير Development يرمي استثناء إن لم تُضبط**).
4. المصادقة: `AuthSettings`, `JsonUserStore`, `AuthService`, `JwtBearer` (يرمي استثناء إن كان `JwtSecret` فارغاً).
5. `FallbackPolicy = RequireAuthenticatedUser` (أي endpoint بلا `[AllowAnonymous]` يتطلب تسجيل دخول).
6. Rate limiter لسياسة `login`: 50 طلباً/دقيقة لكل IP.
7. `ForwardedHeaders` (X-Forwarded-For/Proto) مع مسح `KnownProxies`.
8. تسجيل خدمات المجالات الأربعة: `AddForeignOwnershipServices`, `AddRentalControlsServices`, `AddReportingServices`, `AddAssistantServices` (في [Extensions/](../../Back/AqarInsights.Api/Extensions/)).
9. تسجيل خطوط QuestPDF من الموارد المضمّنة (يرمي استثناء إن غابت).
10. `app.Build()` ثم **أوامر CLI** (`--import`, `--import-controls`, … كل منها ينفّذ ويخرج).
11. تحميل البيانات: FO loader → rental store → rental `Recompute()` → FO `Recompute()` → filter options.
12. Middleware: ForwardedHeaders → RequestLogging → ExceptionHandler → Security headers → CORS → (Scalar في Dev) → Authentication → Authorization → RateLimiter.
13. `GET /v1/api/health-check` (مجهول) ثم `MapControllers()`.

## 5) الأمان

| الجانب | التطبيق |
|---|---|
| المصادقة | JWT موقّع HS256 بمفتاح `AuthSettings:JwtSecret`، صلاحية `JwtExpiryHours` (24 افتراضياً). بدون التحقق من issuer/audience |
| تسجيل الخروج | `POST /api/auth/logout` يلغي `jti` في الذاكرة |
| كلمات المرور | BCrypt؛ يُنفَّذ verify دائماً (حتى لو لم يوجد المستخدم) لتوحيد الزمن |
| التفويض | `FallbackPolicy` يطلب مستخدماً موثَّقاً؛ **لا توجد أدوار (roles)** |
| الحد من التخمين | 50 طلب/دقيقة/IP على `login` |
| CORS | قائمة `Cors:AllowedOrigins`؛ يُكشف `Content-Disposition` للواجهة |
| ترويسات الأمان | `nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy`, `Permissions-Policy`; وخارج Development: CSP صارمة + HSTS |
| الواجهة | الرمز في `localStorage` (مفتاح `aqar_token`)، و`authGuard` يفحص `exp` محلياً، والـ interceptor يعمل logout عند أي 401 ما عدا login/logout |

انظر ملاحظات المخاطر في [10-gotchas-and-known-issues.md](./10-gotchas-and-known-issues.md).

## 5-ب) الإعدادات (Configuration)

- الترتيب القياسي لـ ASP.NET: `appsettings.json` ← `appsettings.{Environment}.json` ← متغيرات البيئة ← (user-secrets في Development).
- الـ `:` تصبح `__` في متغيرات البيئة: `AuthSettings__JwtSecret`, `Cors__AllowedOrigins__0`.
- أهم المفاتيح:

| المفتاح | الوظيفة |
|---|---|
| `AuthSettings:JwtSecret / JwtExpiryHours / UsersFile` | التوقيع والصلاحية وملف المستخدمين |
| `Cors:AllowedOrigins` | الواجهات المسموحة |
| `DataSettings:*` | `InputDir`, `OutputDir`, `TransactionsFile`, `FilterOptionsFile` (تملك الأجانب) |
| `RentalControlsDataSettings:*` | `SourceDir`, `OutputDir` (الإيجار) |
| `KpiThresholds`, `RentalPolicy`, `MarketAnalysis:Thresholds` | عتبات العرض (انظر 02) |
| `Reports:HistoryFile`, `Reports:NationalReport` | سجل التقارير وعتباتها |
| `Logging:File:Directory` | مجلد سجلات الملفات |
| `Assistant:*` | `BaseUrl`, `Model`, `ApiKey`, `Temperature`, `MaxTokens`, `DisableThinking` |

- مسارات البيانات تُحلّ نسبةً إلى **ContentRoot** في [DataPaths.cs](../../Back/AqarInsights.Api/Extensions/DataPaths.cs).
- **الواجهة:** تقرأ [config.json](../../Front/public/config.json) في وقت التشغيل (قبل Bootstrap) لضبط `apiUrl` و`showDataAssistant` **بدون إعادة بناء**. في التطوير تُستخدم `environment.development.ts` (`http://localhost:5032/api`).

## 6) السجلات والمراقبة

- سطر منظّم لكل طلب (Serilog request logging)؛ مسار الـ health-check يُخفَّض إلى Verbose.
- خارج Development: JSON مضغوط على stdout + ملفات يومية `log-.json` (احتفاظ 14 ملفاً، حتى 50MB للملف) في `Logging:File:Directory`.
- `GET /v1/api/health-check` يعيد `{status, version, build}`.

## 7) النشر (كما هو موثّق)

- CI: `Back/pipeline-ci.yaml` و`Front/pipeline-ci.yaml` (بيانات وصفية لخطوط Elm: Nexus IQ / ACS / Coverity حسب الـ README).
- Helm من مستودع `ois-helm-releases` (charts: `rega-dashboard` و`rega-dashboard-fe`) كما تذكر الـ README؛ المتغيرات `AuthSettings__*` و`Cors__*` تأتي من `envVars` في الـ chart.
- الواجهة: build ثم تقديم ملفات static عبر nginx (منفذ 8080 حسب README) مع `try_files … /index.html`.
- هناك أيضاً `web.config` وقيم مسارات Windows (`C:\OIS\Dashboard\Backend\…`) في الإعدادات، ما يدل على نمط استضافة Windows/IIS في بعض البيئات.

> **يحتاج تأكيد:** نمط النشر الفعلي في كل بيئة (Kubernetes أم IIS أم الاثنان) وكيف تُحفَظ ملفات `Data/` بين النشرات. اسأل فريق DevOps.
