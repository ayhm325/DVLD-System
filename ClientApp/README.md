# DVLD — React Frontend (`ClientApp`)

> - **Live Demo:** [https://dvld-demo-ayhm-2026-frcwbghpadcccxaj.uaenorth-01.azurewebsites.net/](https://dvld-demo-ayhm-2026-frcwbghpadcccxaj.uaenorth-01.azurewebsites.net/) (`ajad` / `12345678`)
> - **Directory:** `D:\DVLD-System\ClientApp`
> - **Backend API:** ASP.NET Core 10 LTS Web API (`DVLD.Api`)

---

## 1. Overview & Purpose

`ClientApp` is the browser-based React + TypeScript + Vite single-page application for internal staff (`Staff`) and administrators (`Admin`) of the Driver & Vehicle Licensing Department (DVLD). It communicates with `DVLD.Api` over HTTP + JSON + Bearer JWT (`in-memory`) paired with an `HttpOnly` refresh cookie (`dvld_rt`, `credentials: 'include'`).

For repository-wide backend architecture, NuGet manifests, and governance policies, follow the authoritative documents under `../docs/development/` rather than duplicating version tables here.

---

## 2. Frontend Technology Stack Summary

Exact declared versions live in [`ClientApp/package.json`](./package.json) and are governed by [`TECHNOLOGY-STACK.md`](../docs/development/TECHNOLOGY-STACK.md):

- **UI Runtime:** `react` `^19.3.1`, `react-dom` `^19.3.1`
- **Language:** `typescript` `~6.0.3` (`tsconfig.app.json`: target `ES2023`, alias `@/*`, `strict: true`, `verbatimModuleSyntax: true`, `noUnusedLocals`, `noUnusedParameters`, `erasableSyntaxOnly`)
- **Bundler & CSS:** `vite` `^8.3.4`, `tailwindcss` `^4.3.3`, `@tailwindcss/vite` `^4.3.3`
- **UI Primitives & Animation:** `@radix-ui/react-*`, `lucide-react` `^1.65.0`, `motion` `^14.9.0`, `@fontsource-variable/{fraunces,inter,jetbrains-mono}`
- **Testing & Linting (Dev):** `vitest` `^5.1.0`, `@testing-library/react` `^16.3.3`, `@playwright/test` `^1.65.0`, `@axe-core/playwright` `^4.14.0`, `eslint` `^10.15.0`

---

## 3. Prerequisites

1. **Node.js & `npm`:** **Node.js `26.x` Active LTS** (configured in [`azure-pipelines.yml`](../azure-pipelines.yml); verified locally on `v26.13.1` / `npm 11.20.0`). On Windows PowerShell, use `npm.cmd` if execution policy restricts `npm.ps1`.
2. **Running `DVLD.Api` Backend:** Start `DVLD.Api` from the repository root (`D:\DVLD-System`) using `--launch-profile https` so it matches the default Vite `/api` proxy (`https://localhost:7125`):
   ```powershell
   dotnet run --project ".\API\DVLD.Api\DVLD.Api.csproj" --launch-profile https
   ```
   Run `dotnet dev-certs https --trust` once per workstation if HTTPS trust is not yet configured.

---

## 4. Environment, API Proxy & CORS Configuration

- **`API_BASE_URL` ([`src/services/apiClient.ts`](./src/services/apiClient.ts)):** Defaults to `'/api'` when `VITE_API_BASE_URL` is unset.
- **Vite Dev Server & Proxy ([`vite.config.ts`](./vite.config.ts)):** Listens on `http://localhost:5173` (`host: '0.0.0.0'`, `port: 5173`, `strictPort: true`), matching the `"ReactApp"` CORS policy (`AllowCredentials()`) in `API/DVLD.Api/Program.cs`. Proxies `/api` to `https://localhost:7125`.
- **Using `--launch-profile http` (`http://localhost:5260`) Instead:** If running the API over HTTP only, set `$env:VITE_API_BASE_URL = "http://localhost:5260/api"` before `npm run dev`; otherwise `/api` proxy requests fail connecting to `https://localhost:7125`.
- **Local Dev Proxy Warning:** `vite.config.ts` sets `secure: false` and strips `; Secure` from proxied `Set-Cookie` headers solely so browsers over local HTTP (`http://localhost:5173`) send `dvld_rt`. This workaround is **strictly for local development and must never be used in Production**.

---

## 5. Setup & Available npm Scripts

Run commands inside `ClientApp/` (`ClientApp/package.json`):

```powershell
cd ClientApp

npm install                # Install dependencies
npm run dev                # Start Vite dev server (http://localhost:5173)
npm run lint               # Run ESLint across TS/React files
npm run build              # Run tsc -b && vite build -> dist/
npm run preview            # Preview built dist/ locally
npm run test               # Run Vitest unit/component tests (tests/unit/)
npm run test:e2e           # Run Playwright E2E suite (tests/e2e/)
npm run test:e2e:update    # Update Playwright visual snapshot PNGs
```

---

## 6. Project Folder Architecture

```text
ClientApp/
├─ vite.config.ts                # Dev server (:5173) & local /api -> https://localhost:7125 proxy
├─ vitest.config.ts              # Vitest + jsdom configuration (tests/unit/**/*.test.{ts,tsx})
├─ playwright.config.ts          # Playwright Chromium config (tests/e2e + tests/screenshots)
├─ tsconfig.app.json             # App TS config (ES2023, @/* alias, verbatimModuleSyntax)
├─ src/
│  ├─ components/                # 18 feature/UI folders (auth, common, layout, router, ui, domain pages)
│  ├─ context/                   # AuthContext & NavigationContext (29 routes + query persistence)
│  ├─ hooks/                     # Custom data/UI hooks (useAsyncData, useDashboardStatistics, etc.)
│  ├─ lib/                       # Utility helpers
│  ├─ services/                  # apiClient.ts + 14 domain API service modules
│  ├─ types/                     # TypeScript contract interfaces matching DVLD.Contracts
│  └─ utils/                     # authTokenStore.ts, sessionStorage.ts, permissions.ts, formatters.ts
└─ tests/
   ├─ setup.ts                   # Vitest global test setup
   ├─ unit/                      # 22 Vitest unit/component test files (117 tests)
   ├─ e2e/                       # 2 Playwright E2E specs (login.spec.ts, core-workflows.spec.ts)
   └─ screenshots/               # Chromium & phase1-signoff snapshot PNGs
```

---

## 7. Routing & Navigation System

- **29 Registered Routes (`PAGE_TO_PATH` in [`src/context/navigationContextInstance.ts`](./src/context/navigationContextInstance.ts)):** Maps `Dashboard` (`/`) through `Change Password` (`/change-password`), including 7 entity-dependent detail/workflow routes (`Person Details`, `Edit Person`, `Person License History`, `Appointment Details`, `Manage Test Appointments`, `Take Examination`, `Schedule Appointment`).
- **Lazy Code Splitting (`src/components/layout/router/`):** Routes are grouped across `DashboardRoutes`, `UserRoutes`, `PeopleRoutes`, `LicenseRoutes`, `AppointmentRoutes`, `ReferenceDataRoutes`, and `AccountRoutes`.
- **Shell Navigation:** Role-filtered sidebar in [`Sidebar.tsx`](./src/components/layout/Sidebar.tsx) + [`sidebarNavigation.ts`](./src/components/layout/sidebar/sidebarNavigation.ts), topbar in [`Topbar.tsx`](./src/components/layout/Topbar.tsx), and global Command Palette (`Ctrl + K`) in [`CommandPalette.tsx`](./src/components/layout/CommandPalette.tsx).

---

## 8. Role-Based Access Control (RBAC) & UX Boundaries

- **Authoritative Client Map ([`src/utils/permissions.ts`](./src/utils/permissions.ts)):** `isPagePermitted(page, role)` fails closed for unknown roles/pages; [`AppRouter.tsx`](./src/components/layout/AppRouter.tsx) renders `<AccessDenied />` for unauthorized destinations.
- **Role Split (`Admin` vs `Staff`):**
  - `User Management` (`/users`) -> `Admin` only.
  - `Drivers`, `Person License History`, `Local Applications`, `Test Appointments` & sub-workflows, `Licenses`, `Renew & Replace License`, `License Detention`, `International Licenses` -> `Staff` only.
  - `People`, `Add Person`, `Edit Person`, `Person Details` -> shared `StaffOrAdmin` (no license-history navigation actions exposed on shared People views).
  - `Application Types`, `License Classes`, `Test Types` -> viewable by both, inline editing (`PUT`) restricted to `Admin` via `canManageReferenceData`.
  - `Countries` -> permanently read-only for all roles (`canManageReferenceData` returns `false`).
- **RBAC Alignment (`A-03` / `SEC-01` / `RSK-12`):** `PAGE_PERMISSIONS['Person License History']` is aligned to `['Staff']` (matching backend `LicensesController` and `InternationalLicensesController` `[Authorize(Policy = "StaffOnly")]`). Direct navigation to `/licenses/history` by `Admin` renders `<AccessDenied />` before `LicenseHistoryPage` mounts; `DEC-RBAC-01` (Admin read-only operational license access) remains unapproved, and backend `[Authorize(...)]` policies remain the sole security boundary.

---

## 9. Authentication, Token Storage & Multi-Tab Sync

- **Access Token (`src/utils/authTokenStore.ts`):** Held strictly in JavaScript memory (`currentAccessToken`). Never persisted to `localStorage` or `sessionStorage`.
- **Refresh Token (`AuthContext.tsx`, `apiClient.ts`):** Managed via `HttpOnly`, `SameSite=Lax`, `Secure`-when-HTTPS cookie `dvld_rt` scoped to `/api/Auth`, exchanged via `POST /api/Auth/refresh` with `credentials: 'include'` and `X-CSRF-Protection: 1`.
- **Minimal `localStorage` Usage:** `purgeLegacyTokenStorage()` (`src/utils/sessionStorage.ts`) removes legacy keys (`access_token`, `dvld_token`, `dvld_auth_session`) on startup. The **only** key stored in `localStorage` is `dvld_remembered_username` (`Login.tsx`) when "Remember me" is checked.
- **Cross-Tab Sync & Verified Logout:** `BroadcastChannel('dvld-auth')` synchronizes `login`, `logout`, and `token-refreshed` events across browser tabs (`IMPLEMENTED`). `logoutSession()` returns a typed `LogoutResult` inspected by `AuthContext.logout()`, warning the user if server-side revocation fails while clearing local memory state immediately. `AppErrorBoundary` guards both the root application and individual lazy-loaded route groups.
- **Two-Step `ChangePassword` Recovery (`ChangePasswordPage.tsx`):** If session establishment fails after the password change commits, form inputs are cleared and the user is signed out to re-authenticate with the new password.

---

## 10. Shared UI Components & Styling System

- Built with Tailwind CSS v4 utility classes, dark-navy theme tokens (`tests/unit/designTokens.test.ts`), and accessible Radix UI primitives under `src/components/ui/` (`dialog`, `dropdown-menu`, `select`, `checkbox`, `switch`, `tooltip`, `button`, `card`).
- Reusable composite primitives under `src/components/common/` include `DataTable`, `DataTableContainer`, `DataTableRowContextMenu`, `TableToolbar`, `StatusBadge`, `StatCard`, `StatsGrid`, `PageHero`, `ErrorAlert`, `EmptyState`, `ConfirmModal`, and `ModalShell`.

---

## 11. Data Fetching, API Services & Custom Hooks

- **Services (`src/services/`):** Centralized `apiRequest<T>()` / `apiBlobRequest()` in `apiClient.ts` plus 14 domain service files (`authService`, `userService`, `peopleService`, `driverService`, `licenseService`, `internationalLicenseService`, `detainedLicenseService`, `localDrivingLicenseApplicationService`, `testAppointmentService`, `applicationTypeService`, `licenseClassService`, `testTypeService`, `countryService`, `dashboardService`).
- **Hooks (`src/hooks/`):** `useAsyncData`, `useDashboardStatistics`, `usePagination`, `useTableFilters`, `useDebounce`, `useModal`.

---

## 12. HTTP Error Handling & RFC 7807 `ProblemDetails`

- `apiClient.ts` parses all backend `application/problem+json` failures into typed `ApiError`, `UnauthorizedError` (401), and `ForbiddenError` (403) instances carrying `fieldErrors` and `traceId`.
- Concurrent 401 responses share one single-flight `refreshSession()` promise guarded by `authGeneration`, retrying a 409 rotation race once after `250 ms` before triggering fatal logout handling.

---

## 13. Automated Frontend Tests & Verification Status

`ClientApp/tests/` contains **22 Vitest unit/component test files** (`117` passing tests) and **2 Playwright E2E specs** (`login.spec.ts` and `core-workflows.spec.ts`).

- `npm.cmd --prefix ClientApp run lint` (`eslint .`): `0` errors, `0` warnings.
- `npm.cmd --prefix ClientApp run build` (`tsc -b && vite build`): passes with strict TypeScript checking and lazy-loaded `AppShell` + 7 route-group chunks.
- `npm.cmd --prefix ClientApp run test -- --run`: `22/22` test files (`117/117` tests) pass.
- `npm.cmd --prefix ClientApp run test:e2e`: configured in `playwright.config.ts` and `azure-pipelines.yml` (`Frontend_CI`), requires a live `DVLD.Api` + SQL Server backend.

*(In [`azure-pipelines.yml`](../azure-pipelines.yml), stage `Frontend_CI` runs `npm ci`, two-tier `npm audit` [blocking `--omit=dev` gate + full report artifact], `npm run lint`, `tsc -b --pretty false`, Vitest + JUnit reporting, Playwright Chromium install + `npm run test:e2e` with `CI=true`, and `npx --no-install vite build`.)*

---

## 14. Production Build & Hosting Requirements

- Running `npm run build` emits static assets to `ClientApp/dist/`.
- **Reverse Proxy, HTTPS & CSP (`DEC-DEP-01` / `DEC-DEP-02`):** Because `DVLD.Api` serves pure JSON without HTML CSP headers, the Production SPA web host or reverse proxy must emit Content-Security-Policy (`SECURITY-STANDARDS.md` §13.4), TLS certificates, and trusted proxy headers (`KnownProxies`/`KnownNetworks`). Never deploy Vite's local dev `secure: false` proxy or `; Secure` cookie stripping to Production.

---

## 15. Related Documentation

- **Frontend AI Rules:** [`ClientApp/AGENTS.md`](./AGENTS.md)
- **Repository AI Governance:** [`AGENTS.md`](../AGENTS.md)
- **Developer Setup & Workflow:** [`DEVELOPMENT-GUIDE.md`](../docs/development/DEVELOPMENT-GUIDE.md)
- **End-User Portal Manual:** [`USER-GUIDE.md`](../docs/client/USER-GUIDE.md)
- **Engineering & Layering Standards:** [`ENGINEERING-STANDARDS.md`](../docs/development/ENGINEERING-STANDARDS.md)
- **Security & Session Standards:** [`SECURITY-STANDARDS.md`](../docs/development/SECURITY-STANDARDS.md)
- **Testing Standards:** [`TESTING-STANDARDS.md`](../docs/development/TESTING-STANDARDS.md)
- **Technology Stack & Versions:** [`TECHNOLOGY-STACK.md`](../docs/development/TECHNOLOGY-STACK.md)
- **Licensing Domain Rules:** [`LICENSING-DOMAIN-SPECIFICATION.md`](../docs/reference/LICENSING-DOMAIN-SPECIFICATION.md)
- **Task Priorities, Backlog & Production Readiness:** [`ENGINEERING-ROADMAP.md`](../ENGINEERING-ROADMAP.md)
