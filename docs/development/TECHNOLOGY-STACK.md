# Technology Stack & Dependency Inventory

> **Authority Level:** Authoritative Technical Inventory  
> **Parent Document:** [`AGENTS.md`](../../AGENTS.md)  
> **Repository Root:** `D:\DVLD-System`
>
> **Last Reviewed:** 2026-11-18 (Local Manifest Inspection at `HEAD` `e9e843e974af86a86003bcc364f0e7686c3faa87`)

---

## 1. Overview and Principles

This document is the **sole authoritative inventory** for runtime environments, SDKs, libraries, NuGet/npm manifests, and CI/CD toolchains verified directly from local files in `D:\DVLD-System` (`global.json`, `*.csproj`, `ClientApp/package.json`, `ClientApp/tsconfig.app.json`, and `azure-pipelines.yml`). AI agents and engineers must **not** upgrade, downgrade, or add packages without explicit written approval and an approved Architecture Decision Record.

> [!NOTE]
> **Evidence & Canonical Status Vocabulary:** Throughout active DVLD documentation, declared manifests (`*.csproj`, `package.json`, `azure-pipelines.yml`) establish configuration presence only—never runtime verification. Active docs classify features using nine canonical states: `IMPLEMENTED`, `IMPLEMENTED — RUNTIME UNVERIFIED`, `PARTIALLY IMPLEMENTED`, `CONFIGURED — NOT EXECUTED`, `PLANNED`, `PROPOSED`, `NOT AUTHORIZED`, `NOT IMPLEMENTED`, and `NEEDS VERIFICATION`.

> [!NOTE]
> **Runtime Support Lifecycle (`.NET 10 LTS` & `Node.js 26.x` Active LTS):**
>
> - **.NET / EF Core:** All 8 backend and test projects target `net10.0` (`.NET 10 LTS`, supported through `2028-11-14`) and pin SDK `10.0.404` in `global.json`, with EF Core `10.0.15` and ASP.NET Core `10.0.15`.
> - **Node.js:** `azure-pipelines.yml` configures `nodeVersionSpec: '26.x'` (Node.js 26 Active LTS, verified locally on `v26.13.1` / `npm 11.20.0`). Node.js `20.x` reached EOL (`2026-04-30`) and is **unsupported**.

---

## 2. Verified Backend Technology Stack (.NET & C#)

### 2.1 Core Runtime & SDK
- **.NET SDK Version:** `10.0.404` (`rollForward: latestFeature`, pinned in [`global.json`](../../global.json)).
- **Target Framework:** `net10.0` across all 8 backend projects (`Domain`, `Application`, `Infrastructure`, `DVLD.Contracts`, `DVLD.Api`, `Application.UnitTests`, `API.IntegrationTests`, `Infrastructure.IntegrationTests`).
- **Language Settings:** C# 14 (default for .NET 10 SDK; `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` enabled across projects).
- **Central Package Management:** No `Directory.Packages.props` file currently exists in the repository (even though `azure-pipelines.yml` includes path triggers for it); package versions are declared per `.csproj`.

### 2.2 Backend Application Package Inventory (Exact `.csproj` Declarations)

