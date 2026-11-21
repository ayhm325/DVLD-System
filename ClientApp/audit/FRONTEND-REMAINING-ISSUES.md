# Frontend Remaining Issues & Recommendations (`ClientApp/`)

## 1. Backend-Dependent Recommendations (Read-Only Assessment)
1. **Global Summary Endpoints for Paged Modules:**
   - `DriversPage`, `DetainedLicensesPage`, `InternationalLicensesPage`, and `TestAppointmentsPage` compute sub-card KPIs from the current page (`10` rows) because no `/summary` endpoint exists for those modules (unlike `LocalDrivingLicenseApplicationsController` `[HttpGet("summary")]`).
   - **Recommendation:** Add lightweight `GET /api/TestAppointments/summary`, `GET /api/DetainedLicenses/summary`, `GET /api/InternationalLicenses/summary`, and `GET /api/Drivers/summary` endpoints so KPI cards and `TestAppointmentsPage` tab counts can load in a single SQL aggregate query.
2. **Batch / URL-based Person Photos:**
   - Tables (`PeopleTable`, `UsersTable`, `EligiblePersonPicker`) still issue N individual `/People/{id}/photo` requests for rows with photos (now cached in memory after first load). Returning signed/cacheable image URLs or `Cache-Control` headers from the API would further reduce blob fetches.

## 2. Frontend Items Left Open / Low-Risk Cleanup
1. **End-to-End `AbortSignal` Forwarding:**
   - `useAsyncData` creates an `AbortController` and `apiClient.ts` now preserves `AbortError` and enforces a 30s timeout, but 13 of 14 feature services do not yet forward `signal` to `apiRequest`.
2. **Cross-Tab Token Sync (`AuthContext.tsx:L119-136`):**
   - `token-refreshed` updates `expiresAtUtc` without updating `accessToken`, and `login` broadcasts trigger concurrent `/Auth/refresh` requests across all open tabs (risking 409 rotation races).
3. **Table Column Sorting Wiring:**
   - `AppointmentTable.tsx` and `TestAppointmentsTable.tsx` mark columns `sortable: true` without passing `onSort` to `<DataTable>`.
4. **Unused Dependencies & Dead Files (`package.json` & `src/components/ui/`):**
   - Unused packages in `package.json`: `motion`, `@fontsource-variable/{fraunces,inter,jetbrains-mono}`, `@radix-ui/react-{checkbox,separator,switch,tooltip}`, `@testing-library/user-event` (not removed to respect package-change governance).
   - Unused UI files/assets: `MagicBento.tsx` (1,664 lines), `Carousel.tsx` (963 lines), `src/assets/hero.png`, `public/icons.svg` (tree-shaken out of production bundle).
5. **RTL Logical CSS & Tablist Arrow-Key Navigation:**
   - `App.css`, `Sidebar.css`, `Topbar.css`, `SearchInput.css`, and several Tailwind classes still use physical `left`/`right` properties; `LicenseDetentionPage` and `LicenseRenewalReplacementPage` tablists can add `ArrowLeft`/`ArrowRight` roving focus.
6. **Live E2E & Lighthouse Profiling:**
   - Playwright E2E (`tests/e2e/`) and live Lighthouse Core Web Vitals were not executed against a running backend in this offline sandbox.
