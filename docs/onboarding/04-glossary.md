# 04 — قاموس المصطلحات (Glossary)

## مصطلحات الأعمال — عام

| المصطلح | بالإنجليزية / في الكود | المعنى |
|---|---|---|
| الهيئة العامة للعقار | REGA | الجهة المالكة للمنتج |
| عقار إنسايتس | AqarInsights | الاسم الداخلي للحل (namespaces وsolution) |
| المنتج | Product (`rental-indicators` / `foreign-ownership`) | أحد القسمين الرئيسيين في الشريط الجانبي |
| الفترة | Period (`yyyy-MM`, `yyyy-Qn`) | شهر أو ربع. مثال: `2026-07`، `2026-Q3` |
| الربع | Quarter (`YearQuarter`) | ثلاثة أشهر. الربع 3 = يوليو–سبتمبر |
| Live | `ReleaseIds.Live` / مجلد `tables/live` | أحدث بيانات مستوردة (الربع المفتوح) |
| Release (إصدار) | `tables/<yyyy-Qn>` | لقطة مجمّدة لربع مُغلق كما نُشرت |
| Corrections (تصحيحات) | `tables/corrections/<release>.json` | تعديلات محروسة على إصدار دون تعديل صفوفه |
| Golden fixtures | `Fixtures/*-golden` | ملفات مرجعية تُقارن بها مخرجات الـ API لاكتشاف أي تغيير غير مقصود |

## مصطلحات مؤشرات الإيجار

| العربية | الإنجليزية / الحقل | المعنى |
|---|---|---|
| الضوابط الإيجارية | Rental controls | تدخل تنظيمي على الإيجار على مستوى المدينة |
| حالة المدينة / الحالة التشغيلية | `OperationalStatus` | لا إجراء / دراسة استباقية / دراسة تفصيلية / الضوابط مفعلة |
| التصنيف | `Classification` (`accelerated`/`elevated`/`sustainable`) | نمو متسارع / مرتفع / مستدام |
| المسار | `Path` (متسارع/مرتفع/…) و«تفاعلي Reactive / استباقي Proactive» | عمود في ملف Excel يحدد التصنيف |
| CAGR | `Cagr` | معدل النمو السنوي المركب للإيجارات |
| الزيادة الإيجارية | `RentalIncrease` / `rentalChange` | نسبة تغير الإيجار |
| الشواغر الإيجارية | `vacancy` | نسبة الوحدات المعروضة للإيجار غير المؤجرة (شهري) |
| الشواغر العامة | `publicVacancy` | نسبة الوحدات السكنية العامة غير المشغولة (بلا حدود مرجعية بعد) |
| العائد الإيجاري | `InvestmentReturn` / `roi` / Yield | عائد الإيجار على قيمة العقار |
| نسبة الإيجار إلى الدخل | `RentToIncome` / `rentToIncome` | حصة الإيجار من الدخل |
| العبء السكني | `HousingBurden` / `burden` | عبء السكن على فئات الدخل المحدود |
| CPI الإيجار الفعلي | `actualRentCpi` | الرقم القياسي لإيجار المسكن |
| التضخم العام | `generalInflationCpi` | CPI العام |
| الرقم القياسي (شقة/دور/فيلا) | `apartmentRentIndex`… | مؤشرات الإيجار حسب نوع الوحدة |
| فجوة العرض والطلب | Supply/Demand Gap | العرض − الطلب (سالب = عجز) |
| الرصد الإعلامي | Media monitoring | انطباع المواطنين (إيجابي/محايد/سلبي) |
| الانطباع العام | `OverallSentiment` | يُشتق من النسب الثلاث |
| ضمن الحد / دون الحد / متجاوز / متابعة / تجاوز مرتفع | status strings | حالات المؤشر مقابل `RentalPolicy` |
| إيجابي / محايد / سلبي | KPI status | حالة بطاقة المدينة مقابل `KpiThresholds` |
| الحد المرجعي للمراقبة | Reference threshold | الاسم المعتمد للحدود في التقارير |
| رحلة المدينة | `CityJourney` | مراحل الحالة التشغيلية الأربع |
| ما الذي نراقبه | `WhatToMonitor` | قائمة مؤشرات المراقبة لمدينة |
| حركة السوق | `MarketMovementTable` | جدول مؤشرات (CPI، التغير الإيجاري…) بمستواها وتغيرها وتقييمها |
| Monthly movement | `monthly_movement.json` | صفوف حركة السوق الشهرية كما نُشرت |
| Snapshot (التقاط) | `--snapshot-controls` | تجميد live كإصدار ربع |
| PeriodScope | `Snapshots/PeriodScope.cs` | الربع/الشهر المختار + شهر النص |
| PeriodValueMask | `Projections/PeriodValueMask.cs` | إخفاء القيم/الجمل التي لا تخص الفترة |
| Capped store | `CappedRentalControlsStore` | عرض للبيانات مقصوص عند شهر معين |

## مصطلحات تملك الأجانب

| العربية | في الكود | المعنى |
|---|---|---|
| شراء / بيع / هبة | `TransactionTypes` | أنواع العملية |
| فرد / شركة | `OwnerTypes` | نوع المالك |
| حصة الصفقة | `TransactionShare` / `Weight` | وزن الصفقة في العدّ |
| العدّ المرجّح | share-weighted count | مجموع الأوزان بدل عدد الصفوف |
| منطقة ← مدينة ← حي | `Region > City > District` | شجرة الجغرافيا |
| نافذة آخر 6 فترات | `FilterByLast6Periods` | النافذة الموحدة للتحليل |
| نقطة الارتكاز | anchor / `LatestDataDate` | أحدث تاريخ صفقة في كل البيانات |
| HHI | Herfindahl–Hirschman Index | مقياس التركّز |
| شرائح المساحة / القيمة | `ValueBuckets` | تقسيم الصفقات حسب المساحة أو القيمة |

## مصطلحات تقنية

| المصطلح | المعنى في المشروع |
|---|---|
| Recompute | إعادة بناء الـ Snapshot (نتائج جاهزة) في الذاكرة بعد تحميل البيانات |
| Projection | دالة static تحوّل البيانات الخام إلى DTO معيّن (مثل `OverviewProjection`) |
| Store | طبقة قراءة البيانات (`IRentalControlsStore`) تخفي مكان التخزين |
| Repository | قراءة/كتابة جداول الإصدارات (`IRentalControlsRepository`) |
| DTO | كائن يُرسل عبر الـ API |
| Signal | آلية الحالة التفاعلية في Angular 19 (تُستخدم في صفحات الإيجار) |
| Interceptor | وسيط HTTP في Angular يضيف `Authorization: Bearer` |
| Guard | حارس المسارات (`authGuard`) |
| SSE | Server-Sent Events: بث رد المساعد |
| JTI | معرّف الـ JWT الفريد؛ يُستخدم لإلغاء الرمز عند تسجيل الخروج |
| RTL | اتجاه من اليمين لليسار |
