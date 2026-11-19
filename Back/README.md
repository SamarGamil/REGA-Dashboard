# REGA Dashboard — Backend API

This is the engine room for the REGA real-estate dashboard. It's a small .NET 10 Web API that reads Saudi property transaction data, pre-computes all the aggregations the dashboard needs, and hands them to the Angular frontend behind a JWT login. There's no database to babysit — the data lives in JSON files and everything is crunched in memory when the app boots.

The UI that consumes this API is in a separate repo: `rega-dashboard-fe`.

## The short version

```bash
dotnet restore AqarInsights.Api.sln
dotnet run --project AqarInsights.Api
```

If your JWT secret is set, you'll see it load the transaction records, recompute aggregations, and start listening on **http://localhost:5032**. Hit the health check to be sure:

```bash
curl http://localhost:5032/v1/api/health-check
# {"status":"healthy"}
```

If instead it dies immediately with *"AuthSettings:JwtSecret is required and must not be empty"* — that's expected, not a bug. Keep reading.

## What you need first

- **.NET SDK 9.** Check with `dotnet --version`; you want `9.x`.
- Nothing else. No SQL Server, no Redis, no Docker for local dev. The data ships with the repo under `AqarInsights.Api/Data/` (transactions plus a filter-options file).

## Why it won't start without a secret

The API signs its JWTs with `AuthSettings:JwtSecret`. Rather than quietly boot with an empty key (which would be a real security hole), it throws on startup if the secret is blank. So the very first thing to do on a fresh clone is give it one.

Config is read from `appsettings.json`, but **every value can be overridden by an environment variable** — and that's how it runs in the cluster. The trick to remember: .NET turns the `:` in a config path into a double underscore `__` for env vars. So `AuthSettings:JwtSecret` is `AuthSettings__JwtSecret`, and a CORS origin is `Cors__AllowedOrigins__0`.

The handful of settings worth knowing:

| Config path | Env var | What it does |
|---|---|---|
| `AuthSettings:JwtSecret` | `AuthSettings__JwtSecret` | Signing key — required, and the longer the better |
| `AuthSettings:JwtExpiryHours` | `AuthSettings__JwtExpiryHours` | How long a token lives (24 by default) |
| `AuthSettings:UsersFile` | `AuthSettings__UsersFile` | Path to the login accounts JSON file (defaults to `Data/users-accounts.json`) |
| `Cors:AllowedOrigins` | `Cors__AllowedOrigins__0`, `__1`, … | Which frontends are allowed to call the API |

### Login accounts

Users live in `Data/users-accounts.json` — a plain array of `{ "username", "passwordHash" }`. Passwords are stored as **BCrypt** hashes (never plaintext), so the file is safe to commit. To add or change a user, hash the new password and edit the file:

```csharp
Console.WriteLine(BCrypt.Net.BCrypt.HashPassword("your-password", 11));
```

```json
[
  { "username": "admin", "passwordHash": "<bcrypt-hash>" }
]
```

### Getting a JWT secret locally

I'd use .NET user-secrets so nothing sensitive ends up in git:

```bash
dotnet user-secrets set "AuthSettings__JwtSecret" "any-long-random-string-you-like-32-chars-plus"
```

Don't feel like setting CORS? Leave `Cors:AllowedOrigins` empty and the API falls back to the Angular dev server (`http://localhost:4200`) plus the hosted frontend, which covers most day-to-day work.

## Talking to the API

Log in first, then use the token on everything else:

```bash
# 1) get a token
curl -X POST http://localhost:5032/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"your-password"}'

# 2) call a protected endpoint
curl http://localhost:5032/api/dashboard/summary \
  -H "Authorization: Bearer <token-from-step-1>"
```

The rest of the map:

- `POST /api/auth/login` — username + password in, JWT out. This one's open.
- `GET  /api/dashboard/...` — the actual dashboard numbers. Needs the token.
- `GET  /api/filters` — the filter options that populate the UI dropdowns.
- `POST /api/import` — kick off an Excel re-import.
- `GET  /api/reports/periods` — months (`yyyy-MM`) and quarters (`yyyy-Qn`) a rental-controls report can be generated for.
- `POST /api/reports/national` and `POST /api/reports/city/{cityId}` — generate a report; `?period=2026-Q2` or `?period=2026-08` picks the period (omit it for the current quarter).
- `GET  /v1/api/health-check` — open, no token. It's what Kubernetes pings.

Running in Development? There's a Scalar API reference waiting at **/scalar/v1** that lets you poke at everything from the browser.

## Tests

```bash
dotnet test AqarInsights.Api.sln
```

Everything lives in `AqarInsights.Api.Tests` (xUnit) — auth, the Arabic text normaliser, and the controllers.

## The CLI side

The same executable can do data chores instead of serving traffic:

