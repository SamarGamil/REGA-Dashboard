# REGA Dashboard — Frontend

This is the face of the REGA real-estate dashboard: an Angular 19 single-page app that turns the backend's numbers into something you can actually read. It's got the executive summary up front, a market overview, and a set of deep-dive pages — area, property, financial, geographic, nationality, and strategic analysis — all wired up with charts, shared filters, and one-click PDF/Excel export.

All the data comes from the API in the `rega-dashboard` repo, so you'll usually want that running alongside this.

## The short version

```bash
npm install
npm start
```

`npm start` is just `ng serve` under the hood. It brings up the dev server at **http://localhost:4200** with hot reload, and it expects the API at **http://localhost:5032/api** (that's the default baked into `src/environments/environment.development.ts`). Open the browser, log in, and you're in.

If the pages load but every panel is empty or you're bounced back to the login screen, it's almost always one of two things: the backend isn't running, or its CORS list doesn't include `http://localhost:4200`.

## What you need first

- **Node.js 20 or newer** and npm (`node --version` to check).
- The backend running if you want real data. Without it the app still builds and serves — it just has nothing to talk to.

## The scripts you'll actually use

| Command | What happens |
|---|---|
| `npm start` | Dev server on :4200, hot reload — your day-to-day |
| `npm run build` | Production build into `dist/` |
| `npm run watch` | Keeps rebuilding on every save (dev config) |
| `npm test` | Unit tests via Karma/Jasmine |

## Pointing it at a different API

There are two environment files, and which one applies depends on how you run the app:

- `src/environments/environment.development.ts` → used by `npm start`, points at `http://localhost:5032/api`
- `src/environments/environment.ts` → used by `npm run build`, points at the hosted backend

If your API lives somewhere else, change `apiUrl` in the matching file. One gotcha worth repeating: the backend only answers requests from origins on its CORS allow-list, so if you serve this app from anything other than `localhost:4200`, add that origin to the API's `Cors:AllowedOrigins` or the browser will block the calls.

## Building for production

```bash
npm run build
```

The optimised bundle drops into `dist/`, and that's exactly what gets served in the deployed environments.

## Docker

The `Dockerfile` is a two-stage build: it compiles the app in a Node image, then serves the static bundle from a small **nginx-unprivileged** image on **port 8080**. It builds from Elm's base images, so hand it the registry:

```bash
docker build --build-arg REGISTRY=nexus.elm.sa -t rega-dashboard-fe:local .
docker run --rm -p 8080:8080 rega-dashboard-fe:local
```

Two details worth knowing. The Angular `application` builder emits its static files to `dist/browser/`, so that's the folder nginx serves — not `dist/` itself. And because this is a single-page app, a small `nginx.conf` adds a `try_files … /index.html` fallback so deep links and page refreshes resolve to the app instead of a 404.

## How it gets deployed

Helm does the heavy lifting from the `ois-helm-releases` repo (the `rega-dashboard-fe` chart). Each environment supplies its own values — most importantly the API URL the built bundle points at — and Kubernetes serves the container on port 8080 behind the ingress.

## How it's put together

```
src/app/
  components/       Reusable UI — base pieces, feature widgets, layout, sidebar, filter bar
  pages/            One folder per dashboard page (executive summary, market overview, …)
  services/         API calls, auth, filter state, export, sidebar state
  guards/           The route guard that keeps you logged in
  interceptors/     Attaches the JWT to every outgoing request
  utils/            Little helpers like number formatting
src/environments/   Per-environment apiUrl config
```

## The auth flow, briefly

You log in through the backend's `/api/auth/login`. The JWT that comes back is stored on the client, and from then on the HTTP interceptor quietly attaches it to every API request. The route guard does the bouncer job — no valid token, no dashboard, back to the login page.

## Tech under the hood

Angular 19 (standalone components) · Tailwind CSS · ECharts via `ngx-echarts` · ExcelJS + jsPDF for exports · ngx-translate for i18n.
