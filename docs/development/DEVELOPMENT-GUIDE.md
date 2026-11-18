# DVLD Developer Guide

> **Audience:** Software Engineers & Contributors  
> **Scope:** Local Environment Setup, Running Backend & Frontend, Testing, CI Pipeline, and Governance References  
> **Repository Root:** `D:\DVLD-System`

---

## 1. Prerequisites

Ensure the following tools are installed on your development machine:

1. **.NET 8 SDK (`8.0.427`):** Pinned in [`global.json`](../../global.json) (`rollForward: latestFeature`).
   > [!WARNING]
   > Microsoft ended official support for **.NET 8** and **EF Core 8** on **2026-11-10**. The commands below reflect the current checked-in manifests (`net8.0`). Upgrading to `.NET 10 LTS` is a planned priority; do not change target frameworks or packages without explicit approval.
2. **SQL Server:** Local SQL Server (`MSSQLSERVER` service) or SQL Server LocalDB with Windows Authentication (`Integrated Security=True`) and `sqlcmd` on `PATH`.
3. **Node.js & npm:** **Node.js `22.x` LTS** (configured in [`azure-pipelines.yml`](../../azure-pipelines.yml); Active LTS `24.x`/`26.x` are also supported; Node.js `20.x` reached EOL on `2026-04-30`). On Windows PowerShell, use `npm.cmd` if execution policy restricts `npm.ps1`.

---

## 2. Repository & Solution Structure

```text
D:\DVLD-System\
├─ DVLD.sln                       # Backend .NET solution
├─ global.json                    # Pinned .NET SDK (8.0.427)
├─ azure-pipelines.yml            # Frontend_CI & Backend_CI Azure Pipeline definition
├─ Domain/                        # Pure domain entities & enums (no external dependencies)
├─ Application/                   # Business use-case services, DTOs, validators, interfaces
├─ Infrastructure/                # EF Core 8, SQL Server repositories, UnitOfWork, file storage
├─ DVLD.Contracts/                # Shared HTTP request/response DTOs
├─ API/DVLD.Api/                  # ASP.NET Core Web API composition root & controllers
├─ ClientApp/                     # React 19 + TypeScript + Vite SPA
├─ Test/
│  ├─ Application.UnitTests/      # Database-free unit tests (xUnit + Moq)
│  ├─ API.IntegrationTests/       # API contract & workflow tests (WebApplicationFactory)
│  ├─ Infrastructure.IntegrationTests/ # Real SQL Server repository & concurrency tests
│  └─ Shared/                     # Shared SQL test environment & safety validators
├─ eng/scripts/                   # CI/local SQL provisioning, cleanup & report staging scripts
├─ scripts/testing/               # Local SQL test environment validation script
└─ docs/                          # Categorized documentation (client, development, reference, archive)
```

---

## 3. Running the Backend (`DVLD.Api`)

### 3.1 Configuration & Secrets
Before running `DVLD.Api`, ensure local configuration supplies:
- `ConnectionStrings:DVLDConnection` (configured in `API/DVLD.Api/appsettings.json` for local development).
- `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpirationMinutes` (defaults: `DVLD.Api`, `DVLD.Client`, `15` minutes in `appsettings.json`).
- `Jwt:SecretKey` (must be at least 32 UTF-8 bytes; configure via `.NET User Secrets` using `UserSecretsId` in `API/DVLD.Api/DVLD.Api.csproj` or environment variable `Jwt__SecretKey`, never commit secrets to Git).

### 3.2 Build & Run Commands
Run from the repository root (`D:\DVLD-System`):

```powershell
dotnet restore DVLD.sln
dotnet build DVLD.sln --configuration Release
dotnet run --project ".\API\DVLD.Api\DVLD.Api.csproj" --launch-profile https
```

- **HTTPS profile (Recommended Quick Start):** `https://localhost:7125;http://localhost:5260` (`--launch-profile https` in [`API/DVLD.Api/Properties/launchSettings.json`](../../API/DVLD.Api/Properties/launchSettings.json)), matching the default Vite dev proxy target (`https://localhost:7125`).
- **Local HTTPS Dev Certificate:** If HTTPS trust is not yet initialized on your workstation, run `dotnet dev-certs https --trust` once before launching.
- **HTTP-Only Alternative (`http://localhost:5260`):** `--launch-profile http` binds only `http://localhost:5260`. If you choose the HTTP profile, set `VITE_API_BASE_URL=http://localhost:5260/api` before starting `ClientApp`; otherwise `/api` proxy calls will fail trying to reach `https://localhost:7125`.
- **Swagger / OpenAPI:** Enabled automatically at `/swagger` when `ASPNETCORE_ENVIRONMENT=Development`.

---

## 4. Running the Frontend (`ClientApp`)

Start `DVLD.Api` first using `--launch-profile https`, then launch the Vite dev server (`http://localhost:5173`, matching the `"ReactApp"` CORS origin in `API/DVLD.Api/Program.cs`):

```powershell
cd ClientApp
npm install
npm run dev
```

- **Vite `/api` Proxy & `VITE_API_BASE_URL`:** [`ClientApp/src/services/apiClient.ts`](../../ClientApp/src/services/apiClient.ts) defaults `API_BASE_URL` to `'/api'`. In local development, [`ClientApp/vite.config.ts`](../../ClientApp/vite.config.ts) proxies `/api` to `https://localhost:7125` with `changeOrigin: true`. Override the target in other environments by setting `VITE_API_BASE_URL`.
- > [!WARNING]
  > **Local Dev Proxy Only (`secure: false` & Cookie Header Stripping):** [`ClientApp/vite.config.ts`](../../ClientApp/vite.config.ts) sets `secure: false` and strips `; Secure` from proxied `Set-Cookie` responses strictly so the `HttpOnly` refresh cookie (`dvld_rt`) functions over `http://localhost:5173`. This is **never** acceptable in Staging or Production—always terminate valid TLS certificates and retain `Secure` cookies in deployed environments.