```bash
# Pull the newest Excel file into the transactions data
dotnet run --project AqarInsights.Api -- --import

# Tidy up / normalise the existing data files
dotnet run --project AqarInsights.Api -- --normalize-data

# Rebuild the live rental-controls release from the workbooks and narratives in Data/rental-controls-source
dotnet run --project AqarInsights.Api -- --import-controls

# Patch the source workbooks with a month's update folder (then run --import-controls)
dotnet run --project AqarInsights.Api -- --apply-monthly-update --from <updatesDir> --month 2026-10 [--steps cpi,rental-vacancy,public-vacancy,media,gap]

# Freeze a rental-controls release (default: live) as a closed quarter's release (an existing quarter needs --force)
dotnet run --project AqarInsights.Api -- --snapshot-controls --period 2026-Q3 [--from <releaseId>] [--source <workbooksDir>] [--commit <sha>] [--force]

# One-time move from the monthly-release layout to quarter releases + monthly_movement + corrections (done 2026-11-02)
dotnet run --project AqarInsights.Api -- --migrate-controls-storage
```

They run the task and exit rather than starting the web server.

### Rental-controls data: release tables

All rental-controls data lives in `Data/rental-controls/tables/`, one folder (partition) per release: `live` is what
the dashboard serves; `<yyyy-Qn>` are closed quarters, frozen when the next quarter's data is imported, so past
quarters are shown and reported exactly as they were published. Each table is one JSON file holding an array with one
row per line, and every row carries its `releaseId`:

- `observations.json` — every number: `measure` (`kpi.*`, `indicator.*`, `city.*`, `gap.*`), `grain`
  (`latest`/`year`/`quarter`/`month`), `period`, `dim` (unit type or series key), `value`, `note`. A missing value
  has no row.
- `cities`, `city_status`, `market_movement`, `city_narrative`, `report_products`, `report_axes`, `media_monitoring`,
  `media_items`, `gap_cities` — the city list, state and text.
- `release.json` — the release itself.
- `source/` — the workbooks the release was imported from (audit copy).

Next to the partitions:

- `monthly_movement.json` — per city × month the as-published latest-month market-movement rows (the two CPI rows and
  "التغير الإيجاري (شهري)": level, change, assessment). Every import adds/replaces the live month's rows; a row whose
  level names another month is skipped with a warning. A month's view shows these rows; without them the CPI rows come
  from the cut series with assessment "—". (Vacancy, public vacancy, media and the CPI series keep their monthly
  history inside every release, so they need no such table.)
- `corrections/<releaseId>.json` — guarded corrections `{ id, table, match, field, from, to, reason, date }` plus
  free-text `notes`. A correction is applied when the release is loaded, only while exactly one row matches `match`
  and its `field` equals `from`; otherwise it is stale (logged, listed by the import, never applied). They live
  outside the partition, so rewriting a release keeps them; freezing live into its quarter copies live's corrections.
  Format and example: `docs/superpowers/specs/2026-11-02-release-storage-design.md` §3.

`IRentalControlsRepository` (`JsonTablesRepository`) reads and writes the partitions, the monthly table and the
corrections; moving to a database means bulk-loading each file into a table of the same name and implementing that
interface. `ReleaseMapper` turns a release into the in-memory model the projections and reports read.

- `--import-controls` checks first and writes nothing when a check fails: the new data's latest quarter must not be
  older than live's, and no month live shows for rental/public vacancy, media or the CPI series may disappear (months
  inside a frozen quarter excepted). When the data starts a newer quarter, the outgoing live (rows + `source/`
  workbooks) is frozen as its quarter, once. It then writes the month's `monthly_movement` rows and live with its
  source workbooks.
- A quarterly report uses the live data for the current quarter and the quarter's release for a closed quarter.
- A month belongs to its quarter (portal pages and reports alike). Every month of a quarter that has data is listed
  (up to the newest month), whether or not it has a release of its own. A month view is its quarter's data, as the
  quarter view takes it, cut at the month: quarterly values (rental increase, return, rent-to-income, burden, CAGR,
  transactions, indices, the city-status overview) are the quarter's as is, so the three months of a quarter share
  them; monthly values follow the month (`PeriodScope`, `PeriodValueMask`):
  - rental and public vacancy come from the vacancy series; a city without a value for the panel's month shows "—";
  - the CPI rows (and the monthly rent change) are the month's own as published, from `monthly_movement`, when
    stored; otherwise the CPI rows come from the cut series with assessment "—"; a monthly value from another month
    shows "—";
  - media monitoring is the month's own entry, else unavailable (e.g. April and May 2026 have none).
  The written text (portal summary, report narratives) keeps quarterly sentences; monthly ones (CPI, inflation, media,
  a named month) stay only for the month the text was written for, and sentences naming a later month are dropped; a
  section left empty shows "—". In another month, the actual-rent CPI sentence is rebuilt with that month's own figure
  ("الإيجار الفعلي (CPI) يرتفع 7.3% سنويا على مستوى المدينة (أبريل 2026).", `MonthlyCpiStatement`): it replaces the
  summary's CPI bullet and the report executive section's first actual-rent sentence, and is left out when the month
  has no CPI figure. A month after the newest quarter (its quarterly data has not arrived) is shown from the
  live data as is.
