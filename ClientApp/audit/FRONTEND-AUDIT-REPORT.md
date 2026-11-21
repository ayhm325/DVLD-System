# Frontend Audit Report (`ClientApp/`)

**Baseline Commit:** `45606d59c3b21d0ee9577d92f64c05a1c7375a63` (`development`)  
**Scope:** `ClientApp/**` only (backend, SQL, and API contracts read-only).

---

## Summary of Verified Findings

| ID | Severity | Area | File & Lines | Verified Issue | Status |
|---|---|---|---|---|---|
| F-01 | **P0** | Auth / API Client | `src/services/apiClient.ts:L332-382` | `POST /Auth/login` 401 triggered `refreshSession()` and `unauthorizedHandler`, potentially broadcasting session-expired logout | **Fixed** |
| F-02 | **P0** | Auth / API Client | `src/services/apiClient.ts:L300-313`, `src/context/AuthContext.tsx:L94-99` | `logoutAllSessions()` used `sendWithRefresh` (could mint a new token on 401 during logout); `logoutAll()` delayed local state clear until `.finally()` | **Fixed** |
| F-03 | **P0** | Request Lifecycle | `src/services/apiClient.ts:L332-352` | `send()` masked `AbortError` as `ApiError(0, 'Network error')` and had no request timeout | **Fixed** |
| F-04 | **P0** | Request Lifecycle | `src/hooks/useAsyncData.ts:L80-132` | `refresh()` resolved synchronously before fetch completed; `loading` stayed `true` when `enabled` became `false` | **Fixed** |
| F-05 | **P1** | Animation / Rendering | `src/components/ui/SpecularCard.tsx:L243-348` | Continuous idle RAF loop + per-frame `getBoundingClientRect()` across ~30+ cards on Dashboard; missing `visibilitychange` pause | **Fixed** |
| F-06 | **P1** | Animation / Rendering | `src/components/common/hooks/useStatCardEffects.ts:L69-178` | Continuous idle RAF loop + per-frame `getBoundingClientRect()` across up to 15 `StatCard`s; missing `visibilitychange` pause | **Fixed** |
| F-07 | **P1** | Animation / Rendering | `src/components/layout/Sidebar.tsx:L51-87, L215, L231` | `setMousePos` React state updated on every `pointermove` pixel, re-rendering entire Sidebar | **Fixed** |
| F-08 | **P1** | Animation / Rendering | `src/components/ui/SpecularButton.tsx:L411-418, L668-670` | Dependency-less `useEffect` woke WebGL2 redraw on every parent re-render; no `visibilitychange` or dynamic `prefers-reduced-motion` listener | **Fixed** |
| F-09 | **P1** | API Efficiency | `src/components/layout/router/PeopleRoutes.tsx:L37-46` | `refreshPeople()` called unpaginated `GET /People` on person save even though `PeoplePage` uses `getPeoplePage` | **Fixed** |
| F-10 | **P1** | API Efficiency | `src/components/test-appointments/TestAppointmentsPage.tsx:L99-115` | `countsFetcher` duplicated the `'all'` tab request on mount | **Fixed** |
| F-11 | **P1** | API Efficiency | `src/components/common/PersonPhoto.tsx:L78-113`, `MyProfilePage.tsx:L352` | Uncached photo blob downloads (duplicate fetch in `PersonDetailsPage` lightbox); omitted `hasPhoto` on `MyProfilePage` caused unnecessary 404s | **Fixed** |
| F-12 | **P1** | API Efficiency | `src/services/countryService.ts:L6-8` | Static `/Countries` catalog re-fetched on every mount | **Fixed** |
| F-13 | **P1** | Pagination | `src/components/local-applications/LocalApplicationsPage.tsx:L224-231, L390-393` | `onSearchChange` reset page on every keystroke before debounce; `returnToList(true)` triggered double fetch on `page > 1` | **Fixed** |
| F-14 | **P1** | Bundle / Loading | `src/App.tsx:L7` | `AppShell` statically imported on unauthenticated `<Login />` screen | **Fixed** |
| F-15 | **P2** | Hooks / Stability | `src/hooks/useTableFilters.ts:L58`, `src/hooks/usePagination.ts:L80-129` | Default `{}` in `useTableFilters` invalidated `resetFilters`/`isFiltered` every render; `usePagination` invoked `onPageChange` inside state updater | **Fixed** |
| F-16 | **P2** | DataTable / A11y | `src/components/common/DataTable.tsx:L268-270`, `Topbar.tsx:L260`, `CommandPalette.tsx:L236-320` | Object-reference row highlight lost on refresh; duplicate `<h1>` with `PageHero`; `CommandPalette` lacked focus trap, scroll lock, and focus restore | **Fixed** |
| F-17 | **P1** | Request Cancellation | `src/services/*.ts` (13 of 14 files) & page fetchers | `useAsyncData` `AbortSignal` not forwarded through most services/fetchers (note: omitted intentionally on `Detained`/`International`/`TestAppointments` earlier to avoid StrictMode `OperationCanceledException` in VS debugger) | **Open (Documented)** |
| F-18 | **P2** | Auth Cross-Tab | `src/context/AuthContext.tsx:L119-136` | `token-refreshed` updates `expiresAtUtc` without updating `accessToken`; `login` broadcast triggers concurrent `/Auth/refresh` across open tabs | **Open (Documented)** |
| F-19 | **P2** | DataTable Sorting | `AppointmentTable.tsx:L114-202`, `TestAppointmentsTable.tsx:L71-211` | Columns marked `sortable: true` without passing `onSort` to `<DataTable>` | **Open (Documented)** |
| F-20 | **P2** | Backend-Limited KPIs | `DriversPage`, `DetainedLicensesPage`, `InternationalLicensesPage`, `TestAppointmentsPage` | Sub-metrics in summary cards reflect current page only; global KPI counts require backend `/summary` endpoints | **Open (Backend-Dependent)** |
| F-21 | **P3** | Dead Code / Deps / RTL | `MagicBento.tsx`, `Carousel.tsx`, unused `package.json` deps, physical CSS margins/text-align | Unused UI showcase components/packages and physical LTR CSS properties | **Open (Documented)** |
