# 09 — التشغيل المحلي والاختبارات والنشر

> الأوامر هنا بصيغة **PowerShell على Windows**. ما كُتب عليه «تحققنا» جرّبناه فعلاً على الجهاز (.NET SDK `10.0.400`، Node `v20.18.0`)؛ وما كُتب «من الـ README» أو «يحتاج تأكيد» لم نتحقق منه.

## 1) المتطلبات

| الأداة | المطلوب | ملاحظة |
|---|---|---|
| .NET SDK | **10.x** (المشاريع `net10.0`) | الـ README الخلفية تقول SDK 9 — قديم، تجاهله |
| Node.js | 20+ للتشغيل؛ وللاختبارات الوحدة يُرجَّح 22+ (انظر §5) | صورة بناء الواجهة تستخدم `node:21.7.2` |
| Git, Windows Terminal (`wt.exe`) | لاستخدام `run-local.bat` | اختياري |
| Chrome | يفتحه `run-local.bat` | اختياري |

لا تحتاج SQL Server ولا Redis ولا Docker محلياً.

## 2) التشغيل السريع (الطريقة الموصى بها)

من جذر المستودع:

```powershell
.\run-local.bat
```

ما يفعله ([run-local.bat](../../run-local.bat)):
1. يفتح Windows Terminal بتبويبين: **REGA API** و**REGA Angular**.
2. التبويب الأول: `dotnet restore … --source https://api.nuget.org/v3/index.json` ثم `dotnet run --no-restore --launch-profile http` (المنفذ **5032**).
3. التبويب الثاني: `npm start` (المنفذ **4200**).
4. ينتظر (حتى دقيقتين) نجاح `http://localhost:5032/v1/api/health-check` و`http://localhost:4200/`.
5. يفتح Chrome على التطبيق وعلى Scalar (`http://localhost:5032/scalar/v1`).

> يفترض أن `npm install` سبق تشغيله في `Front/` (مجلد `node_modules` موجود).

## 3) التشغيل اليدوي

### الـ Backend

```powershell
cd Back
dotnet restore AqarInsights.Api.sln --source https://api.nuget.org/v3/index.json
dotnet run --project AqarInsights.Api --no-restore --launch-profile http
# تحقق:
curl.exe http://localhost:5032/v1/api/health-check
```

- الـ profile `http` يضبط `ASPNETCORE_ENVIRONMENT=Development` والمنفذ 5032 ([launchSettings.json](../../Back/AqarInsights.Api/Properties/launchSettings.json)). يوجد أيضاً `https` على 7056.
- في Development: CORS يسمح لـ `http://localhost:4200`، وتتوفر وثائق OpenAPI وواجهة Scalar على `/scalar/v1`.
- `JwtSecret` موجود افتراضياً في `appsettings.json`/`appsettings.Development.json` (مفتاح تطوير) فالتطبيق يقلع. الـ README تقول إنه يفشل بدون سر — هذا قديم. ولا تعتمد هذا المفتاح خارج التطوير (انظر 10).

### الـ Frontend

```powershell
cd Front
npm install
npm start          # ng serve على http://localhost:4200
```

الواجهة في التطوير تتصل بـ `http://localhost:5032/api` ([environment.development.ts](../../Front/src/environments/environment.development.ts)).

## 4) تسجيل الدخول محلياً

- يلزم حساب في `Data/users-accounts.json` (اسم مستخدم + BCrypt hash). **اطلب بيانات حساب تطوير من الفريق ولا تفترضها.**
- إن احتجت حسابك الخاص دون تعديل ملف متتبَّع في git: أنشئ ملفاً خارج المستودع (بنفس الصيغة) ووجّه الـ API إليه عبر متغير بيئة:
  ```powershell
  $env:AuthSettings__UsersFile = "C:\dev\my-users.json"
  ```
  ثم شغّل الـ API من نفس النافذة. (طريقة توليد الـ hash في [08-data-and-operations.md](./08-data-and-operations.md).)
