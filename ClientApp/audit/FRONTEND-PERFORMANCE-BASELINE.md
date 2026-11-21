# Frontend Performance Baseline (`ClientApp/`)

**Baseline Commit:** `45606d59c3b21d0ee9577d92f64c05a1c7375a63` (`development`)  
**Measurement Date:** 2026-11-21  
**Command:** `npm.cmd --prefix ClientApp run build` (`tsc -b && vite build`)

---

## 1. Production Bundle Baseline

| Metric | Baseline Value | Notes |
|---|---|---|
| **Build Status** | Passed (`0` errors) | `2296` modules transformed |
| **Build Duration** | `1.14s` | Vite `v8.3.5` |
| **Initial JS (`index-*.js`)** | `363.42 kB` (`111.30 kB` gzip) | Includes `AppShell`, all 7 route wrappers, `CommandPalette`, `Topbar`, `ConfirmModal` |
| **Initial CSS (`index-*.css`)** | `240.33 kB` (`37.17 kB` gzip) | Tailwind CSS 4 + `App.css` + component CSS |
| **Largest Non-Index JS Chunk (`button-*.js`)** | `66.14 kB` (`23.53 kB` gzip) | Shared `Button` + `SpecularButton` (WebGL2) + shared runtime |
| **Largest Route Chunks** | `UsersPage` `50.60 kB` (`12.17 kB` gzip)<br>`LocalApplicationsPage` `47.31 kB` (`10.94 kB` gzip)<br>`LicenseRenewalReplacementPage` `41.77 kB` (`8.95 kB` gzip)<br>`DashboardPage` `41.58 kB` (`8.50 kB` gzip)<br>`card-*.js` `39.70 kB` (`10.44 kB` gzip) | Route pages are lazy-loaded per route group |
| **Other Notable Chunks** | `Combination-*.js` `30.75 kB`<br>`ScheduleAppointmentPage` `33.23 kB`<br>`LicenseDetentionPage` `29.76 kB`<br>`LicenseHistoryPage` `27.37 kB` | Plus 14 small `lucide-react` icon chunks (`0.09–0.53 kB`) |

---

## 2. Runtime & Network Baseline (Code-Verified)

| Area | Baseline Observation |
|---|---|
| **Animation CPU/GPU (Idle)** | `SpecularCard` (default on `<Card>`) and `useStatCardEffects` (`StatCard`) run **continuous `requestAnimationFrame` loops** calling `getBoundingClientRect()` every frame even when idle (e.g., ~30+ concurrent loops on Dashboard, 15 on `TestAppointmentsPage`), plus `N` duplicate `window.pointermove` listeners. `Sidebar.tsx` calls `setMousePos` React state on every `pointermove` pixel. |
| **Request Cancellation** | `useAsyncData` creates an `AbortController`, but 13 of 14 service files (and all page fetchers) do not forward `signal` to `apiRequest`; `apiClient.ts` also converts `AbortError` into `ApiError(0, 'Network error')` and has no request timeout. |
| **Duplicate / Extra Requests** | - `TestAppointmentsPage`: fires 4 requests on mount (including a duplicate `tab='all'` `pageSize=1` call).<br>- `PeopleRoutes`: fires an unpaginated `GET /People` via `PeopleContext.refreshPeople()` on person save even though `PeoplePage` does not use `PeopleContext`.<br>- `PersonPhoto`: N+1 uncached photo fetches; duplicate fetch in `PersonDetailsPage` lightbox; 404 fetches when `hasPhoto` is omitted.<br>- `LocalApplicationsPage`: `onSearchChange` resets page on every keystroke while `debouncedSearch` lags 300ms.<br>- `countryService.getCountries`: uncached across mounts. |
| **Core Web Vitals (LCP / INP / CLS)** | `NOT MEASURED` (no live browser/Lighthouse server run in sandboxed audit). |

---

## 3. Tooling & Environment Limitations

- Terminal commands execute in an isolated Windows sandbox without live `DVLD.Api` / SQL Server network connectivity; live Lighthouse / Core Web Vitals against production API responses are `NOT MEASURED`.
- Unit/component tests (`vitest run`) run under `jsdom` with mocked service responses.
