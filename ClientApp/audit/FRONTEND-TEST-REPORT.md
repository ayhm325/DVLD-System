# Frontend Test Report (`ClientApp/`)

**Baseline Commit:** `45606d59c3b21d0ee9577d92f64c05a1c7375a63` (`development`)  
**Test Runner:** Vitest `v5.1.0` (`jsdom`), ESLint `10.15.0`, TypeScript `~6.0.3` (`tsc -b`)

---

## 1. Executed Checks & Outcomes

| Check | Command | Exit Code | Result |
|---|---|---|---|
| **ESLint** | `npm.cmd --prefix ClientApp run lint` | `0` | Passed (`0` errors, `0` warnings) |
| **TypeScript + Production Build** | `npm.cmd --prefix ClientApp run build` | `0` | Passed (`2295` modules, `1.17s`) |
| **Unit & Component Tests** | `npm.cmd --prefix ClientApp run test -- --run` | `0` | **22/22 test files passed (`117/117` tests)** |

---

## 2. Newly Added Regression Tests (`tests/unit/auditRegression.test.tsx`)

1. `does not trigger /Auth/refresh or unauthorizedHandler when POST /Auth/login returns 401`
2. `logoutAllSessions does not trigger /Auth/refresh on 401 response`
3. `preserves AbortError instead of masking it as Network error when caller signal aborts`
4. `useAsyncData.refresh waits until the refetch completes and clears loading when disabled`
5. `useTableFilters keeps resetFilters stable across renders when initialFilters is omitted`

---

## 3. Pre-Existing Failures & Non-Executed Suites

- **Pre-existing failures:** None (`0` failures on baseline `112/112` tests).
- **Not Executed:** Playwright E2E suite (`tests/e2e/login.spec.ts`, `tests/e2e/core-workflows.spec.ts`) — requires a live running `DVLD.Api` + SQL Server + Vite dev server, which is outside the offline sandboxed audit environment.
