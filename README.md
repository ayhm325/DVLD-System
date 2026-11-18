# DVLD — Driver & Vehicle Licensing Department System

DVLD is a .NET 8 driving-license department management system with an ASP.NET Core Web API (`DVLD.Api`), SQL Server persistence (`Infrastructure`), transactional business workflows (`Application`), pure domain models (`Domain`), shared HTTP contracts (`DVLD.Contracts`), a React 19 single-page application (`ClientApp`), and automated unit/integration/E2E test suites.

---

## Architecture Overview

```text
  ClientApp (React 19 + TypeScript + Vite)
              │
              │ HTTP + JSON + JWT + HttpOnly Refresh Cookie
              v
          DVLD.Api (ASP.NET Core 8)
              │
              v
         Application
              │
              v
        Infrastructure (EF Core 8 + SQL Server)
```

---

## Quick Start

### Prerequisites
- **.NET 8 SDK** (`8.0.427` pinned in [`global.json`](./global.json); note Microsoft ended .NET 8 support on `2026-11-10` — see [`TECHNOLOGY-STACK.md`](./docs/development/TECHNOLOGY-STACK.md))
- **SQL Server** (`MSSQLSERVER` or LocalDB) with Windows Authentication
- **Node.js** (20+ LTS recommended) and `npm`

### 1. Run the Backend API (`DVLD.Api`)
Configure `Jwt:SecretKey` (minimum 32 UTF-8 bytes) via User Secrets or environment variables, then run from `D:\DVLD-System`:

```powershell
dotnet restore DVLD.sln
dotnet build DVLD.sln --configuration Release
dotnet run --project ".\API\DVLD.Api\DVLD.Api.csproj" --launch-profile http
```

By default, the local HTTP profile listens on `http://localhost:5260` (Swagger UI at `/swagger` in Development).

### 2. Run the React Frontend (`ClientApp`)
With `DVLD.Api` running at `http://localhost:5260`:

```powershell
cd ClientApp
npm install
npm run dev
```

For full setup, configuration, safe test execution (`DVLDf` isolation rules), and CI details, see [`docs/development/DEVELOPMENT-GUIDE.md`](./docs/development/DEVELOPMENT-GUIDE.md) and [`ClientApp/README.md`](./ClientApp/README.md).

---

## Documentation Navigation

All project documentation is organized around a single-source-of-truth hierarchy under [`docs/README.md`](./docs/README.md):

| Category / Purpose | Entry Point |
|---|---|
| **Central Documentation Index** | [`docs/README.md`](./docs/README.md) |
| **End-User & Staff Guide (`docs/client/`)** | [`docs/client/USER-GUIDE.md`](./docs/client/USER-GUIDE.md) |
| **Developer Setup, Architecture, Security, Testing & Stack (`docs/development/`)** | [`docs/development/DEVELOPMENT-GUIDE.md`](./docs/development/DEVELOPMENT-GUIDE.md) |
| **Licensing Domain Rules (`docs/reference/`)** | [`docs/reference/LICENSING-DOMAIN-SPECIFICATION.md`](./docs/reference/LICENSING-DOMAIN-SPECIFICATION.md) |
| **Frontend Portal Setup (`ClientApp/`)** | [`ClientApp/README.md`](./ClientApp/README.md) |
| **AI Engineering Governance** | [`AGENTS.md`](./AGENTS.md) and [`ClientApp/AGENTS.md`](./ClientApp/AGENTS.md) |
| **Engineering Backlog & Task Authorization** | [`ENGINEERING-ROADMAP.md`](./ENGINEERING-ROADMAP.md) |
| **Architecture & Policy Decisions (ADRs)** | [`ENGINEERING-DECISIONS.md`](./ENGINEERING-DECISIONS.md) |
| **Historical Audits, Snapshots & Superseded Plans (`docs/archive/`)** | [`docs/archive/README.md`](./docs/archive/README.md) |
