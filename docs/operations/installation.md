# Installation & Local Development Setup

## 1. Prerequisites

| Tool | Required Version | Verification Source |
|---|---|---|
| **.NET SDK** | `10.0.404` (`rollForward: latestFeature`) | [`global.json`](../../global.json) |
| **SQL Server** | SQL Server / LocalDB | [`Infrastructure/Infrastructure.csproj`](../../Infrastructure/Infrastructure.csproj) |
| **Node.js & npm** | Node `22.x` (GitHub Actions) / `26.x` (Azure Pipelines) | [`.github/workflows/deploy-azure-appservice.yml`](../../.github/workflows/deploy-azure-appservice.yml), [`azure-pipelines.yml`](../../azure-pipelines.yml) |

---

## 2. Backend & Database Setup

### 2.1 Restore Dependencies

```powershell
dotnet restore DVLD.sln
```

### 2.2 Configure Local Secrets

`DVLD.Api` requires a SQL Server connection string (`ConnectionStrings:DefaultConnection`) and a JWT signing key (`Jwt:SecretKey`, minimum 32 UTF-8 bytes). Set them via .NET User Secrets (`UserSecretsId = 01db273f-2194-45c0-8b00-a6b4e5d19918`) or environment variables:

```powershell
dotnet user-secrets --project API/DVLD.Api/DVLD.Api.csproj set "ConnectionStrings:DefaultConnection" "<YOUR_SQL_SERVER_CONNECTION_STRING>"
dotnet user-secrets --project API/DVLD.Api/DVLD.Api.csproj set "Jwt:SecretKey" "<YOUR_32_PLUS_BYTE_SIGNING_KEY>"
```

### 2.3 Apply EF Core Migrations

```powershell
dotnet ef database update --project Infrastructure/Infrastructure.csproj --startup-project API/DVLD.Api/DVLD.Api.csproj
```

### 2.4 Start the API Server

```powershell
dotnet run --project API/DVLD.Api/DVLD.Api.csproj --launch-profile https
```

- `https` profile (`launchSettings.json`): `https://localhost:7125` and `http://localhost:5260`.
- `http` profile: `http://localhost:5260`.
- Swagger UI is available at `/swagger` when `ASPNETCORE_ENVIRONMENT=Development`.

---

## 3. Frontend Setup (`ClientApp/`)

```powershell
cd ClientApp
npm install
npm run dev
```

- Vite serves the SPA on `http://localhost:5173` (`strictPort: true`).
- Requests to `/api/*` are proxied to `https://localhost:7125` (`secure: false`), with `; secure` stripped from `set-cookie` headers for local HTTP development.

---

## Implementation References

- SDK & Solution: [`global.json`](../../global.json), [`DVLD.sln`](../../DVLD.sln)
- API launch & project: [`API/DVLD.Api/Properties/launchSettings.json`](../../API/DVLD.Api/Properties/launchSettings.json), [`API/DVLD.Api/DVLD.Api.csproj`](../../API/DVLD.Api/DVLD.Api.csproj)
- Frontend manifest & proxy: [`ClientApp/package.json`](../../ClientApp/package.json), [`ClientApp/vite.config.ts`](../../ClientApp/vite.config.ts)
