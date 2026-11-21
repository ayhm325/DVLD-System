# Frontend Inventory (`ClientApp/`)

**Baseline Commit:** `45606d59c3b21d0ee9577d92f64c05a1c7375a63` (`development`)  
**Audit Date:** 2026-11-21  
**Stack:** React `19.3.1`, TypeScript `~6.0.3` (`strict: true`), Vite `8.3.4` (`@vitejs/plugin-react` `6.1.2`), Tailwind CSS `4.3.3` (`@tailwindcss/vite`), Radix UI primitives, `lucide-react` `1.65.0`, Vitest `5.1.0`, Playwright `1.65.0`.

---

## 1. Root Configuration & Entry Points

| File | Purpose | Key Settings |
|---|---|---|
| `ClientApp/index.html` | SPA HTML shell | `<html lang="en">`, mounts `#root`, loads `/src/main.tsx` |
| `ClientApp/package.json` | Manifest & scripts | `dev`, `build` (`tsc -b && vite build`), `lint` (`eslint .`), `preview`, `test` (`vitest run`), `test:e2e` |
| `ClientApp/vite.config.ts` | Vite bundler & dev proxy | `tailwindcss()`, `react()`, `@` -> `./src` alias, `/api` proxy to `https://localhost:7125` |
| `ClientApp/tsconfig.app.json` | App TS config | `target: "es2023"`, `strict: true`, `noUnusedLocals: true`, `noUnusedParameters: true`, `erasableSyntaxOnly: true` |
| `ClientApp/eslint.config.js` | ESLint 10 flat config | `@eslint/js`, `typescript-eslint`, `eslint-plugin-react-hooks`, `eslint-plugin-react-refresh` |
| `ClientApp/src/main.tsx` | React entry point | `<StrictMode><AuthProvider><NavigationProvider><App /></NavigationProvider></AuthProvider></StrictMode>` |
| `ClientApp/src/App.tsx` | Auth gate & shell coordinator | Renders `LoadingSpinner`, `<Login />`, or `<AppShell />` inside `<AppErrorBoundary scope="app">` |

---

## 2. Context Providers & State Management (`src/context/`)

- **Authentication (`AuthContext.tsx`, `authContextInstance.ts`, `useAuth.ts`):**
  - Stores `accessToken` strictly in module memory (`src/utils/authTokenStore.ts`), never in `localStorage`/`sessionStorage`.
  - Restores session on startup via HttpOnly refresh cookie (`POST /api/Auth/refresh`).
  - Coordinates silent token renewal, `visibilitychange` checks, and cross-tab sync via `BroadcastChannel('dvld-auth')`.
- **Navigation (`NavigationContext.tsx`, `navigationContextInstance.ts`, `useNavigation.ts`):**
  - Custom `window.history` (`pushState`/`replaceState`/`popstate`) router mapping 26 `PageName` keys to URL paths (`PAGE_TO_PATH`) and sanitized query parameters.
- **People (`PeopleContext.tsx`, `peopleContextInstance.ts`, `usePeopleData.ts`):**
  - Wraps `<AppRouter />` inside `AppShell.tsx` and provides `people`, `refreshPeople`, etc. (Note: `PeoplePage.tsx` uses `getPeoplePage` directly rather than `PeopleContext`.)

---

## 3. Navigation, Shell & Lazy-Loading Boundaries (`src/components/layout/`)