- تجربة سريعة (PowerShell):
  ```powershell
  Invoke-RestMethod -Method Post -Uri http://localhost:5032/api/auth/login -ContentType 'application/json' `
    -Body (@{ username = '<user>'; password = '<pass>' } | ConvertTo-Json)
  ```

## 5) الاختبارات

### الـ Backend (xUnit)

الأمر المقصود: `dotnet test AqarInsights.Api.sln` — لكن **جرّبناه وفشل** لسببين شائعين:

**أ) الـ NuGet feed الخاصة (تحققنا):** إعداد NuGet العام على الجهاز يتضمن مصادر خاصة على Azure DevOps (`Thiqah`…) تُرجع `401 Unauthorized` فيفشل الـ restore (`NU1301`). الحل الذي نجح عندنا:

```powershell
cd Back
dotnet restore AqarInsights.Api.sln --source https://api.nuget.org/v3/index.json   # نجح (exit 0)
dotnet test AqarInsights.Api.sln --no-restore
```

> الأفضل أن تضبط مصادر NuGet لديك صحيحة (تسجيل دخول للـ feed) إن كنت تحتاجها لمشاريع أخرى.

**ب) قفل الملفات (تحققنا):** إن كان الـ API يعمل (مثلاً عبر `run-local.bat`) فإن `AqarInsights.Api.exe/.dll` في `bin\Debug\net10.0` مقفلان، وبناء مشروع الاختبارات يفشل بـ `MSB3027/MSB3021`. **أوقف الـ API** (أو نافذة REGA API) قبل `dotnet test`.

لم نتمكن من تشغيل مجموعة الاختبارات كاملة أثناء كتابة هذه الوثيقة بسبب (ب)، فلا نعرف عدد الاختبارات ولا نتيجتها الحالية — شغّلها أول ما تقلع.

تشغيل جزء محدد:

```powershell
dotnet test AqarInsights.Api.sln --no-restore --filter "FullyQualifiedName~ForeignOwnershipGolden"
dotnet test AqarInsights.Api.sln --no-restore --filter "FullyQualifiedName~PeriodValueMask"
```

إعادة توليد الـ golden (فقط لتغيير مقصود):

```powershell
$env:RC_GOLDEN_REGEN = "1"          # صفحات الإيجار الحية
$env:RC_PERIOD_GOLDEN_WRITE = "1"   # فترات الإيجار
$env:FO_GOLDEN_REGEN = "1"          # تملك الأجانب (الـ endpoints السبعة)
dotnet test AqarInsights.Api.sln --no-restore --filter "FullyQualifiedName~Golden"
Remove-Item Env:RC_GOLDEN_REGEN, Env:RC_PERIOD_GOLDEN_WRITE, Env:FO_GOLDEN_REGEN
git diff --stat   # راجع بعناية
```

### الـ Frontend

```powershell
cd Front
npm run test:unit     # node --test "tests/**/*.test.ts"
```

**فشل عندنا على Node 20.18** بالرسالة `Could not find '…\tests\**\*.test.ts'`. السبب المرجَّح (استنتاج لم نختبره): توسيع الـ glob داخل `node --test` وتشغيل ملفات `.ts` مباشرةً (الاختبارات تستورد `…/utils/pagination.ts` بامتداده) يحتاجان Node أحدث (تشغيل TypeScript بـ type stripping يتطلب 22.6+). جرّب Node 22/23 (مثلاً عبر nvm-windows) أو تأكد من إصدار Node المستخدم في الـ CI.

`npm test` غير مفيد: لا يوجد target باسم `test` في `angular.json`.

## 6) البناء والتحقق

```powershell
cd Front;  npm run build          # يشغّل prebuild (generate-version.mjs) ثم ng build → dist/browser
cd Back;   dotnet build AqarInsights.Api.sln --no-restore
```

## 7) الحاويات والنشر (من الـ README والملفات)

- [Back/Dockerfile](../../Back/Dockerfile): مرحلة build على صورة `dotnet/sdk:10.0` من سجل Nexus (`ARG REGISTRY=nexus.elm.sa`) ثم `dotnet build -c Release` وضغط المخرجات في `/output/app.tar.gz`. **لا يُنتج صورة runtime** بنفسه؛ فشرح الـ README (منفذ 5000، `docker run`) لا يطابق الملف الحالي — **يحتاج تأكيد** كيف يُغلَّف الـ artifact في خط النشر.
- [Front/Dockerfile](../../Front/Dockerfile): `node:21.7.2` → `npm install --force` → `npm run build` → `app.tar.gz` من `dist`. والـ README تذكر تقديم nginx على المنفذ 8080 مع [nginx.conf](../../Front/nginx.conf).
- النشر عبر Helm (مستودع `ois-helm-releases`) مع متغيرات بيئة لكل بيئة (`AuthSettings__*`, `Cors__*`).
- [web.config](../../Back/AqarInsights.Api/web.config) ومسارات `C:\OIS\…` تدل على استضافة IIS/Windows في بعض البيئات.
- CORS المسموح في `appsettings.json` و`Staging`: `https://10.240.112.21:9061`؛ و`config.json` الحالي للواجهة يشير إلى `https://10.240.112.21:9060/api`.