- A quarter view (and the default page with no `?period=`, which is the live quarter's view) shows each monthly value
  at its latest month inside the quarter, tagged with that month, or "—" when the series ends before the quarter.
  Once a monthly update moves live past its quarter (e.g. October on the open Q3), the text month is that newer
  month, and the quarter's views mask the narrative's monthly sentences like any other foreign month's text (the
  stored releases keep their own text month, capped at their quarter). The narratives always describe the newest
  imported month.
- Market-movement rows show their period under the indicator. Newer workbooks state it in the level
  ("مدينة - ‎2026-Q3"); older ones (the 2026-Q2 release) write just "مدينة", and the row gets the latest period of the
  series it summarises — the period the newer workbooks state — or stays plain when that series is missing (Abha and
  Jazan CPI in Q2).
- The supply/demand gap (مؤشرات فجوة العرض والطلب) is not period data: its workbook is a single sheet with no history,
  so every period shows the live workbook as is (no cut, no release copy).
- A quarterly report never shows months after the quarter: monthly series (rent/inflation CPI, media monitoring)
  are cut at the quarter's last month, and the CPI panel uses that month's figures (from `monthly_movement`, or
  computed from the series when none is stored). The stored releases themselves are left untouched.
- The overall media impression (الانطباع العام) is derived from the positive/neutral/negative shares, not the
  workbook's label: the largest share wins, positive = negative → متباين, and a direction tied with neutral wins
  (`MediaSentimentClassifier`). Narrative media bullets must not contradict it (`AllPeriodsWordingStandardsTests`).
- Every report states its period in the header ("فترة التقرير: …"), the footer, the file name and the file metadata;
  city reports also print their creation date in the footer (PDF and Word).
- The report's product table and averages line are generated from the data; a year-on-year change whose year-ago base
  is under 20 deals is shown as "—" (sample noise).
- Rental yield is shown with one decimal: from the latest point of the city workbook's yield series, else the
  overview's own assessment text ("العائد 5.3% دون الحد") — each used when it rounds to the overview workbook's figure,
  which is that point rounded to a whole percent (Jeddah 5.33% → 5%; rounding also hid Buraidah 5.8% and Bahah 5.5%
  below the 6% reference). The city's own point goes first because the overview text can belong to another city (Q2
  2026: Buraidah's line repeats Hail's "العائد 4.7%"; Buraidah's point is 5.23%). Statuses are judged on that value;
  otherwise the overview figure stays. Narrative yield figures must
  match (`AllPeriodsWordingStandardsTests`; the 2026-Q2 release's quoted figures are aligned by
  `tables/corrections/2026-Q2.json`), and the national report prints the city report's strings
  (`NationalCityConsistencyTests`).
- Reports tag each indicator monthly or quarterly (KPI cards, the city indicators table, the supply/investment strip
  and the national table headers), as the dashboard does.
- The national table colours each cell as the city's own report colours that figure (its platform status, e.g. burden
  above 30% red «متجاوز», a rising actual rent red «ارتفاع»; a missing figure grey), read with the legend ضمن / قرب /
  يتجاوز الحد المرجعي للمراقبة / غير متاح. Burden and actual rent carry ↑/↓ against the previous quarter/month, «—» when unchanged, nothing
  without a previous value; the summary's «تباطؤ الإيجار الفعلي…» point counts the table's ↓ and quotes the named
  city's value as the table prints it (`NationalReportService.ContextFor`, `NationalCityConsistencyTests`).
- `--import-controls` also warns when a report narrative quotes figures that contradict the imported data, and
  `--snapshot-controls` refuses to create a release dated after the current month (workbook date typos).
- Report narratives are written in `Data/rental-controls-source/city-reports/<cityId>.json` and imported with the
  data: after editing one, run `--import-controls` (`NarrativeSourceTests` fails until the live release matches the
  files). Narrative edits meant for the outgoing period must be imported before the next period's workbooks are added.
  An import without the `city-reports` folder fails and writes nothing, so it can never drop the narratives.
