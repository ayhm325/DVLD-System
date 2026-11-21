# Testing Guide

## 1. Test Suite Architecture

The repository includes automated test suites across both backend (.NET 10) and frontend (`ClientApp/`):

| Suite | Location | File Count / Scope |
|---|---|---|
| **Backend Unit Tests** | `Test/Application.UnitTests/` | 46 `.cs` files testing services, validators, and domain rules without SQL Server. |
| **API Integration Tests** | `Test/API.IntegrationTests/` | 50 `.cs` files using `ApiWebApplicationFactory` (DB-free) and `SqlServerApiWebApplicationFactory` (SQL-backed). |
| **Infrastructure Integration Tests** | `Test/Infrastructure.IntegrationTests/` | 45 `.cs` files verifying EF Core mappings, migrations, SQL locks, and `UnitOfWork` against SQL Server. |
| **Frontend Unit Tests** | `ClientApp/tests/unit/` | 22 test files (114 test cases) running on Vitest + `@testing-library/react` + `jsdom`. |
| **Frontend E2E Tests** | `ClientApp/tests/e2e/` | 2 Playwright spec files (`login.spec.ts`, `core-workflows.spec.ts`, 8 test cases) running on Chromium with `@axe-core/playwright`. |

---

## 2. Isolated SQL Server Integration Test Lifecycle

`Test/Shared/SqlServerTestEnvironment.cs` enforces strict isolation so integration tests never touch developer or production databases:

- **Required Environment Variables**:
  - `DVLD_TEST_CONNECTION`: Target administrative/test SQL Server connection string.
  - `DVLD_TEST_EXPECTED_INSTANCE`: Expected SQL Server instance name for safety verification.
- **Safety Guards**:
  - Explicitly rejects databases named `DVLD` or `DVLDf` with no fallback.
  - Generates ephemeral per-run databases named `DVLD_IntegrationTests_<GUID>` or `DVLD_IntegrationTests_Api_<GUID>`.
  - Stamps `DVLD_Test_Run_Token` ownership metadata and verifies `database_id` and `service_broker_guid` before teardown (`DisposeAsync`) drops the ephemeral database.

---

## 3. Running Tests Locally

### Backend Unit & Integration Tests

```powershell
# Unit tests (no SQL Server required)
dotnet test Test/Application.UnitTests/Application.UnitTests.csproj

# Integration tests (requires DVLD_TEST_CONNECTION & DVLD_TEST_EXPECTED_INSTANCE)
dotnet test Test/Infrastructure.IntegrationTests/Infrastructure.IntegrationTests.csproj
dotnet test Test/API.IntegrationTests/API.IntegrationTests.csproj
```

### Frontend Unit & E2E Tests

```powershell
cd ClientApp

# Vitest unit suite
npm test

# Lint & TypeScript build check
npm run lint
npm run build

# Playwright E2E suite (starts Vite dev server automatically)
npm run test:e2e
```

---

## Implementation References

- Backend test projects: [`Test/Application.UnitTests/Application.UnitTests.csproj`](../../Test/Application.UnitTests/Application.UnitTests.csproj), [`Test/API.IntegrationTests/API.IntegrationTests.csproj`](../../Test/API.IntegrationTests/API.IntegrationTests.csproj), [`Test/Infrastructure.IntegrationTests/Infrastructure.IntegrationTests.csproj`](../../Test/Infrastructure.IntegrationTests/Infrastructure.IntegrationTests.csproj)
- SQL test isolation: [`Test/Shared/SqlServerTestEnvironment.cs`](../../Test/Shared/SqlServerTestEnvironment.cs), [`Test/API.IntegrationTests/Infrastructure/ApiWebApplicationFactory.cs`](../../Test/API.IntegrationTests/Infrastructure/ApiWebApplicationFactory.cs), [`Test/API.IntegrationTests/Infrastructure/SqlServerApiWebApplicationFactory.cs`](../../Test/API.IntegrationTests/Infrastructure/SqlServerApiWebApplicationFactory.cs)
- Frontend test configs: [`ClientApp/vitest.config.ts`](../../ClientApp/vitest.config.ts), [`ClientApp/playwright.config.ts`](../../ClientApp/playwright.config.ts)
