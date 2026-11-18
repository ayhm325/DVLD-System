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

> [!WARNING]
> **Runtime Support Lifecycle (.NET 8 & Node.js 20 End of Support):**
>
> - **.NET / EF Core:** This repository currently targets `net8.0` and pins SDK `8.0.427`. Microsoft ended official support for **.NET 8** and **EF Core 8** on **2026-11-10** ([Microsoft .NET Support Policy](https://dotnet.microsoft.com/en-us/platform/support/policy)). Upgrading to a supported LTS line (currently `.NET 10 LTS`, supported through `2028-11-14`, with `EF Core 10`) is an urgent planned workstream (`PLANNED` / `NOT AUTHORIZED`), **not** an implemented state.
> - **Node.js:** `azure-pipelines.yml` pins `nodeVersionSpec: '22.x'` (Node.js 22 *Maintenance LTS*, EOL `2027-04-30`). Node.js 20 reached End-of-Life (`2026-04-30`) and is **unsupported**. Local development should match CI (`22.x` LTS) or use an Active LTS release (`24.x` / `26.x`).

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
- **Package Manager:** `npm` (`ClientApp/package-lock.json` present for deterministic CI `npm ci`).
- **Node.js Version:** `22.x` LTS configured in [`azure-pipelines.yml`](../../azure-pipelines.yml) (`Maintenance LTS` until `2027-04-30`). Use Node.js `22.x` LTS locally to match CI (or an Active LTS release `24.x` / `26.x`); Node.js `20.x` reached EOL on `2026-04-30` and is no longer recommended.
- **UI Library:** `react` `^19.3.1`, `react-dom` `^19.3.1`.
- **Language / Compiler:** `typescript` `~6.0.3`. In [`ClientApp/tsconfig.app.json`](../../ClientApp/tsconfig.app.json), target is `es2023` (`lib: ["ES2023", "DOM"]`), `moduleResolution: "bundler"`, and safety flags `noUnusedLocals`, `noUnusedParameters`, `noFallthroughCasesInSwitch`, `verbatimModuleSyntax`, and `erasableSyntaxOnly` are `true`; however, `"strict": true` is **not** explicitly declared.
- **Bundler & Dev Server:** `vite` `^8.3.4` (`@vitejs/plugin-react` `^6.1.2`). In [`ClientApp/vite.config.ts`](../../ClientApp/vite.config.ts), the local dev server listens on `0.0.0.0:5173` (`strictPort: true`) and proxies `/api` to `https://localhost:7125` (`secure: false` and `; Secure` cookie attribute stripping are local-dev-only conveniences, never suitable for Production).
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

*(Static inventory inspected at `e9e843e`: `Application.UnitTests` has 41 C# files with 842 `[Fact]` and 51 `[Theory]` attributes [141 `[InlineData]` rows]; `Infrastructure.IntegrationTests` has 41 C# files with 652 `[Fact]` and 38 `[Theory]` attributes [87 `[InlineData]` rows]; `API.IntegrationTests` has 58 C# files with 466 `[Fact]` and 56 `[Theory]` attributes [161 `[InlineData]` rows]; `Test/Shared/` has 1 helper file. Attribute counts reflect static source declarations, not current-pass test execution.)*

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
- **Unit & Component Tests:** `vitest` `^5.1.0`, `@testing-library/react` `^16.3.3`, `@testing-library/jest-dom` `^7.0.1`, `@testing-library/user-event` `^14.6.8`, and `jsdom` `^31.0.0` configured in [`ClientApp/vitest.config.ts`](../../ClientApp/vitest.config.ts) (`setupFiles: ['./tests/setup.ts']`) with **9 test files** under `ClientApp/tests/unit/`.
- **End-to-End (E2E) Tests:** `@playwright/test` `^1.65.0` and `@axe-core/playwright` `^4.14.0` configured in [`ClientApp/playwright.config.ts`](../../ClientApp/playwright.config.ts) with **1 spec file** (`ClientApp/tests/e2e/login.spec.ts`) and 16 visual snapshot PNGs under `ClientApp/tests/screenshots/`.

| Test Tier | Package Declared | Config Present | Discovered Files (`e9e843e`) | Executed in Current Audit | Runtime Pass Proof | Included in `azure-pipelines.yml` |
|---|:---:|:---:|---|---|---|:---:|
| **Vitest Unit/Component (`npm run test`)** | `IMPLEMENTED` | `IMPLEMENTED` | **9** `.test.ts(x)` files (+ `tests/setup.ts`) | `Not Executed` | `NEEDS VERIFICATION` | **No** (`CONFIGURED — NOT EXECUTED` in CI) |
| **Playwright E2E (`npm run test:e2e`)** | `IMPLEMENTED` | `IMPLEMENTED` | **1** `login.spec.ts` (+ 16 screenshots) | `Not Executed` | `NEEDS VERIFICATION` | **No** (`NOT IMPLEMENTED` in CI) |

---

## 5. Continuous Integration & Pipeline Configuration (`azure-pipelines.yml`)

- **Platform & Image:** Azure Pipelines running on `windows-latest` (`CONFIGURED — NOT EXECUTED` locally during documentation audit; pipeline runtime status requires hosted CI run proof).
- **Stage 1 — `Frontend_CI` (`timeoutInMinutes: 15`):**
  - Installs Node.js `22.x`, restores npm cache, runs `npm ci --no-fund`, `npm run lint`, `npx --no-install tsc -b --pretty false`, and `npm run build`, then publishes `clientapp-dist`.
  - **CI Verification Gap:** Does **not** currently invoke `npm run test` (Vitest) or `npm run test:e2e` (Playwright).
- **Stage 2 — `Backend_CI` (`timeoutInMinutes: 60`):**
  - Note: `timeoutInMinutes` is configured at `60` in [`azure-pipelines.yml`](../../azure-pipelines.yml) alongside a run-specific LocalDB instance `DVLD_CI_$(Build.BuildId)_$(System.JobAttempt)`. This is static pipeline configuration (`CONFIGURED — NOT EXECUTED` in current pass), not runtime proof that a subsequent Azure DevOps run succeeded.
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