- **`AppShell.tsx`**: Renders `Sidebar`, `Topbar`, `Footer`, `ConfirmModal`, `CommandPalette` (`Ctrl/Cmd+K`), and `PeopleProvider > AppRouter`.
- **`AppRouter.tsx`**: Enforces RBAC via `isPagePermitted(activePage, userRole)`, resolves one of 7 route groups (`routeConfig.ts`), and wraps the active group in `<AppErrorBoundary scope="route">` and `<Suspense fallback={<LoadingSpinner size="lg" />}>`.
- **Route Groups (`src/components/layout/router/`)**:
  1. `DashboardRoutes.tsx` — `DashboardPage` (lazy)
  2. `UserRoutes.tsx` — `UsersPage` (lazy)
  3. `PeopleRoutes.tsx` — `PeoplePage`, `AddPersonPage`, `EditPersonPage`, `PersonDetailsPage`, `DriversPage`, `LicenseHistoryPage` (lazy)
  4. `LicenseRoutes.tsx` — `LicensesPage`, `InternationalLicensesPage`, `IssueInternationalLicensePage`, `LicenseRenewalReplacementPage`, `LicenseDetentionPage` (lazy)
  5. `AppointmentRoutes.tsx` — `LocalApplicationsPage`, `TestAppointmentsPage`, `AppointmentDetailsPage`, `ManageTestAppointmentsPage`, `TakeExaminationPage`, `ScheduleAppointmentPage` (lazy)
  6. `ReferenceDataRoutes.tsx` — `CountriesPage`, `ApplicationTypesPage`, `LicenseClassesPage`, `TestTypesPage` (lazy)
  7. `AccountRoutes.tsx` — `MyProfilePage`, `ChangePasswordPage` (lazy)

---

## 4. API Communication & Feature Services (`src/services/`)

- **Core Client (`apiClient.ts`)**: `apiRequest<T>`, `apiBlobRequest`, `ApiError`, `UnauthorizedError`, single-flight `refreshSession()` with `authGeneration` guard and `409` retry.
- **14 Feature Services**: `authService.ts`, `dashboardService.ts`, `peopleService.ts`, `driverService.ts`, `userService.ts`, `licenseService.ts`, `internationalLicenseService.ts`, `detainedLicenseService.ts`, `localDrivingLicenseApplicationService.ts`, `testAppointmentService.ts`, `countryService.ts`, `applicationTypeService.ts`, `licenseClassService.ts`, `testTypeService.ts`.

---

## 5. Custom Hooks (`src/hooks/`)

- `useAsyncData.ts`: Async data loader with `AbortController`, stale-response guard, `refresh()`, `loading`, and `error`.
- `usePagination.ts`: Page & page-size state (`PAGE_SIZE_OPTIONS = [10, 20, 60, 80, 100]`).
- `useDebounce.ts`: Debounced value hook.
- `useTableFilters.ts`: Client-side search + filter state hook.
- `useDashboardStatistics.ts`: Dashboard statistics loader.
- `useModal.ts`: Modal open/close helper.

---

## 6. Shared UI & Testing Architecture

- **Shared Components (`src/components/common/` & `src/components/ui/`)**: `DataTable`, `DataTableContainer`, `DataTableRowContextMenu`, `useRowContextMenu`, `PaginationControls`, `TableToolbar`, `SearchInput`, `PageHero`, `StatCard` + `useStatCardEffects`, `StatsGrid`, `StatusBadge`, `PersonPhoto`, `ModalShell`, `ConfirmModal`, `ErrorAlert`, `EmptyState`, `LoadingSpinner`, `AppErrorBoundary`, `AccessDenied`, `SpecularCard`, `SpecularButton`, `button`, `card`, `dialog`, `dropdown-menu`, `select`, `input`, `textarea`, `label`, `badge`.
- **Unused UI / Assets**: `src/components/ui/MagicBento.tsx` (1,664 lines), `src/components/ui/Carousel.tsx` (963 lines), `src/assets/hero.png`, `public/icons.svg`, and unused `package.json` packages (`motion`, `@fontsource-variable/*`, `@radix-ui/react-checkbox|separator|switch|tooltip`, `@testing-library/user-event`).
- **Tests (`tests/`)**: 21 Vitest unit/component test files (`tests/unit/*.test.{ts,tsx}`) and 2 Playwright E2E specs (`tests/e2e/login.spec.ts`, `tests/e2e/core-workflows.spec.ts`).
