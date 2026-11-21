# Frontend Optimization Report (`ClientApp/`)

Every implemented change is strictly scoped to `ClientApp/**`, preserves all existing business rules and API contracts, and is verified by `eslint`, `tsc -b && vite build`, and `vitest run`.

---

## 1. Auth & Request Lifecycle (`P0`)

- **[`src/services/apiClient.ts`](../src/services/apiClient.ts)**
  - **Auth lifecycle guard (`isAuthLifecyclePath`)**: `sendWithRefresh` no longer invokes `refreshSession()` or `unauthorizedHandler()` when `/Auth/login`, `/Auth/refresh`, `/Auth/logout`, or `/Auth/logout-all` returns `401`.
  - **`logoutAllSessions()`**: Uses `fetch` directly (mirroring `logoutSession`) so a `401` during logout-all cannot mint a replacement token via `/Auth/refresh`.
  - **Signal & timeout handling (`createRequestSignal`)**: Adds a `30_000ms` default request timeout while composing any caller `AbortSignal`, and preserves `AbortError` instead of converting aborted requests into `ApiError(0, 'Network error')`.
- **[`src/context/AuthContext.tsx`](../src/context/AuthContext.tsx)**
  - **`logoutAll()`**: Clears local session state and broadcasts `{ type: 'logout' }` immediately rather than waiting for the network request to settle.
- **[`src/hooks/useAsyncData.ts`](../src/hooks/useAsyncData.ts)**
  - **Awaitable `refresh()`**: Returns a `Promise<void>` that resolves only after the triggered `load()` finishes (or cleans up).
  - **Consistent `loading` state**: Returns `loading: enabled ? loading : false` so disabling `enabled` never leaves `loading` stuck `true`.

## 2. Animation & Rendering Performance (`P1`)

- **[`src/components/layout/Sidebar.tsx`](../src/components/layout/Sidebar.tsx)**
  - Replaced `mousePos` React state (`setMousePos` on every `pointermove` pixel) with CSS custom properties (`--sidebar-mouse-x`, `--sidebar-mouse-y`) written directly to the `<aside>` element, eliminating full Sidebar React re-renders on pointer movement while keeping the identical spotlight effect.
- **[`src/components/common/hooks/useStatCardEffects.ts`](../src/components/common/hooks/useStatCardEffects.ts)** & **[`src/components/ui/SpecularCard.tsx`](../src/components/ui/SpecularCard.tsx)**
  - Stopped continuous idle `requestAnimationFrame` loops when `near <= 0.005` (and `!active` on `SpecularCard`), eliminating ~30+ idle 60/120Hz loops on Dashboard and 15 on Test Appointments.
  - Replaced per-frame `getBoundingClientRect()` layout reads with a `ResizeObserver` + `scroll`-invalidated cached `DOMRect`.
  - Added `document.addEventListener('visibilitychange', ...)` to pause animations in background tabs.
- **[`src/components/ui/SpecularButton.tsx`](../src/components/ui/SpecularButton.tsx)**
  - Replaced the dependency-less `useEffect(() => { wakeRef.current?.(); })` with `[autoAnimate, disabled, followMouse]` so unrelated parent re-renders do not trigger WebGL2 redraws.
  - Added `visibilitychange` pausing and a `matchMedia('(prefers-reduced-motion: reduce)')` `change` listener that halts RAF drawing when reduced motion is enabled.

## 3. API Request Efficiency & Bundle Splitting (`P1`)

- **[`src/App.tsx`](../src/App.tsx)**
  - Converted `AppShell` to `React.lazy(() => import('./components/layout/AppShell'))` inside `<Suspense>`, removing `AppShell`, `Sidebar`, `Topbar`, `CommandPalette`, `ConfirmModal`, and all 7 route wrappers from the initial unauthenticated bundle.
- **[`src/services/countryService.ts`](../src/services/countryService.ts)**
  - Added in-memory resolved + in-flight deduplication cache for read-only `/Countries`.
- **[`src/components/common/PersonPhoto.tsx`](../src/components/common/PersonPhoto.tsx)** & **[`src/components/profile/MyProfilePage.tsx`](../src/components/profile/MyProfilePage.tsx)**
  - Added a bounded (`100` entries) in-memory `Blob` cache in `PersonPhoto` (`getCachedPhotoBlob`) to avoid duplicate downloads (e.g., `PersonDetailsPage` lightbox) and repeated table fetches; passed `hasPhoto={Boolean(profile.imagePath)}` in `MyProfilePage` to avoid 404 requests when no photo exists.
- **[`src/components/layout/router/PeopleRoutes.tsx`](../src/components/layout/router/PeopleRoutes.tsx)**
  - Removed the unused `await refreshPeople()` full-table `GET /People` call on person save (`PeoplePage` uses `getPeoplePage` directly).
- **[`src/components/test-appointments/TestAppointmentsPage.tsx`](../src/components/test-appointments/TestAppointmentsPage.tsx)**
  - Removed the duplicate `'all'` call from `countsFetcher` by capturing `allTabCount` from the main `useAsyncData` `onSuccess` callback.
- **[`src/components/local-applications/LocalApplicationsPage.tsx`](../src/components/local-applications/LocalApplicationsPage.tsx)**
  - Removed `pagination.resetPage()` from keystroke `onSearchChange` and avoided calling both `resetPage()` and `refresh()` when returning from form on `page > 1`.

## 4. Shared Hooks, DataTable & Accessibility (`P2`)

- **[`src/hooks/useTableFilters.ts`](../src/hooks/useTableFilters.ts)**: Stabilized default `initialFilters` with module-level `EMPTY_FILTERS`.
- **[`src/hooks/usePagination.ts`](../src/hooks/usePagination.ts)**: Moved `onPageChange` notification out of `setPage` state updaters into a `useEffect` so `StrictMode` does not double-invoke it.
- **[`src/components/common/DataTable.tsx`](../src/components/common/DataTable.tsx)**: Matched `isContextMenuOpen` by `getRowKey(...)` in addition to reference equality so row highlight survives data refreshes.
- **[`src/components/layout/Topbar.tsx`](../src/components/layout/Topbar.tsx)**: Changed `<h1 className="topbar-title">` to `<span className="topbar-title">` to prevent duplicate `<h1>` elements on pages with `<PageHero>`.
- **[`src/components/layout/CommandPalette.tsx`](../src/components/layout/CommandPalette.tsx)**: Added `Tab`/`Shift+Tab` focus trap, `document.body` scroll lock while open, and focus restoration to the previous element on close.