| Package / Library | Declared Version | Manifest File | Layer / Project | Purpose / Role | Notes |
|---|---|---|---|---|---|
| **Microsoft.AspNetCore.App** | Framework Reference (`10.0`) | Implicit (`Microsoft.NET.Sdk.Web`) | `DVLD.Api` | Web API hosting, routing, middleware, controllers | Active (`.NET 10 LTS`) |
| **Microsoft.AspNetCore.Authentication.JwtBearer** | `10.0.15` | `API/DVLD.Api/DVLD.Api.csproj` | `DVLD.Api` | JWT bearer authentication & request validation | Aligned (`10.0.15`) |
| **Serilog.AspNetCore** | `10.0.0` | `API/DVLD.Api/DVLD.Api.csproj` | `DVLD.Api` | Structured diagnostic logging, request logging, Console & File sinks | Composition root only (`net10.0` aligned) |
| **Serilog.Sinks.Seq** | `9.1.0` | `API/DVLD.Api/DVLD.Api.csproj` | `DVLD.Api` | Structured diagnostic-event sink for Seq | Composition root only; optional at runtime |
| **Swashbuckle.AspNetCore** | `6.6.2` | `API/DVLD.Api/DVLD.Api.csproj` | `DVLD.Api` | OpenAPI 3.0 spec & Swagger UI (Development) | Verified compatible on `net10.0` (`ApiSmokeTests`) |
| **Microsoft.EntityFrameworkCore.Design** | `10.0.15` | `API/DVLD.Api/DVLD.Api.csproj`, `Infrastructure/Infrastructure.csproj` | `DVLD.Api`, `Infrastructure` | EF Core design-time tooling (`PrivateAssets=all`) | Aligned (`10.0.15`) |
| **BCrypt.Net-Next** | `4.2.0` | `Application/Application.csproj` | `Application` | Password hashing (WorkFactor = 11) | Active |
| **SkiaSharp** | `4.154.0` | `Application/Application.csproj` | `Application` | Server-side image validation & WebP resizing | Active |
| **Microsoft.Extensions.Logging.Abstractions** | `10.0.15` | `Application/Application.csproj` | `Application` | `ILogger<T>` logging abstractions | Aligned (`10.0.15` on `net10.0`) |
| **Microsoft.Extensions.Options** | `10.0.15` | `Application/Application.csproj` | `Application` | Strongly-typed options abstractions | Aligned (`10.0.15` on `net10.0`) |
| **Microsoft.EntityFrameworkCore** | `10.0.15` | `Infrastructure/Infrastructure.csproj` | `Infrastructure` | ORM data access & change tracking | Aligned (`10.0.15`) |
| **Microsoft.EntityFrameworkCore.SqlServer** | `10.0.15` | `Infrastructure/Infrastructure.csproj` | `Infrastructure` | SQL Server provider for EF Core (pulls `Microsoft.Data.SqlClient` `6.1.7` + `Microsoft.Identity.Client.Broker` `4.84.2`) | Aligned (`10.0.15`) |
| **Microsoft.EntityFrameworkCore.Tools** | `10.0.15` | `Infrastructure/Infrastructure.csproj` | `Infrastructure` | EF Core CLI/Package Manager tools (`PrivateAssets=all`) | Aligned (`10.0.15`) |

---

## 3. Verified Frontend Technology Stack (`ClientApp/`)

The web frontend resides in `ClientApp/` and is built independently using Node.js and `npm` (`ClientApp/package.json`, `ClientApp/package-lock.json`).

### 3.1 Core Runtime, Language & Tooling
- **Package Manager:** `npm` (`ClientApp/package-lock.json` present for deterministic CI `npm ci`).
- **Node.js Version:** `26.x` Active LTS configured in [`azure-pipelines.yml`](../../azure-pipelines.yml) (`nodeVersionSpec: '26.x'`, locally verified on `v26.13.1` / `npm 11.20.0`); Node.js `20.x` reached EOL on `2026-04-30` and is unsupported.
- **Note on `npm audit`:** `npm audit --omit=dev --audit-level=high --json` reports **`0` vulnerabilities** (`0 critical, 0 high, 0 moderate, 0 low`). Full `npm audit --audit-level=critical --json` reports `0 critical`, `0 moderate`, and `12` devDependency-only `high` advisories propagation from `picomatch <=4.0.7` (`GHSA-fc5q-39xc-vjpf`) across `vite`/`vitest`/`typescript-eslint` (latest npm `picomatch` version is `4.0.7`; tracked under `G-05` / `SEC-PKG-14`).
- **UI Library:** `react` `^19.3.1`, `react-dom` `^19.3.1`.
- **Language / Compiler:** `typescript` `~6.0.3`. In [`ClientApp/tsconfig.app.json`](../../ClientApp/tsconfig.app.json), target is `es2023` (`lib: ["ES2023", "DOM"]`), `moduleResolution: "bundler"`, and safety flags `noUnusedLocals`, `noUnusedParameters`, `noFallthroughCasesInSwitch`, `verbatimModuleSyntax`, and `erasableSyntaxOnly` are `true`; however, `"strict": true` is **not** explicitly declared.
- **Bundler & Dev Server:** `vite` `^8.3.4` (`@vitejs/plugin-react` `^6.1.2`). In [`ClientApp/vite.config.ts`](../../ClientApp/vite.config.ts), the local dev server listens on `0.0.0.0:5173` (`strictPort: true`) and proxies `/api` to `https://localhost:7125` (`secure: false` and `; Secure` cookie attribute stripping are local-dev-only conveniences, never suitable for Production).
- **Styling Engine:** `tailwindcss` `^4.3.3` with `@tailwindcss/vite` `^4.3.3` (declared in `devDependencies` as build-time CSS compiler plugins).

