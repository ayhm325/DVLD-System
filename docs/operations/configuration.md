# Configuration Reference

All backend runtime configuration is bound from `API/DVLD.Api/appsettings.json`, `appsettings.Development.json`, .NET User Secrets, and environment variables.

---

## 1. Configuration Sections & Defaults

| Section | Key | Default (`appsettings.json`) | Validation / Behavior |
|---|---|---|---|
| `ConnectionStrings` | `DefaultConnection` | *(must be provided)* | SQL Server connection string for `DVLDDbContext`. |
| `Jwt` | `Issuer` | `"DVLD.Api"` | Required non-empty string (`AuthenticationExtensions`). |
| `Jwt` | `Audience` | `"DVLD.Client"` | Required non-empty string (`AuthenticationExtensions`). |
| `Jwt` | `SecretKey` | *(must be provided)* | Required; minimum **32 UTF-8 bytes**. |
| `Jwt` | `ExpirationMinutes` | `15` | `JwtOptionsValidator` enforces `5..60` (`ValidateOnStart`). |
| `AuthSession` | `RefreshTokenLifetimeDays` | `14` | Validated `1..90`. |
| `AuthSession` | `IdleTimeoutDays` | `7` | Validated `1..30` and `<= RefreshTokenLifetimeDays`. |
| `AuthSession` | `RotationGraceSeconds` | `30` | Validated `0..300`. |
| `AuthSession` | `RefreshCookieName` | `"dvld_rt"` | Non-whitespace cookie name. |
| `AuthSession` | `RefreshCookiePath` | `"/api/Auth"` | Must start with `'/'`. |
| `Storage` | `PersonImagesPath` | `"../DVLD_Storage/People"` | Fallback: `%LocalApplicationData%\DVLD\Storage`. |
| `LicenseExpiration` | `IsEnabled` | `true` | Enables `LicenseExpirationBackgroundService`. |
| `LicenseExpiration` | `PollingIntervalSeconds` | `60` | Polling interval for expiration updates. |
| `NoShowProcessor` | `IsEnabled` | `true` | Enables `NoShowBackgroundService`. |
| `NoShowProcessor` | `PollingIntervalSeconds` | `60` | Polling interval for no-show scans. |
| `NoShowProcessor` | `BatchSize` | `50` | Appointments processed per batch (max 20 batches/run). |
| `NoShowProcessor` | `LockTimeoutMs` | `5000` | SQL `sp_getapplock` timeout per appointment. |
| `NoShowProcessor` | `GracePeriodMinutes` | `40` | Grace period after `AppointmentDate` before marking `NoShow`. |
| `Seq` | `ServerUrl` | `""` (`http://localhost:5341` in Dev) | Optional Serilog Seq sink URL. |
| `DiagnosticLogging:File` | `Enabled` | `false` | Enables rolling file sink (`logs/dvld-.log`, 7 files, 10 MB limit). |

---

## 2. Frontend Configuration (`ClientApp/`)

- **`API_BASE_URL`** (`ClientApp/src/services/apiClient.ts`): Defaults to `'/api'`.
- **Vite Dev Proxy** (`ClientApp/vite.config.ts`): Proxies `/api` to `https://localhost:7125` on port `5173`.
- **Playwright Port** (`ClientApp/playwright.config.ts`): Configurable via `PLAYWRIGHT_PORT` (defaults to `5173`).

---

## Implementation References

- App settings: [`API/DVLD.Api/appsettings.json`](../../API/DVLD.Api/appsettings.json), [`API/DVLD.Api/appsettings.Development.json`](../../API/DVLD.Api/appsettings.Development.json)
- Options classes: [`Application/Options/`](../../Application/Options/)
- Startup validation: [`API/DVLD.Api/Configuration/AuthenticationExtensions.cs`](../../API/DVLD.Api/Configuration/AuthenticationExtensions.cs), [`API/DVLD.Api/Configuration/LoggingExtensions.cs`](../../API/DVLD.Api/Configuration/LoggingExtensions.cs)
