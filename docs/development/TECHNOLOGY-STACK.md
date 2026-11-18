# Technology Stack & Dependency Inventory

> **Authority Level:** Authoritative Technical Inventory  
> **Parent Document:** [`AGENTS.md`](../../AGENTS.md)  
> **Repository Root:** `D:\DVLD-System`  
> **Last Reviewed:** 2026-11-18 (Local Manifest Inspection at `HEAD` `dead101`)  

---

## 1. Overview and Principles

This document inventories the runtime environments, frameworks, libraries, build tools, and CI/CD configurations verified directly from local manifests in `D:\DVLD-System` (`global.json`, `*.csproj`, `ClientApp/package.json`, `ClientApp/tsconfig.app.json`, and `azure-pipelines.yml`). AI agents and engineers must **not** upgrade, downgrade, or add packages without explicit written approval and an approved Architecture Decision Record.

> [!WARNING]
> **Runtime Support Lifecycle (.NET 8 End of Support):**  
> This repository currently targets `net8.0` and pins SDK `8.0.427`. Microsoft ended official support for **.NET 8** and **EF Core 8** on **2026-11-10** ([Microsoft .NET Support Policy](https://dotnet.microsoft.com/en-us/platform/support/policy)). Upgrading to a supported LTS line (currently `.NET 10 LTS`, supported through `2028-11-14`, with `EF Core 10`) is an urgent planned workstream, **not** an implemented state.

---

## 2. Verified Backend Technology Stack (.NET & C#)

### 2.1 Core Runtime & SDK
- **.NET SDK Version:** `8.0.427` (`rollForward: latestFeature`, pinned in [`global.json`](../../global.json)).
- **Target Framework:** `net8.0` across all 8 backend projects (`Domain`, `Application`, `Infrastructure`, `DVLD.Contracts`, `DVLD.Api`, `Application.UnitTests`, `API.IntegrationTests`, `Infrastructure.IntegrationTests`).
- **Language Settings:** C# 12 (default for .NET 8 SDK; `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` enabled across projects).
- **Central Package Management:** No `Directory.Packages.props` file currently exists in the repository (even though `azure-pipelines.yml` includes path triggers for it); package versions are declared per `.csproj`.

### 2.2 Backend Application Package Inventory (Exact `.csproj` Declarations)

| Package / Library | Declared Version | Manifest File | Layer / Project | Purpose / Role | Notes |
|---|---|---|---|---|---|
| **Microsoft.AspNetCore.App** | Framework Reference (`8.0`) | Implicit (`Microsoft.NET.Sdk.Web`) | `DVLD.Api` | Web API hosting, routing, middleware, controllers | Active |
| **Microsoft.AspNetCore.Authentication.JwtBearer** | `8.0.19` | `API/DVLD.Api/DVLD.Api.csproj` | `DVLD.Api` | JWT bearer authentication & request validation | Active |
| **Swashbuckle.AspNetCore** | `6.6.2` | `API/DVLD.Api/DVLD.Api.csproj` | `DVLD.Api` | OpenAPI 3.0 spec & Swagger UI (Development) | Active |
| **Microsoft.EntityFrameworkCore.Design** | `8.0.0` | `API/DVLD.Api/DVLD.Api.csproj`, `Infrastructure/Infrastructure.csproj` | `DVLD.Api`, `Infrastructure` | EF Core design-time tooling (`PrivateAssets=all`) | Version drift vs. EF Core `8.0.18` |
| **BCrypt.Net-Next** | `4.2.0` | `Application/Application.csproj` | `Application` | Password hashing (WorkFactor = 11) | Active |
| **SkiaSharp** | `4.154.0` | `Application/Application.csproj` | `Application` | Server-side image validation & WebP resizing | Active |
| **Microsoft.Extensions.Logging.Abstractions** | `10.0.3` | `Application/Application.csproj` | `Application` | `ILogger<T>` logging abstractions | Version 10.x referenced on `net8.0` |
| **Microsoft.Extensions.Options** | `10.0.3` | `Application/Application.csproj` | `Application` | Strongly-typed options abstractions | Version 10.x referenced on `net8.0` |
| **Microsoft.EntityFrameworkCore** | `8.0.18` | `Infrastructure/Infrastructure.csproj` | `Infrastructure` | ORM data access & change tracking | Version drift vs. `8.0.0` siblings |
| **Microsoft.EntityFrameworkCore.SqlServer** | `8.0.0` | `Infrastructure/Infrastructure.csproj` | `Infrastructure` | SQL Server provider for EF Core | Version drift vs. EF Core `8.0.18` |
| **Microsoft.EntityFrameworkCore.Tools** | `8.0.0` | `Infrastructure/Infrastructure.csproj` | `Infrastructure` | EF Core CLI/Package Manager tools (`PrivateAssets=all`) | Version drift vs. EF Core `8.0.18` |

---

## 3. Verified Frontend Technology Stack (`ClientApp/`)

The web frontend resides in `ClientApp/` and is built independently using Node.js and `npm` (`ClientApp/package.json`, `ClientApp/package-lock.json`).

### 3.1 Core Runtime, Language & Tooling
- **Package Manager:** `npm` (`package-lock.json` present for deterministic CI `npm ci`).
- **Node.js Version:** Node.js 20+ recommended locally; `22.x` LTS configured in `azure-pipelines.yml`.
- **UI Library:** `react` `^19.3.1`, `react-dom` `^19.3.1`.
- **Language / Compiler:** `typescript` `~6.0.3`. In `ClientApp/tsconfig.app.json`, target is `es2023` (`lib: ["ES2023", "DOM"]`), `moduleResolution: "bundler"`, and lint/safety flags `noUnusedLocals`, `noUnusedParameters`, `noFallthroughCasesInSwitch`, `verbatimModuleSyntax`, and `erasableSyntaxOnly` are `true`; however, `"strict": true` is **not** explicitly declared.
- **Bundler & Dev Server:** `vite` `^8.3.4` (`@vitejs/plugin-react` `^6.1.2`).
- **Styling Engine:** `tailwindcss` `^4.3.3` with `@tailwindcss/vite` `^4.3.3`.

### 3.2 Key Frontend Runtime & Dev Dependencies (`ClientApp/package.json`)

| Package | Declared Version | Role | Scope |
|---|---|---|---|
| **@radix-ui/react-*** (`checkbox`, `dialog`, `dropdown-menu`, `label`, `select`, `separator`, `slot`, `switch`, `tooltip`) | `^1.2.0` – `^2.4.1` | Accessible UI primitives | Runtime |
| **lucide-react** | `^1.65.0` | SVG iconography | Runtime |
| **motion** | `^14.9.0` | UI animations and transitions | Runtime |
| **clsx** / **tailwind-merge** / **class-variance-authority** | `^2.1.1` / `^3.7.0` / `^0.7.1` | Conditional class composition & variant styling | Runtime |
| **@fontsource-variable/{fraunces,inter,jetbrains-mono}** | `^5.3.0` | Self-hosted variable typography | Runtime |
| **eslint** / **@eslint/js** / **typescript-eslint** | `^10.13.0` / `^10.0.1` / `^8.71.2` | TypeScript & React static linting | Dev |
| **eslint-plugin-react-hooks** / **eslint-plugin-react-refresh** | `^7.1.1` / `^0.5.7` | React hooks & Fast Refresh rules | Dev |
| **vitest** / **@testing-library/react** / **@testing-library/jest-dom** / **@testing-library/user-event** / **jsdom** | `^5.1.0` / `^16.3.3` / `^7.0.1` / `^14.6.8` / `^31.0.0` | Frontend unit & component test stack | Dev |
| **@playwright/test** / **@axe-core/playwright** | `^1.65.0` / `^4.14.0` | End-to-end browser & accessibility testing | Dev |

---

## 4. Test Frameworks & Tooling

### 4.1 Backend Test Projects (`Test/`)

| Package | `Application.UnitTests` | `Infrastructure.IntegrationTests` | `API.IntegrationTests` | Role |
|---|---:|---:|---:|---|
| **xunit** | `2.5.3` | `2.5.3` | `2.9.2` | Primary unit/integration test framework |
| **xunit.runner.visualstudio** | `2.5.3` | `2.5.3` | `2.8.2` | VSTest / `dotnet test` adapter |
| **Microsoft.NET.Test.Sdk** | `17.8.0` | `17.8.0` | `17.12.0` | MSBuild test host infrastructure |
| **coverlet.collector** | `6.0.0` | `6.0.0` | `6.0.2` | Cobertura code coverage collector |
| **Moq** | `4.20.72` | — | `4.20.72` | Interface mocking |
| **Microsoft.AspNetCore.Mvc.Testing** | — | — | `8.0.19` | `WebApplicationFactory` in-memory/test hosting |
| **Microsoft.EntityFrameworkCore.SqlServer** | — | `8.0.0` | (via `Infrastructure`) | Ephemeral SQL Server test execution |

### 4.2 Frontend Test Suites (`ClientApp/tests/`)
- **Unit Tests:** `vitest` (`npm run test`) configured via `ClientApp/vitest.config.ts` with 7 test files under `ClientApp/tests/unit/`.
- **E2E Tests:** `@playwright/test` (`npm run test:e2e`) configured via `ClientApp/playwright.config.ts` with 1 test file under `ClientApp/tests/e2e/login.spec.ts`.

---

## 5. Continuous Integration & Pipeline Configuration (`azure-pipelines.yml`)

- **Platform & Image:** Azure Pipelines running on `windows-latest`.
- **Stage 1 — `Frontend_CI` (`timeoutInMinutes: 15`):**
  - Installs Node.js `22.x`, restores npm cache, runs `npm ci`, `npm run lint`, `npx --no-install tsc -b --pretty false`, and `npm run build`, then publishes `clientapp-dist`.
  - **Gap:** Does not currently invoke `npm run test` (Vitest) or `npm run test:e2e` (Playwright).
- **Stage 2 — `Backend_CI` (`timeoutInMinutes: 60`):**
  - Note: `timeoutInMinutes` was increased from `45` to `60` in commit `dead101` (`azure-pipelines.yml:281`). This is a static pipeline configuration change, not runtime proof that a subsequent pipeline run succeeded.
  - Installs .NET 8 SDK from `global.json`, logs `dotnet --info`, restores and builds `DVLD.sln` (`Release`), runs `Application.UnitTests`, initializes an isolated run-specific LocalDB instance (`eng/scripts/Initialize-TestSqlEngine.ps1`), validates `SqlExpectedInstance` and `Jwt__SecretKey`, executes `API.IntegrationTests` and `Infrastructure.IntegrationTests`, runs ownership-verified cleanup (`eng/scripts/Cleanup-TestSqlEngine.ps1` with `condition: always()`), stages TRX/Cobertura reports (`eng/scripts/Stage-BackendTestReports.ps1`), and publishes `dvld-api-release`.

---

## 6. Known Manifest Discrepancies & Architectural Boundaries

### 6.1 EF Core & Microsoft.Extensions Version Drift
- `Infrastructure/Infrastructure.csproj` declares `Microsoft.EntityFrameworkCore` at `8.0.18` while `Microsoft.EntityFrameworkCore.SqlServer`, `Design`, and `Tools` are declared at `8.0.0`.
- `Application/Application.csproj` references `Microsoft.Extensions.Logging.Abstractions` and `Microsoft.Extensions.Options` at `10.0.3` on `net8.0`, and test runner/SDK package versions vary between `API.IntegrationTests` and its sibling test projects.
- **Policy:** Do **not** perform blind text edits to package versions. Align versions deliberately as part of an authorized `.NET 10 LTS` upgrade plan validated across restore, build, migrations, and all test suites.

### 6.2 Application Layer Clean Architecture Purity (BCrypt & SkiaSharp)
- `BCrypt.Net-Next` and `SkiaSharp` are referenced directly in `Application.csproj`. Both are CPU-bound algorithms with zero I/O or database dependencies; decoupling them behind interfaces is tracked as low-priority cleanup rather than an urgent defect.

### 6.3 Excluded / Unapproved Technologies
The following technologies are **NOT** part of the current repository and must **NOT** be introduced without an approved ADR in [`ENGINEERING-DECISIONS.md`](../../ENGINEERING-DECISIONS.md):
- **MediatR / full CQRS / Event Sourcing**
- **Redis or external distributed caching**
- **Dapper or secondary micro-ORMs**
- **Microservices, Docker/Kubernetes manifests, Kafka, or RabbitMQ**
