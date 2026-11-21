# DVLD — Driver & Vehicle Licensing Department System

> - **Live Demo:** [https://dvld-demo-ayhm-2026-frcwbghpadcccxaj.uaenorth-01.azurewebsites.net/](https://dvld-demo-ayhm-2026-frcwbghpadcccxaj.uaenorth-01.azurewebsites.net/)
> - **Demo Login:** Username `ajad` · Password `12345678`
> - **Architecture:** ASP.NET Core 10 LTS Web API (`DVLD.Api`) + SQL Server / EF Core 10 (`Infrastructure`) + React 19 SPA (`ClientApp`)

DVLD is a departmental driving-license management system implementing clean Onion Architecture on .NET 10 LTS (`Domain`, `Application`, `Infrastructure`, `DVLD.Contracts`, `DVLD.Api`), automated test suites (`Test/`), and an independent TypeScript/Vite React single-page application (`ClientApp/`). This documentation reflects verified source code, configuration manifests, scripts, and local verification runs in the local repository.

---

## Live Demo Access

You can try the deployed application immediately at:

- **URL:** [https://dvld-demo-ayhm-2026-frcwbghpadcccxaj.uaenorth-01.azurewebsites.net/](https://dvld-demo-ayhm-2026-frcwbghpadcccxaj.uaenorth-01.azurewebsites.net/)
- **Username:** `ajad`
- **Password:** `12345678`

---

## 1. System Overview & Scope

The repository comprises a modular monolith backend serving an internal browser portal for departmental counter staff (`Staff`) and administrators (`Admin`). Citizens do not authenticate directly.

---

## 2. Architecture Overview

```text
  ClientApp (React 19 + TypeScript 6 + Vite 8 @ http://localhost:5173)
              │
              │ Vite /api proxy -> https://localhost:7125 (dev)
              │ HTTP + JSON + Bearer JWT (in-memory) + HttpOnly Cookie (dvld_rt @ /api/Auth)
              v
          DVLD.Api (ASP.NET Core 10 — Controllers, RateLimiter, OnTokenValidated, RFC 7807)
              │
              v
         Application (Use-Case Services, Validators, Mappers, Result<T>, Abstractions)
              │
              v
       Infrastructure (EF Core 10, SQL Server Repositories, UnitOfWork, sp_getapplock, File Storage)
              │
              v
           Domain (Pure C# Entities & Enums — Zero External Dependencies)
```

---

## 3. Technology Stack Summary

For exact SDK, `.csproj` NuGet versions, `ClientApp/package.json` dependencies, and CI manifests, consult the **sole authoritative inventory** in [`TECHNOLOGY-STACK.md`](./docs/development/TECHNOLOGY-STACK.md).

- **Backend Runtime:** `.NET 10 LTS` (`net10.0`, SDK `10.0.404` in [`global.json`](./global.json)), C# 14, EF Core `10.0.15`, ASP.NET Core `10.0.15`, SQL Server (`Microsoft.Data.SqlClient` `6.1.7`), `Swashbuckle.AspNetCore` `6.6.2`, `BCrypt.Net-Next` `4.2.0`, `SkiaSharp` `4.154.0`.
- **Frontend Runtime:** Node.js `26.x` Active LTS (`azure-pipelines.yml` CI baseline; locally verified on `v26.13.1` / `npm 11.20.0`), React `^19.3.1`, TypeScript `~6.0.3`, Vite `^8.3.4`, Tailwind CSS `^4.3.3`, Radix UI primitives, Vitest `^5.1.0`, Playwright `^1.65.0`.

> [!NOTE]
> **Runtime Support Lifecycle:** All backend projects target `.NET 10 LTS` (`net10.0`, supported through `2028-11-14`) with EF Core `10.0.15`, paired with `Node.js 26.x` Active LTS. Install SDK `10.0.404` via [`global.json`](./global.json) rather than `.NET 11 STS`.

---

## 4. Repository & Solution Structure

```text
D:\DVLD-System\
├─ DVLD.sln                              # Backend .NET solution (8 projects)
├─ global.json                           # Pinned .NET 10 SDK (10.0.404)
├─ azure-pipelines.yml                   # Azure Pipelines (Frontend_CI & Backend_CI)
├─ API/DVLD.Api/                         # Web API composition root, controllers, security, background services
├─ Application/                          # Business services, DTOs, validators, options, Result<T>
├─ Domain/                               # Pure domain entities & enums
├─ Infrastructure/                       # EF Core 10 DbContext, migrations, repositories, UnitOfWork, file storage
├─ DVLD.Contracts/                       # Shared HTTP request/response contract DTOs
├─ ClientApp/                            # React 19 + TypeScript 6 + Vite 8 SPA & Vitest/Playwright tests
├─ Test/
│  ├─ Application.UnitTests/             # Database-free unit tests (46 C# files)
│  ├─ API.IntegrationTests/              # API contract, controller & workflow tests (50 C# files)
│  ├─ Infrastructure.IntegrationTests/   # Real SQL Server repository & concurrency tests (45 C# files)
│  └─ Shared/                            # Shared ephemeral SQL Server safety & ownership helper
├─ eng/scripts/                          # Pipeline & local SQL engine init/cleanup/reporting scripts
├─ scripts/testing/                      # Local SQL integration environment validator (-Setup)
└─ docs/                                 # Documentation hierarchy (client, development, reference, archive)
```

---

## 5. Prerequisites

1. **.NET 10 SDK (`10.0.404`):** Pinned in [`global.json`](./global.json) (`rollForward: latestFeature`).
2. **SQL Server:** Local SQL Server (`MSSQLSERVER`) or LocalDB with Windows Authentication (`Integrated Security=True`) and `sqlcmd` on `PATH`.
3. **Node.js & `npm`:** Node.js **`26.x` Active LTS** (matching [`azure-pipelines.yml`](./azure-pipelines.yml); verified locally on `v26.13.1`). On Windows PowerShell, use `npm.cmd` if script execution policy restricts `npm.ps1`.
4. **ASP.NET Core Dev Certificate (one-time per workstation):** Run `dotnet dev-certs https --trust` so the local HTTPS profile (`https://localhost:7125`) operates cleanly.

---

## 6. Backend Setup & Run (`DVLD.Api`)

Configure `Jwt:SecretKey` (minimum 32 UTF-8 bytes) via User Secrets or environment variable (`Jwt__SecretKey`), then run from `D:\DVLD-System`:

```powershell
dotnet restore DVLD.sln
dotnet build DVLD.sln --configuration Release
dotnet run --project ".\API\DVLD.Api\DVLD.Api.csproj" --launch-profile https
```

- **HTTPS Profile (Recommended Quick Start):** `--launch-profile https` listens on `https://localhost:7125;http://localhost:5260`, matching the Vite dev proxy target (`https://localhost:7125`).
- **HTTP-Only Profile:** `--launch-profile http` binds only `http://localhost:5260`. Only use this if you also set `VITE_API_BASE_URL=http://localhost:5260/api` for `ClientApp`.
- **Swagger / OpenAPI:** Enabled at `/swagger` when `ASPNETCORE_ENVIRONMENT=Development`.

---

## 7. Frontend Setup & Run (`ClientApp`)

With `DVLD.Api` running under `--launch-profile https`:

```powershell
cd ClientApp
npm install
npm run dev
```

- **Local UI URL:** `http://localhost:5173` (matches `"ReactApp"` CORS policy in `API/DVLD.Api/Program.cs`).
- **Vite Proxy (`/api` -> `https://localhost:7125`):** `apiClient.ts` defaults `API_BASE_URL` to `'/api'`. In local dev, [`ClientApp/vite.config.ts`](./ClientApp/vite.config.ts) proxies `/api` to `https://localhost:7125` with `secure: false` and strips `; Secure` from `Set-Cookie` headers so the `dvld_rt` cookie works over `http://localhost:5173`. Set `VITE_API_BASE_URL` when targeting a non-proxied API origin.
- **Security Warning:** `secure: false` and `; Secure` header stripping in `vite.config.ts` are **local-development-only** settings and must **never** be used in Staging or Production.

---

## 8. Authentication, Session & RBAC Overview

- **Short-Lived JWT + Server Validation (`AUTH-01`):** `DVLD.Api` issues 15-minute HS256 JWT Bearer tokens carrying `sst` (security stamp) and `sid` (session GUID). `JwtBearerEvents.OnTokenValidated` queries SQL Server (`IAuthService.IsPrincipalCurrentAsync`) on every request, making token invalidation immediate on password/role/activation changes or logout.
- **Rotating Refresh Cookies:** 256-bit opaque refresh tokens are stored as Base64Url SHA-256 hashes in `UserSessions` (14-day absolute / 7-day idle lifetime, 30s grace window) and delivered strictly via `HttpOnly`, `SameSite=Lax`, `Secure`-when-HTTPS cookie `dvld_rt` scoped to `/api/Auth`, requiring `X-CSRF-Protection: 1` on cookie endpoints.
- **Frontend Token Storage:** Access tokens live in JS memory only (`authTokenStore.ts`). `localStorage` stores **only** `"dvld_remembered_username"` when "Remember me" is checked.
- **Segregation of Duties (SoD Model B):** Operational licensing endpoints use `[Authorize(Policy = "StaffOnly")]`, `UsersController` and reference-data `PUT`s use `[Authorize(Policy = "AdminOnly")]`, and `PeopleController` uses `StaffOrAdmin`. Client-side permissions in [`ClientApp/src/utils/permissions.ts`](./ClientApp/src/utils/permissions.ts) are aligned with Model B (`Person License History` restricted to `['Staff']` under `A-03` / `SEC-01`, rendering `AccessDenied` for `Admin`); `DEC-RBAC-01` remains unapproved.

---

## 9. Database Safety & Ephemeral Test DB Protocol

> [!CAUTION]
> **Protected Databases (`DVLD` / `DVLDf`):** Automated tests, migrations, and AI agents must **never** connect to, query, truncate, drop, or mutate `DVLD` or `DVLDf`. `SqlServerConnectionSafety.EnsureSafeForHost` fails fast if a test host targets a protected catalog.

- SQL integration suites require `DVLD_TEST_CONNECTION` (administrative `master` connection, Windows Authentication) and `DVLD_TEST_EXPECTED_INSTANCE` (`SERVERPROPERTY('ServerName')|@@SERVICENAME`). Configure per-session via `. .\scripts\testing\Test-DatabaseIntegrationEnvironment.ps1 -Setup`.
- Every test run generates an isolated database `DVLD_IntegrationTests_<GUID>` (or `DVLD_IntegrationTests_Api_<GUID>`) stamped with extended property `DVLD_Test_Run_Token`, `database_id`, and `service_broker_guid`, and verifies ownership before `DROP DATABASE`.

---

## 10. Automated Tests Inventory & Commands

*(Verified in the current local workspace on `.NET 10.0.404` / `Node v26.13.1`: `Application.UnitTests` passed `980/980`, database-free `API.IntegrationTests` passed `574/574`, and `ClientApp` Vitest passed `117/117` across `22` unit test files with `eslint` and `tsc -b && vite build` passing. SQL-backed `Infrastructure.IntegrationTests` and `API.IntegrationTests` `Auth`/`Workflows` require `DVLD_TEST_CONNECTION` and `DVLD_TEST_EXPECTED_INSTANCE`.)*

- **`Application.UnitTests`** (46 files):
  ```powershell
  dotnet test .\Test\Application.UnitTests\Application.UnitTests.csproj -c Release
  ```
- **`API.IntegrationTests`** (50 files — controller/smoke/security/result tests use mocked `ApiWebApplicationFactory`, workflow/cross-process tests require SQL env vars):
  ```powershell
  dotnet test .\Test\API.IntegrationTests\API.IntegrationTests.csproj -c Release --filter "FullyQualifiedName~Controllers"
  dotnet test .\Test\API.IntegrationTests\API.IntegrationTests.csproj -c Release --no-restore
  ```
- **`Infrastructure.IntegrationTests`** (45 files — requires SQL env vars):
  ```powershell
  dotnet test .\Test\Infrastructure.IntegrationTests\Infrastructure.IntegrationTests.csproj -c Release --no-restore
  ```
- **`ClientApp` Frontend Tests** (**22** Vitest unit files in `ClientApp/tests/unit/` + **2** Playwright specs `ClientApp/tests/e2e/login.spec.ts` and `core-workflows.spec.ts`):
  ```powershell
  npm.cmd --prefix ClientApp run test
  npm.cmd --prefix ClientApp run test:e2e
  ```

---

## 11. Continuous Integration (`azure-pipelines.yml`)

[`azure-pipelines.yml`](./azure-pipelines.yml) configures two mandatory stages on `windows-latest` (`CONFIGURED` locally; live Azure DevOps execution requires a hosted pipeline run):
1. **`Frontend_CI`** (Node.js `26.x`, `timeoutInMinutes: 25`): runs `npm ci`, two-tier `npm audit` (blocking production `--omit=dev` gate + full advisory report artifact `clientapp-npm-audit`), `npm run lint`, `npx --no-install tsc -b --pretty false`, Vitest (`npm run test` with JUnit reporting), Chromium installation + Playwright E2E (`npm run test:e2e` with `CI=true`, JUnit reporting, and `clientapp-playwright-reports`), and `npx --no-install vite build`, publishing `clientapp-dist`.
2. **`Backend_CI`** (.NET `10.0.404`): enforces a `10.x` SDK, fail-closed NuGet vulnerability scan (`dotnet list DVLD.sln package --vulnerable --include-transitive`, artifact `backend-nuget-audit`), restores/builds `DVLD.sln` (`Release`), runs `Application.UnitTests`, provisions an isolated LocalDB instance (`DVLD_CI_$(Build.BuildId)_$(System.JobAttempt)`), validates `SqlExpectedInstance` and `Jwt__SecretKey`, runs `API.IntegrationTests` and `Infrastructure.IntegrationTests`, cleans up owned DBs, enforces non-empty fail-closed report staging (`Stage-BackendTestReports.ps1`), publishes Cobertura coverage + TRX reports, and packages `dvld-api-release`.

---

## 12. Production Readiness Status & Governance Disclaimer

> [!IMPORTANT]
> **Not Yet Production-Ready:** Although `.NET 10 LTS` (`net10.0` / `10.0.404`), core security (`AUTH-01`, `REST-01`, `TEST-03`, `CONCURRENCY-01`, `API-01`), full local unit/integration/E2E verification, and key domain workflows are implemented in source, the system is **strictly ineligible for Production release** until mandatory readiness gaps and approval gates are closed—including hosted Azure DevOps verification (`G-02`), ASP.NET Core Health Checks (`OBS-01`), structured logging (`OBS-03` / `SEC-04`), reverse-proxy & security header hardening (`DEC-DEP-01` / `DEC-DEP-02`), HTTP 429 test verification for `ChangePassword` (`SEC-03`), dev-dependency `picomatch` advisory tracking (`G-05`), formal risk acceptance for plaintext PII (`RSK-11` / Gate 8), and regulatory rulings (`DEC-POL-01` / `DEC-POL-02` / Gate 3). See [`ENGINEERING-ROADMAP.md`](./ENGINEERING-ROADMAP.md) §8.

---

## 13. Documentation Map

All documentation adheres to the single-source-of-truth hierarchy indexed under [`docs/README.md`](./docs/README.md):

| Category / Purpose | Entry Point |
|---|---|
| **Central Documentation Index** | [`docs/README.md`](./docs/README.md) |
| **End-User & Staff Guide (`docs/client/`)** | [`docs/client/USER-GUIDE.md`](./docs/client/USER-GUIDE.md) |
| **Developer Setup & Run Guide** | [`docs/development/DEVELOPMENT-GUIDE.md`](./docs/development/DEVELOPMENT-GUIDE.md) |
| **System Architecture, UoW & Concurrency Standards** | [`docs/development/ENGINEERING-STANDARDS.md`](./docs/development/ENGINEERING-STANDARDS.md) |
| **Authentication, RBAC/SoD & Security Standards** | [`docs/development/SECURITY-STANDARDS.md`](./docs/development/SECURITY-STANDARDS.md) |
| **Test Isolation, Ephemeral DBs & SQL Guide** | [`docs/development/TESTING-STANDARDS.md`](./docs/development/TESTING-STANDARDS.md) |
| **Verified SDK, NuGet, npm & CI Inventory** | [`docs/development/TECHNOLOGY-STACK.md`](./docs/development/TECHNOLOGY-STACK.md) |
| **Civic Licensing Domain Rules** | [`docs/reference/LICENSING-DOMAIN-SPECIFICATION.md`](./docs/reference/LICENSING-DOMAIN-SPECIFICATION.md) |
| **Frontend Portal Documentation** | [`ClientApp/README.md`](./ClientApp/README.md) and [`ClientApp/AGENTS.md`](./ClientApp/AGENTS.md) |
| **AI Engineering Governance** | [`AGENTS.md`](./AGENTS.md) |
| **Engineering Backlog, Authorization & Readiness** | [`ENGINEERING-ROADMAP.md`](./ENGINEERING-ROADMAP.md) |
| **Architecture & Policy Decisions (ADRs)** | [`ENGINEERING-DECISIONS.md`](./ENGINEERING-DECISIONS.md) |
| **Historical Audits & Superseded Plans** | [`docs/archive/README.md`](./docs/archive/README.md) |
