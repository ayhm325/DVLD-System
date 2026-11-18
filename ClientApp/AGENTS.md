# AI Agent Engineering Instructions — DVLD ClientApp

> **Authority Level:** Frontend Component Instruction  
> **Parent Governance:** [`../AGENTS.md`](../AGENTS.md) *(sole general AI engineering governance — Git safety, DB protection, approval gates, and verification rules apply here without exception)*  
> **Project Root:** `D:\DVLD-System\ClientApp`  
> **Governance Status:** ACTIVE & ENFORCED

---

## 1. Overview & Frontend Scope

This file defines **frontend-specific** architecture conventions and quality gates for `ClientApp/`, the React + TypeScript + Vite SPA communicating with `DVLD.Api` over HTTP + JSON + JWT (`/api` proxied to `https://localhost:7125` in local development via `vite.config.ts`, or configured via `VITE_API_BASE_URL`). General repository-wide agent rules (Git safety, protected databases, approval gates, evidence standards, and 10-step workflow) live exclusively in [`../AGENTS.md`](../AGENTS.md).

---

## 2. Authoritative Documentation References

1. [`../AGENTS.md`](../AGENTS.md) — Universal repository AI governance, safety gates, and workflow.
2. [`./README.md`](./README.md) — `ClientApp` prerequisites, scripts, and setup commands.
3. [`../docs/development/ENGINEERING-STANDARDS.md`](../docs/development/ENGINEERING-STANDARDS.md) — System architecture, API contracts, and persistence rules.
4. [`../docs/development/SECURITY-STANDARDS.md`](../docs/development/SECURITY-STANDARDS.md) — Authentication, `AUTH-01` session lifecycle, RBAC/SoD, and client token storage rules.
5. [`../docs/reference/LICENSING-DOMAIN-SPECIFICATION.md`](../docs/reference/LICENSING-DOMAIN-SPECIFICATION.md) — Authoritative civic licensing business rules.

---

## 3. Frontend Architecture & Coding Standards

1. **Authentication & Browser Storage (`Login.tsx`, `AuthContext.tsx`, `authTokenStore.ts`):**
   - Active login portal is implemented in `src/components/auth/Login.tsx` and `Login.css`.
   - The access token is held strictly in memory (`authTokenStore.ts`); the refresh token is held only in an `HttpOnly` cookie and is never readable from JavaScript.
   - `localStorage` stores **only** the optional remembered username (`dvld_remembered_username` in `Login.tsx`), never authentication tokens.
2. **Authenticated Application Shell:**
   - Root coordinator in `src/App.tsx`; page routing and RBAC guards in `src/components/layout/AppRouter.tsx` and `src/utils/permissions.ts`.
   - Command palette (`Ctrl+K`) in `src/components/layout/CommandPalette.tsx`.
   - Role-filtered navigation sidebar in `src/components/layout/Sidebar.tsx` and `sidebarNavigation.ts`; topbar in `src/components/layout/Topbar.tsx`.
3. **Shared Design System (`src/components/common/` & `src/components/ui/`):**
   - Reuse shared components (`DataTable`, `DataTableContainer`, `TableToolbar`, `StatusBadge`, `StatCard`, `PageHero`, `ErrorAlert`, `EmptyState`, `ConfirmModal`, `ModalShell`) built on Radix UI (`@radix-ui/react-*`) and Tailwind CSS v4.
4. **TypeScript & Compiler Rules (`tsconfig.app.json`):**
   - Target `ES2023`, `DOM`; path alias `@/*` maps to `./src/*`.
   - `verbatimModuleSyntax: true` — always use `import type` for type-only imports.
   - Enforce `noUnusedLocals`, `noUnusedParameters`, `erasableSyntaxOnly`, and `noFallthroughCasesInSwitch`. (Note: `tsconfig.app.json` does not currently set `"strict": true`.)
5. **Data Fetching & Custom Hooks (`src/hooks/`):**
   - Encapsulate stateful operations in dedicated hooks (`useDashboardStatistics`, `useAsyncData`, `usePagination`, `useTableFilters`, `useDebounce`).
6. **HTTP Transport Client (`src/services/apiClient.ts`):**
   - Route all API calls through `apiRequest<T>()` / `apiBlobRequest()`; never invoke `fetch()` directly from UI components.
   - Normalizes RFC 7807 `ProblemDetails` into typed `ApiError`, `UnauthorizedError`, and `ForbiddenError` instances.
   - Handles single-flight 401 refresh with bounded 409 retry and triggers `setUnauthorizedHandler()` on fatal auth failure.

---

## 4. Frontend Boundaries & Quality Gates

- **Scope Confinement:** When executing a frontend-only task, keep edits inside `ClientApp/` and do **not** modify backend C# code (`API/`, `Application/`, `Infrastructure/`, `Domain/`, `DVLD.Contracts/`), database migrations, or backend authorization attributes without explicit authorization.
- **Dependency Discipline:** Inspect `ClientApp/package.json` directly; never install, upgrade, or remove npm packages without explicit user approval.
- **Verification Commands (run when executing frontend code changes):**
  ```powershell
  npm.cmd --prefix ClientApp run build
  npm.cmd --prefix ClientApp run lint
  npm.cmd --prefix ClientApp run test
  git diff --check
  ```