### 3.2 Key Frontend Runtime & Dev Dependencies (`ClientApp/package.json`)

| Package | Declared Version | Role | Scope |
|---|---|---|---|
| **@radix-ui/react-*** (`checkbox`, `dialog`, `dropdown-menu`, `label`, `select`, `separator`, `slot`, `switch`, `tooltip`) | `^1.2.0` – `^2.4.1` | Accessible UI primitives | Runtime |
| **lucide-react** | `^1.65.0` | SVG iconography | Runtime |
| **motion** | `^14.9.0` | UI animations and transitions | Runtime |
| **clsx** / **tailwind-merge** / **class-variance-authority** | `^2.1.1` / `^3.7.0` / `^0.7.1` | Conditional class composition & variant styling | Runtime |
| **@fontsource-variable/{fraunces,inter,jetbrains-mono}** | `^5.3.0` | Self-hosted variable typography | Runtime |
| **eslint** / **@eslint/js** / **typescript-eslint** | `^10.15.0` / `^10.0.1` / `^8.73.0` | TypeScript & React static linting | Dev |
| **eslint-plugin-react-hooks** / **eslint-plugin-react-refresh** | `^7.1.1` / `^0.5.7` | React hooks & Fast Refresh rules | Dev |
| **vitest** / **@testing-library/react** / **@testing-library/jest-dom** / **@testing-library/user-event** / **jsdom** | `^5.1.0` / `^16.3.3` / `^7.0.1` / `^14.6.8` / `^31.0.0` | Frontend unit & component test stack | Dev |
| **@playwright/test** / **@axe-core/playwright** | `^1.65.0` / `^4.14.0` | End-to-end browser & accessibility testing | Dev |

---

## 4. Test Frameworks & Tooling

### 4.1 Backend Test Projects (`Test/`)