- City reports print every indicator reference as «الحد المرجعي للمراقبة» with the threshold of the indicator it
  refers to (`ReportThresholds`, after `ReportTerminology` writes the platform's indicator names): "دون الحد" →
  "دون الحد المرجعي للمراقبة (6%)", "(حد 7-9%)" → "(الحد المرجعي للمراقبة 7%–9%)", status words in cards and tables
  included. The yield shows the indicators-page status (e.g. «دون الحد المرجعي للمراقبة») in the KPI card, the
  supply strip and the investment axis alike. `ReportReferenceStandardsTests` checks this on every period's reports.
- The platform's assessment line in a report's recommendations states its point change with the period it is measured
  against, from the city page ("بتراجع 0.8 نقطة عن الربع السابق") in a quarterly report; a monthly report drops that
  quarter-on-quarter change (any city) and keeps the quarter's level. A monthly report also drops the narrative's
  quarter-on-quarter comparison clauses ("وارتد صعودًا في الربع الثالث (34% ← 36%)", "رغم تراجعها من 14% في الربع
  السابق"), keeping the rest of the sentence; multi-quarter trends ("تتباطأ بانتظام (11% ← 3%)") stay.
- Fixes to stored data are corrections in `corrections/<releaseId>.json` (see above), never edits to a partition's
  rows.
- `PeriodGoldenTests` pins every period's pages and report models; regenerate its fixtures
  (`RC_PERIOD_GOLDEN_WRITE=1`) only for an intended change, and review the diff. `GoldenEquivalenceTests` pins the live
  pages the same way (`RC_GOLDEN_REGEN=1`).
- Charts and tabs show a blank workbook cell as a gap (`null` / "—"), never as 0; one-decimal values round half away
  from zero.
- The workbook classes the CAGR on its value rounded to a whole percent (6 / 7-9 / 10+). The portal legend keeps those
  bands; the city report, which shows the CAGR to one decimal, states the exact cut-offs (أقل من 6.5% / 6.5%–9.4% /
  9.5% فأكثر). The national report breaks ties between rounded yields on the city's unrounded yield (its ROI series).

### Monthly data update

The client sends a folder each month. `--apply-monthly-update` takes only the monthly figures into the workbooks in
`Data/rental-controls-source`; quarterly data changes only with the quarter's workbooks.

| Step | Update file | Patched workbook | What is taken |
|---|---|---|---|
| `cpi` | `… تحليل مدينة <city> ….xlsx` (14) | `تحليل مدينة <city>_محدث.xlsx` | sheet `مؤشرات CPI`; market-movement rows `الإيجار الفعلي للمسكن (CPI)`, `التضخم العام (CPI)`, `التغير الإيجاري (شهري)`; the two conclusions quoting them |
| `rental-vacancy` | the same city workbooks, sheet `الشواغر الإيجارية` | `مؤشرات المحددات الايجارية (الشواغر العامة).xlsx`, sheet `الشواغر الايجارية` | months not there yet (existing months are kept; a different value is a warning) |
| `public-vacancy` | `الشواغر العامة ….xlsx` (English city names, fraction) | same vacancy workbook, sheet `الشواغر العامة- لم يعتمد` | every month with a rate (whole %, 1 decimal); older history kept |
| `media` | `التحليل الشهري لانطباعات المواطنين ….pptx` | `الرصد الإعلامي.xlsx` | the month's rows of all three sheets (replaced) |
| `gap` | `فجوة_العرض_والطلب_….xlsx` | `supply-demand-gap.xlsx` | monthly actual gap (a surplus for every city — data owner, 2026-11-18) and actual supply = demand + gap |

A city whose update does not reach `--month` is skipped with a warning. Review every warning, run
`--import-controls`, then regenerate the golden fixtures (`RC_GOLDEN_REGEN=1`, `RC_PERIOD_GOLDEN_WRITE=1`) and review
their diff: only monthly values may change.

## Docker

The `Dockerfile` publishes the API into an ASP.NET runtime image that listens on **port 5000** (not 5032 — that's a local-dev thing). It builds from Elm's base images, so hand it the registry:

```bash
docker build --build-arg REGISTRY=nexus.elm.sa -t rega-dashboard:local .
docker run --rm -p 5000:5000 -e AuthSettings__JwtSecret=dev-secret-min-32-chars rega-dashboard:local
```

## How it gets deployed

Helm does the heavy lifting from the `ois-helm-releases` repo (the `rega-dashboard` chart). Each environment supplies its own `AuthSettings__*` / `Cors__*` values through the chart's `envVars`, and Kubernetes checks the pod's health on `/v1/api/health-check` at port 5000 before sending it traffic.

## Where things live

```
AqarInsights.Api/            The Web API
  Controllers/               Auth, Dashboard, Filters, Import
  Services/                  Data loading, aggregation, Excel conversion, auth
  Models/                    DTOs + the settings classes
  Data/                      transactions.json + filter-options JSON
AqarInsights.Api.Tests/      xUnit tests
AqarInsights.Api.sln         Solution (at the repo root)
Dockerfile                   Container build
pipeline-ci.yaml             Elm CI metadata (Nexus IQ / ACS / Coverity)
```
