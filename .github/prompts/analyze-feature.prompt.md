---
description: Analyze a feature or bug across the REGA Dashboard Angular app and ASP.NET Core API without changing code.
agent: ask

argument-hint: "Feature name (e.g., Rental Indicators > City Status), plus optional details"
---

# Analyze the REGA Dashboard

**Feature name:** ${input:featureName:Enter a feature name, or type project overview}

Use the feature name to scope the analysis and the chat or attached context for details. If the feature name is blank or is `project overview`, use any feature specified in the chat; otherwise give a project overview covering the main user flows, the Angular-to-API boundary, and how the API loads and aggregates data. This is a read-only analysis: do not modify files, run imports, or deploy anything.

## Project map

- **Frontend:** [Front](../../Front/) is an Angular 19 application. Start at [app.routes.ts](../../Front/src/app/app.routes.ts), then follow the relevant page or component into the [Angular services](../../Front/src/app/services/). The main dashboard areas are foreign ownership and rental indicators.
- **Backend:** [AqarInsights.Api](../../Back/AqarInsights.Api/) is an ASP.NET Core .NET 10 API. Start at [Program.cs](../../Back/AqarInsights.Api/Program.cs) for startup, authentication, and service registration. Follow the relevant [controller](../../Back/AqarInsights.Api/Controllers/) into the [models](../../Back/AqarInsights.Api/Models/), [services](../../Back/AqarInsights.Api/Services/), and [data path configuration](../../Back/AqarInsights.Api/Extensions/DataPaths.cs). The dashboard uses file-backed datasets and in-memory aggregation; verify the data source for the specific feature.
- **Local verification:** [run-local.bat](../../run-local.bat) is the local launcher. Look for existing coverage in [API tests](../../Back/AqarInsights.Api.Tests/) and [Angular unit tests](../../Front/tests/). Prefer the current source, project manifests, and environment configuration over README examples if they disagree.

## How to analyze

1. State the requested outcome or observed symptom and define the scope. Separate the current behavior from the desired behavior.
2. Trace the relevant flow end to end: route and UI state -> Angular service and request -> API controller and DTO -> service, data source, or aggregation -> response and rendering. For API-only or UI-only work, say which layers are not involved.
3. Check what actually crosses the boundary: HTTP method, route, parameters, defaults, response fields, auth requirements, and error handling. Where relevant, verify filters, period selection and release data, missing versus zero values, KPI units or thresholds, Arabic/English labels and RTL, and report or export consistency.
4. Identify the smallest viable change for the requested behavior, the existing code to reuse, and any dependencies or backwards-compatibility risks. For a bug, label a root cause as confirmed only when the evidence supports it; otherwise explain what would confirm it.
5. Identify focused tests and manual checks that would prove the result. Do not claim checks were run unless they were. Do not quote secrets, tokens, credentials, or raw user records.

## Response format

- **Summary:** What the feature does today and what the request would change.
- **Current flow:** The relevant entry points, data flow, and API contract, with `path:line` references.
- **Impact and risks:** Affected files or layers, edge cases, and any compatibility concerns.
- **Recommended approach:** Minimal implementation steps, or priorities if this is a project overview.
- **Verification and open questions:** Specific tests or smoke checks, plus any assumptions or gaps that materially affect the recommendation.