Additional frontend scripts in [`ClientApp/package.json`](../../ClientApp/package.json) (on Windows PowerShell, use `npm.cmd` if script execution policy blocks `npm.ps1`):
- `npm run build` — Runs `tsc -b && vite build` into `ClientApp/dist/`.
- `npm run lint` — Runs ESLint across TypeScript/React source files.
- `npm run preview` — Serves the production build locally.
- `npm run test` — Runs Vitest unit/component tests (**9** test files in `ClientApp/tests/unit/`; not currently invoked in `azure-pipelines.yml`).
- `npm run test:e2e` — Runs Playwright E2E tests (**1** spec `ClientApp/tests/e2e/login.spec.ts` + 16 screenshots under `ClientApp/tests/screenshots/`; not currently invoked in `azure-pipelines.yml`).

See [`ClientApp/README.md`](../../ClientApp/README.md) and [`ClientApp/AGENTS.md`](../../ClientApp/AGENTS.md) for frontend-specific guidelines.

---

## 5. Running Automated Tests Safely

### 5.1 Database-Free Backend Tests
```powershell
# Application Unit Tests (no SQL Server required)
dotnet test .\Test\Application.UnitTests\Application.UnitTests.csproj -c Release

# API Controller Tests (mocked services via ApiWebApplicationFactory; no SQL Server required)
dotnet test .\Test\API.IntegrationTests\API.IntegrationTests.csproj -c Release --filter "FullyQualifiedName~Controllers"
```

### 5.2 SQL Server Integration Tests
> [!CAUTION]
> **Never** point any test runner or migration at `DVLD` or `DVLDf`. Integration tests enforce strict isolation (`DVLD_IntegrationTests_<GUID>`) and fail fast if configuration is missing or points to a protected catalog.

1. Configure session-scoped environment variables (`DVLD_TEST_CONNECTION` targeting `master` with Windows Authentication, and `DVLD_TEST_EXPECTED_INSTANCE` matching `SERVERPROPERTY('ServerName')|@@SERVICENAME`):
   ```powershell
   . .\scripts\testing\Test-DatabaseIntegrationEnvironment.ps1 -Setup
   ```
2. Run SQL-backed suites:
   ```powershell
   dotnet test .\Test\Infrastructure.IntegrationTests\Infrastructure.IntegrationTests.csproj -c Release --no-restore
   dotnet test .\Test\API.IntegrationTests\API.IntegrationTests.csproj -c Release --no-restore
   ```
3. Note: Running tests with `-c Release` prevents MSBuild file-lock errors (`MSB3027`/`MSB3021`) if `DVLD.Api` is running locally in `Debug` mode.

For complete test standards, ownership verification, and SQL Server troubleshooting, see [`TESTING-STANDARDS.md`](./TESTING-STANDARDS.md).

---

## 6. Continuous Integration (`azure-pipelines.yml`)

The Azure DevOps pipeline ([`azure-pipelines.yml`](../../azure-pipelines.yml)) defines two mandatory stages on `windows-latest`:
1. **`Frontend_CI`** (`timeoutInMinutes: 15`, Node.js `22.x`): runs `npm ci`, `npm run lint`, `npx --no-install tsc -b --pretty false`, and `npm run build`, then publishes `clientapp-dist`. *(Note: `npm run test` and `npm run test:e2e` are not currently invoked in `azure-pipelines.yml`.)*
2. **`Backend_CI`** (`timeoutInMinutes: 60`, .NET 8 SDK from `global.json`): restores and builds `DVLD.sln` (`Release`), executes `Application.UnitTests`, provisions an isolated LocalDB instance (`eng/scripts/Initialize-TestSqlEngine.ps1`), executes `API.IntegrationTests` and `Infrastructure.IntegrationTests`, runs ownership-verified cleanup (`eng/scripts/Cleanup-TestSqlEngine.ps1`), stages TRX/Cobertura reports (`eng/scripts/Stage-BackendTestReports.ps1`), and publishes `dvld-api-release`.

---

## 7. Authoritative Governance & Development Standards

Consult these single-source-of-truth documents before modifying architecture, security controls, or backlog items:

| Topic | Authoritative Document |
|---|---|
| **AI Agent Rules & Approval Gates** | [`AGENTS.md`](../../AGENTS.md) |
| **Backlog Priorities & Task Authorization** | [`ENGINEERING-ROADMAP.md`](../../ENGINEERING-ROADMAP.md) |
| **Architecture & Policy Decisions (ADRs)** | [`ENGINEERING-DECISIONS.md`](../../ENGINEERING-DECISIONS.md) |
| **Clean Architecture, UoW, SQL Locking & Code Rules** | [`ENGINEERING-STANDARDS.md`](./ENGINEERING-STANDARDS.md) |
| **Authentication, RBAC, SoD & Client Security** | [`SECURITY-STANDARDS.md`](./SECURITY-STANDARDS.md) |
| **Test Isolation, Ephemeral DBs & SQL Guide** | [`TESTING-STANDARDS.md`](./TESTING-STANDARDS.md) |
| **Verified SDK, NuGet, npm & CI Inventory** | [`TECHNOLOGY-STACK.md`](./TECHNOLOGY-STACK.md) |
| **Civic Licensing Business Rules** | [`LICENSING-DOMAIN-SPECIFICATION.md`](../reference/LICENSING-DOMAIN-SPECIFICATION.md) |