## 8) مشاكل شائعة وحلولها

| العَرَض | السبب المحتمل | الحل |
|---|---|---|
| `NU1301 … 401` عند restore/test | مصادر NuGet خاصة بلا صلاحية | `--source https://api.nuget.org/v3/index.json` ثم `--no-restore` |
| `MSB3027 … file is locked by AqarInsights.Api` | الـ API يعمل | أوقفه ثم أعد الأمر |
| الواجهة تفتح لكن كل اللوحات فارغة أو تعود لـ login | الـ API متوقف، أو CORS لا يتضمن `http://localhost:4200`، أو رمز منتهٍ | شغّل الـ API، راجع `Cors:AllowedOrigins`، أعد الدخول |
| تسجيل الدخول يفشل | لا يوجد مستخدم/كلمة مرور صحيحة؛ أو الملف لم يُعد تحميله | راجع §4، أعد تشغيل الـ API |
| بعد `--import` لا يخرج البرنامج | فرع `--import` يكمل إلى تشغيل الخادم (انظر 08) | `Ctrl+C` |
| توليد تقرير ينشئ مجلد `C:\OIS\Dashboard\Backend\reports` | `Reports:HistoryFile` مسار مطلق في `appsettings.json` ويُنشأ مجلده تلقائياً | اضبط `$env:Reports__HistoryFile` على مسار محلي خارج المستودع قبل التشغيل |
| أرقام الإيجار لم تتغير بعد `--import-controls` | الـ API لم يُعد التشغيل | أعد التشغيل |
| `npm run test:unit` يفشل | إصدار Node | انظر §5 |

## 9) متغيرات بيئة مفيدة للتطوير

| المتغير | الاستخدام |
|---|---|
| `ASPNETCORE_ENVIRONMENT=Development` | يفعّل Scalar وCORS الافتراضي لـ 4200 ونص السجلات |
| `AuthSettings__JwtSecret` | مفتاح التوقيع (32+ حرفاً) |
| `AuthSettings__UsersFile` | ملف مستخدمين شخصي |
| `Reports__HistoryFile` | مكان سجل التقارير |
| `Logging__File__Directory` | مكان سجلات الملفات (خارج Development) |
| `Assistant__BaseUrl / ApiKey / Model` | تفعيل «تحدث مع البيانات» (لا تضع المفتاح في ملف يُرفع) |
| `RC_GOLDEN_REGEN`, `RC_PERIOD_GOLDEN_WRITE`, `FO_GOLDEN_REGEN` | إعادة توليد الـ golden (إيجار حي / فترات الإيجار / تملك الأجانب) |
