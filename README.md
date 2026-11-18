# DVLD — Driver & Vehicle Licensing Department System

> - **Baseline Commit:** `e9e843e974af86a86003bcc364f0e7686c3faa87`
> - **Architecture:** ASP.NET Core 8 Web API (`DVLD.Api`) + SQL Server / EF Core 8 (`Infrastructure`) + React 19 SPA (`ClientApp`)

DVLD is a departmental driving-license management system implementing clean Onion Architecture on .NET 8 (`Domain`, `Application`, `Infrastructure`, `DVLD.Contracts`, `DVLD.Api`), automated test suites (`Test/`), and an independent TypeScript/Vite React single-page application (`ClientApp/`). This documentation reflects verified source code, configuration manifests, and scripts in the local repository.

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
          DVLD.Api (ASP.NET Core 8 — Controllers, RateLimiter, OnTokenValidated, RFC 7807)
              │
              v
         Application (Use-Case Services, Validators, Mappers, Result<T>, Abstractions)
              │
              v
       Infrastructure (EF Core 8, SQL Server Repositories, UnitOfWork, sp_getapplock, File Storage)
              │
              v
           Domain (Pure C# Entities & Enums — Zero External Dependencies)
```

---

## 3. Technology Stack Summary

For exact SDK, `.csproj` NuGet versions, `ClientApp/package.json` dependencies, and CI manifests, consult the **sole authoritative inventory** in [`TECHNOLOGY-STACK.md`](./docs/development/TECHNOLOGY-STACK.md).

- **Backend Runtime:** `.NET 8` (`net8.0`, SDK `8.0.427` in [`global.json`](./global.json)), C# 12, EF Core `8.0.x`, SQL Server, `Swashbuckle.AspNetCore` `6.6.2`, `BCrypt.Net-Next` `4.2.0`, `SkiaSharp` `4.154.0`.
- **Frontend Runtime:** Node.js `22.x` LTS (CI baseline), React `^19.3.1`, TypeScript `~6.0.3`, Vite `^8.3.4`, Tailwind CSS `^4.3.3`, Radix UI primitives, Vitest `^5.1.0`, Playwright `^1.65.0`.

> [!WARNING]
> **Support Lifecycle Notice:** Microsoft ended official support for **.NET 8** and **EF Core 8** on `2026-11-10`, and Node.js `20.x` reached EOL on `2026-04-30`. Upgrading to `.NET 10 LTS` is a tracked priority in [`ENGINEERING-ROADMAP.md`](./ENGINEERING-ROADMAP.md), **not** yet implemented.

---

## 4. Repository & Solution Structure

```text
D:\DVLD-System\
├─ DVLD.sln                              # Backend .NET solution (8 projects)
├─ global.json                           # Pinned .NET SDK (8.0.427)
├─ azure-pipelines.yml                   # Azure Pipelines (Frontend_CI & Backend_CI)
├─ API/DVLD.Api/                         # Web API composition root, controllers, security, background services
├─ Application/                          # Business services, DTOs, validators, options, Result<T>
├─ Domain/                               # Pure domain entities & enums
├─ Infrastructure/                       # EF Core 8 DbContext, migrations, repositories, UnitOfWork, file storage
├─ DVLD.Contracts/                       # Shared HTTP request/response contract DTOs
├─ ClientApp/                            # React 19 + TypeScript 6 + Vite 8 SPA & Vitest/Playwright tests
├─ Test/
│  ├─ Application.UnitTests/             # Database-free unit tests (41 C# files)
│  ├─ API.IntegrationTests/              # API contract, controller & workflow tests (58 C# files)
│  ├─ Infrastructure.IntegrationTests/   # Real SQL Server repository & concurrency tests (41 C# files)
│  └─ Shared/                            # Shared ephemeral SQL Server safety & ownership helper
├─ eng/scripts/                          # Pipeline & local SQL engine init/cleanup/reporting scripts
├─ scripts/testing/                      # Local SQL integration environment validator (-Setup)
└─ docs/                                 # Documentation hierarchy (client, development, reference, archive)
```

---

## 5. Prerequisites

1. **.NET 8 SDK (`8.0.427`):** Pinned in [`global.json`](./global.json) (`rollForward: latestFeature`).
2. **SQL Server:** Local SQL Server (`MSSQLSERVER`) or LocalDB with Windows Authentication (`Integrated Security=True`) and `sqlcmd` on `PATH`.
3. **Node.js & `npm`:** Node.js **`22.x` LTS** (matching [`azure-pipelines.yml`](./azure-pipelines.yml); Active LTS `24.x`/`26.x` also supported). On Windows PowerShell, use `npm.cmd` if script execution policy restricts `npm.ps1`.
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
- **Segregation of Duties (SoD Model B):** Operational licensing endpoints use `[Authorize(Policy = "StaffOnly")]`, `UsersController` and reference-data `PUT`s use `[Authorize(Policy = "AdminOnly")]`, and `PeopleController` uses `StaffOrAdmin`. *(Known UI mismatch: `ClientApp` `PAGE_PERMISSIONS['Person License History']` permits `['Staff', 'Admin']`, while the backend `LicensesController`/`InternationalLicensesController` enforce `StaffOnly` and return HTTP 403 for `Admin`; tracked under `SEC-01`/`RSK-12`.)*

---

## 9. Database Safety & Ephemeral Test DB Protocol

> [!CAUTION]
> **Protected Databases (`DVLD` / `DVLDf`):** Automated tests, migrations, and AI agents must **never** connect to, query, truncate, drop, or mutate `DVLD` or `DVLDf`. `SqlServerConnectionSafety.EnsureSafeForHost` fails fast if a test host targets a protected catalog.

- SQL integration suites require `DVLD_TEST_CONNECTION` (administrative `master` connection, Windows Authentication) and `DVLD_TEST_EXPECTED_INSTANCE` (`SERVERPROPERTY('ServerName')|@@SERVICENAME`). Configure per-session via `. .\scripts\testing\Test-DatabaseIntegrationEnvironment.ps1 -Setup`.
- Every test run generates an isolated database `DVLD_IntegrationTests_<GUID>` (or `DVLD_IntegrationTests_Api_<GUID>`) stamped with extended property `DVLD_Test_Run_Token`, `database_id`, and `service_broker_guid`, and verifies ownership before `DROP DATABASE`.

---

## 10. Automated Tests Inventory & Commands

*(Static discovery counts verified at `e9e843e`; automated suites were **not executed** during the documentation audit pass.)*

- **`Application.UnitTests`** (41 files; 842 `[Fact]`, 51 `[Theory]`):
  ```powershell
  dotnet test .\Test\Application.UnitTests\Application.UnitTests.csproj -c Release
  ```
- **`API.IntegrationTests`** (58 files; 466 `[Fact]`, 56 `[Theory]` — controller tests use mocked `ApiWebApplicationFactory`, workflow/cross-process tests require SQL env vars):
  ```powershell
  dotnet test .\Test\API.IntegrationTests\API.IntegrationTests.csproj -c Release --filter "FullyQualifiedName~Controllers"
  dotnet test .\Test\API.IntegrationTests\API.IntegrationTests.csproj -c Release --no-restore
  ```
- **`Infrastructure.IntegrationTests`** (41 files; 652 `[Fact]`, 38 `[Theory]` — requires SQL env vars):
  ```powershell
  dotnet test .\Test\Infrastructure.IntegrationTests\Infrastructure.IntegrationTests.csproj -c Release --no-restore
  ```
- **`ClientApp` Frontend Tests** (**9** Vitest unit files in `ClientApp/tests/unit/` + **1** Playwright spec `ClientApp/tests/e2e/login.spec.ts` + 16 screenshots):
  ```powershell
  npm.cmd --prefix ClientApp run test
  npm.cmd --prefix ClientApp run test:e2e
  ```

---

## 11. Continuous Integration (`azure-pipelines.yml`)

[`azure-pipelines.yml`](./azure-pipelines.yml) configures two mandatory stages on `windows-latest` (`CONFIGURED — NOT EXECUTED` locally):
1. **`Frontend_CI`** (Node.js `22.x`): runs `npm ci`, `npm run lint`, `tsc -b --pretty false`, and `npm run build`, publishing `clientapp-dist`. **Note:** Vitest (`npm run test`) and Playwright (`npm run test:e2e`) are **not** executed in CI.
2. **`Backend_CI`** (.NET `8.0.427`): restores/builds `DVLD.sln` (`Release`), runs `Application.UnitTests`, provisions an isolated LocalDB instance (`DVLD_CI_$(Build.BuildId)_$(System.JobAttempt)`), validates `SqlExpectedInstance` and `Jwt__SecretKey`, runs `API.IntegrationTests` and `Infrastructure.IntegrationTests`, cleans up owned DBs, publishes Cobertura coverage + TRX reports, and packages `dvld-api-release`.

---

## 12. Production Readiness Status & Governance Disclaimer

> [!IMPORTANT]
> **Not Yet Production-Ready:** Although core security (`AUTH-01`, `REST-01`, `TEST-03`, `CONCURRENCY-01`, `API-01`) and key domain workflows are implemented in source, the system is **strictly ineligible for Production release** until mandatory readiness gaps and approval gates are closed—including `.NET 8` EOS upgrade, ASP.NET Core Health Checks (`OBS-01`), structured logging (`OBS-03` / `SEC-04`), reverse-proxy & security header hardening (`DEC-DEP-01` / `DEC-DEP-02`), HTTP 429 test verification for `ChangePassword` (`SEC-03`), CI frontend test enforcement, formal risk acceptance for plaintext PII (`RSK-11` / Gate 8), and regulatory rulings (`DEC-POL-01` / `DEC-POL-02` / Gate 3). See [`ENGINEERING-ROADMAP.md`](./ENGINEERING-ROADMAP.md) §8.

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