*(Static inventory inspected at `e9e843e`: `Application.UnitTests` has 41 C# files with 842 `[Fact]` and 51 `[Theory]` attributes [141 `[InlineData]` rows]; `Infrastructure.IntegrationTests` has 41 C# files with 652 `[Fact]` and 38 `[Theory]` attributes [87 `[InlineData]` rows]; `API.IntegrationTests` has 58 C# files with 466 `[Fact]` and 56 `[Theory]` attributes [161 `[InlineData]` rows]; `Test/Shared/` has 1 helper file. Attribute counts reflect static source declarations, not current-pass test execution.)*

| Package | `Application.UnitTests` | `Infrastructure.IntegrationTests` | `API.IntegrationTests` | Role |
|---|---:|---:|---:|---|
| **xunit** | `2.9.2` | `2.9.2` | `2.9.2` | Primary unit/integration test framework |
| **xunit.runner.visualstudio** | `2.8.2` | `2.8.2` | `2.8.2` | VSTest / `dotnet test` adapter |
| **Microsoft.NET.Test.Sdk** | `17.12.0` | `17.12.0` | `17.12.0` | MSBuild test host infrastructure |
| **coverlet.collector** | `6.0.2` | `6.0.2` | `6.0.2` | Cobertura code coverage collector |
| **Moq** | `4.20.72` | — | `4.20.72` | Interface mocking |
| **Microsoft.AspNetCore.Mvc.Testing** | — | — | `10.0.15` | `WebApplicationFactory` in-memory/test hosting |
| **Microsoft.EntityFrameworkCore.SqlServer** | — | `10.0.15` | (via `Infrastructure`) | Ephemeral SQL Server test execution |

### 4.2 Frontend Test Suites (`ClientApp/tests/`)
- **Unit & Component Tests:** `vitest` `^5.1.0`, `@testing-library/react` `^16.3.3`, `@testing-library/jest-dom` `^7.0.1`, `@testing-library/user-event` `^14.6.8`, and `jsdom` `^31.0.0` configured in [`ClientApp/vitest.config.ts`](../../ClientApp/vitest.config.ts) (`setupFiles: ['./tests/setup.ts']`) with **9 test files** under `ClientApp/tests/unit/`.
- **End-to-End (E2E) Tests:** `@playwright/test` `^1.65.0` and `@axe-core/playwright` `^4.14.0` configured in [`ClientApp/playwright.config.ts`](../../ClientApp/playwright.config.ts) with **1 spec file** (`ClientApp/tests/e2e/login.spec.ts`) and 16 visual snapshot PNGs under `ClientApp/tests/screenshots/`.

| Test Tier | Package Declared | Config Present | Discovered Files (`e9e843e`) | Executed on Node `26.13.1` | Runtime Pass Proof | Included in `azure-pipelines.yml` |
|---|:---:|:---:|---|---|---|:---:|
| **Vitest Unit/Component (`npm run test`)** | `IMPLEMENTED` | `IMPLEMENTED` | **9** `.test.ts(x)` files (+ `tests/setup.ts`) | **Yes** (`46/46` passed across `9/9` files) | `VERIFIED` (local JUnit + Hosted Run `20261118.5` [`690aec48`] Succeeded) | **Yes** (`Frontend_CI` + JUnit) |
| **Playwright E2E (`npm run test:e2e`)** | `IMPLEMENTED` | `IMPLEMENTED` | **1** `login.spec.ts` (+ 16 screenshots) | **Yes** (`8/8` passed in local & `CI=true` modes) | `VERIFIED` (local `CI=true` + Hosted Run `20261118.5` [`690aec48`] Succeeded) | **Yes** (`Frontend_CI` + Chromium + JUnit) |

---

## 5. Continuous Integration & Pipeline Configuration (`azure-pipelines.yml`)

- **Platform & Image:** Azure Pipelines running on `windows-latest` (**HOSTED VERIFIED** on branch `main` at commit `690aec48`; `G-01` and `G-02` closed, while non-blocking dev-dependency advisory tracking remains open under `G-05`).
- **Stage 1 — `Frontend_CI` (`timeoutInMinutes: 25`):**
  - Installs Node.js `26.x`, restores npm cache, runs `npm ci --no-fund`, enforces two-tier fail-closed `npm audit` (`--omit=dev --audit-level=high --json` blocking production `high`/`critical` and malformed reports + `--audit-level=critical --json` blocking critical advisories and registry/JSON/metadata errors while logging dev-only `high`/`moderate`/`low` warnings; stages `npm-audit-prod.json` & `npm-audit-full.json` to `clientapp-npm-audit`), runs `npm run lint`, `npx --no-install tsc -b --pretty false`, Vitest (`npm run test` with JUnit output + `PublishTestResults@2`), Chromium install (`npx --no-install playwright install chromium`) + Playwright E2E (`npm run test:e2e` with `CI=true`, JUnit reporting, HTML/trace `clientapp-playwright-reports`), and `npx --no-install vite build`, then publishes `clientapp-dist`.
- **Stage 2 — `Backend_CI` (`timeoutInMinutes: 60`):**
  - Installs `.NET 10 SDK` (`10.0.404`) from `global.json` and fails if resolved major SDK is not `10.x`, runs fail-closed `dotnet list DVLD.sln package --vulnerable --include-transitive` (`backend-nuget-audit`), restores and builds `DVLD.sln` (`Release`), runs `Application.UnitTests`, initializes an isolated run-specific LocalDB instance (`eng/scripts/Initialize-TestSqlEngine.ps1`), validates `SqlExpectedInstance` and `Jwt__SecretKey`, executes `API.IntegrationTests` and `Infrastructure.IntegrationTests`, runs ownership-verified cleanup (`eng/scripts/Cleanup-TestSqlEngine.ps1` with `condition: always()`), stages TRX/Cobertura reports with fail-closed suite/prerequisite enforcement (`eng/scripts/Stage-BackendTestReports.ps1`), and publishes `dvld-api-release`.

### Hosted Verification Evidence
- **Pipeline Run:** `20261118.5` (`ayhm325/DVLD-System`, branch `main`)
- **Commit:** `690aec48` (Full SHA: `690aec488e16e16617aa68bacd6fb8232fe0f008`) — verified on the revision containing fail-closed `npm audit` gate hardening (`66654e0`) and the 13 `xUnit2031` test analyzer warning fixes (`690aec48`).
- **Pipeline Status:** **Succeeded** (**Total Duration:** `19m 53s`)
  - **`Frontend_CI`:** **Succeeded** — `3m 22s`
  - **`Backend_CI` (`Backend & Solution CI`):** **Succeeded** — `16m 08s`
- **Tests:** `100%` Passed
- **Code Coverage:** `94.26%`
- **Published Artifacts:** `7`
- **Warnings:** `1` (non-blocking development dependency advisory warning; see below)
- **Prior Historical Run (`20261118.3`):** Executed earlier on branch `main` at commit `15730709` (`Frontend_CI`: Succeeded `5m 15s`, `Backend_CI`: Succeeded `16m 27s`, total `22m 04s`, `100%` tests passed, `94.28%` code coverage, `7` artifacts), now superseded as current evidence by Run `20261118.5` on commit `690aec48`.

#### Remaining Non-Blocking CI Warning (`Audit Frontend npm Dependencies`)
- **Warning Message:** `Full npm audit detected non-critical development/tooling advisories (high=12, moderate=0, low=0).` — emitted deliberately by the full dev-dependency audit tier and **not** a build error or test failure.
- **Production Audit (`npm audit --omit=dev --audit-level=high`):** `Critical: 0`, `High: 0`, `Moderate: 0`, `Low: 0`, `Exit Code: 0` (passed cleanly with zero production runtime vulnerabilities).
- **Full Dependency Audit (`npm audit --audit-level=critical`):** `Critical: 0`, `High: 12`, `Moderate: 0`, `Low: 0`, `Exit Code: 0` (passed current CI policy; all 12 `high` advisories originate from development/tooling dependency chains involving `picomatch <=4.0.7` and remain tracked **open** under roadmap item `G-05` until a compatible upstream fix publishes).

---

## 6. Package Governance, EF Core 10 Behavior & Architectural Boundaries

### 6.1 Resolved Version Drift & Remaining Governance Gaps
- **Resolved in `.NET 10 LTS` & Stabilization Pass:** Aligned EF Core (`10.0.15`), `Microsoft.AspNetCore.Authentication.JwtBearer` (`10.0.15`), `Microsoft.AspNetCore.Mvc.Testing` (`10.0.15`), `Application`'s `Microsoft.Extensions.*` (`10.0.15`), and all test runner/SDK packages (`Microsoft.NET.Test.Sdk 17.12.0`, `xunit 2.9.2`, `xunit.runner.visualstudio 2.8.2`, `coverlet.collector 6.0.2`) across all 8 `.csproj` files (`dotnet list DVLD.sln package --vulnerable --include-transitive` reports `0` vulnerable packages). Moved `@tailwindcss/vite` and `tailwindcss` into `devDependencies` and updated `eslint` (`^10.15.0`) + `typescript-eslint` (`^8.73.0`), achieving **`0` vulnerabilities in `npm audit --omit=dev`**.
- **Transitive SqlClient Note:** EF Core `10.0.15` pulls `Microsoft.Data.SqlClient 6.1.7` and `Microsoft.Identity.Client.Broker 4.84.2`.
- **Remaining Package Governance Items:** Central package management (`Directory.Packages.props` — `CAND-07`) is not yet adopted; `npm audit --json` still reports `12` devDependency-only `high` advisories propagating from `picomatch <=4.0.7` (`latest: 4.0.7` on npm, affecting `vite`/`vitest`/`typescript-eslint`); `Swashbuckle.AspNetCore` remains pinned at `6.6.2` (verified working on `net10.0` via `ApiSmokeTests`).

### 6.2 Application Layer Clean Architecture Purity (BCrypt & SkiaSharp)
- `BCrypt.Net-Next` and `SkiaSharp` are referenced directly in `Application.csproj`. Both are CPU-bound algorithms with zero I/O or database dependencies; decoupling them behind interfaces is tracked as low-priority cleanup rather than an urgent defect.

### 6.3 Excluded / Unapproved Technologies
The following technologies are **NOT** part of the current repository and must **NOT** be introduced without an approved ADR in [`ENGINEERING-DECISIONS.md`](../../ENGINEERING-DECISIONS.md):
- **MediatR / full CQRS / Event Sourcing**
- **Redis or external distributed caching**
- **Dapper or secondary micro-ORMs**
- **Microservices, Docker/Kubernetes manifests, Kafka, or RabbitMQ**
