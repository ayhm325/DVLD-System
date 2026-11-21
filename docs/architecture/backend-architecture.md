# Backend Architecture

## 1. Composition and Startup (`Program.cs` & `Configuration/*.cs`)

`API/DVLD.Api/Program.cs` delegates service registration and middleware ordering to modular extension methods in `API/DVLD.Api/Configuration/`:

| Extension Class | Responsibility |
|---|---|
| `LoggingExtensions` | Configures Serilog (`AddDvldLogging`) and request logging (`UseDvldRequestLogging`) with route-template path sanitization and `SensitiveDataRedactingEnricher`. |
| `DatabaseExtensions` | Registers `DVLDDbContext` with SQL Server using `ConnectionStrings:DefaultConnection`. |
| `DependencyInjectionExtensions` | Registers repositories, `IUnitOfWork`, application services, `IClock` (`JordanClock`), `IFileStorageService` (`PhysicalFileStorageService`), `JwtTokenService`, `ApiCurrentUserService`, options (`StorageOptions`, `LicenseExpirationOptions`, `NoShowProcessorOptions`), and background services. |
| `AuthenticationExtensions` | Validates `JwtOptions` and `AuthSessionOptions`, configures `JwtBearerDefaults.AuthenticationScheme`, and enforces `OnTokenValidated` database security-state checks. |
| `AuthorizationExtensions` | Defines `AdminOnly`, `StaffOnly`, and `StaffOrAdmin` role policies. |
| `RateLimitingExtensions` | Configures fixed-window IP rate limiters (`LoginRateLimit`, `RefreshRateLimit`, `ChangePasswordRateLimit`) returning `429`. |
| `ApiAndSwaggerExtensions` | Registers controllers, `InvalidModelStateResponseFactory`, `GlobalExceptionHandler`, `ProblemDetails`, CORS policy `"ReactApp"`, and Swagger/OpenAPI (in `Development`). |
| `MiddlewareExtensions` | Defines the HTTP middleware pipeline (`UseDvldPipeline`). |

---

## 2. HTTP Middleware Pipeline Order

`MiddlewareExtensions.UseDvldPipeline` executes middleware in the following exact sequence:

```mermaid
flowchart LR
    SWG["Swagger (Dev only)"] --> LOG["UseDvldRequestLogging"]
    LOG --> EXC["UseExceptionHandler"]
    EXC --> SCP["UseStatusCodePages (ProblemDetails)"]
    SCP --> HTTPS["UseHttpsRedirection"]
    HTTPS --> DEF["UseDefaultFiles"]
    DEF --> STAT["UseStaticFiles"]
    STAT --> RTE["UseRouting"]
    RTE --> CORS["UseCors (ReactApp)"]
    CORS --> RL["UseRateLimiter"]
    RL --> AUTH["UseAuthentication"]
    AUTH --> AUTHZ["UseAuthorization"]
    AUTHZ --> SPA["SPA Fallback (wwwroot/index.html)"]
    SPA --> CTRL["MapControllers"]
```

- **`UseStatusCodePages`** formats bodyless responses (such as `401` challenges, `403` forbidden, unmatched `404`, and `429` rate limits) as `application/problem+json` with `traceId`.
- **SPA Fallback** handles non-`/api` `GET`/`HEAD` requests that do not contain a file extension by serving `wwwroot/index.html`.

---

## 3. Application Layer (`Application/`)

The `Application` project contains business orchestration without infrastructure dependencies:

- **Services (`Application/Services/*.cs`)**:
  - `AuthService`, `RefreshTokenService`
  - `PersonService` (partial: `.Crud.cs`, `.Photos.cs`, `.Queries.cs`)
  - `UserService` (partial: `.Administration.cs`, `.Queries.cs`, `.Security.cs`)
  - `ApplicationService`, `LocalDrivingLicenseApplicationService`, `DriverService`
  - `TestAppointmentService` (partial: `.Scheduling.cs`, `.Lifecycle.cs`, `.Availability.cs`, `.Queries.cs`), `TestService`, `TestWorkflowService`, `NoShowProcessorService`
  - `LicenseIssuanceService`, `LicenseRenewalService`, `LicenseReplacementService`, `DetainedLicenseService`, `InternationalService`
  - Reference data: `CountryService`, `ApplicationTypeService`, `LicenseClassService`, `TestTypeService`
- **Validators (`Application/Validators/*.cs`)**: Pure validation logic (`UserValidator`, `PersonValidator`, `ApplicationValidator`, `LocalDrivingLicenseApplicationValidator`, `TestAppointmentValidator`, `TestValidator`, `LicenseValidator`, `DetainedLicenseValidator`, `InternationalLicenseValidator`, `DriverValidator`, `ApplicationTypeValidator`, `LicenseClassValidator`, `TestTypeValidator`).
- **Options (`Application/Options/*.cs`)**: `JwtOptions`, `AuthSessionOptions`, `StorageOptions`, `LicenseExpirationOptions`, `NoShowProcessorOptions`.

---

## 4. Infrastructure Layer (`Infrastructure/`)

### 4.1 `UnitOfWork` and Transaction Verification

`Infrastructure/UnitOfWork.cs` (split across `.Transactions.cs`, `.Verification.cs`, and `.ConstraintHandling.cs`) coordinates EF Core `SaveChangesAsync`, explicit database transactions, and SQL Server execution strategies:

- `ExecuteInTransactionAsync` wraps transactional delegates with deferred physical commit and `TransactionVerificationTracker` support for ambiguous commit recovery.
- `ConstraintHandling` translates SQL Server constraint violations into domain-specific exceptions such as `DuplicateUsernameException`, `DuplicatePersonUserException`, and `ConcurrencyConflictException`.

### 4.2 Clock & File Storage

- **`JordanClock`** (`Infrastructure/Services/JordanClock.cs`): Provides `UtcNow`, `Now` (Jordan facility local time), and `Today` for date-sensitive licensing rules.
- **`PhysicalFileStorageService`** (`Infrastructure/Services/PhysicalFileStorageService.cs`): Persists processed person images to `Storage:PersonImagesPath` (default `"../DVLD_Storage/People"`, falling back to `%LocalApplicationData%\DVLD\Storage`).

---

## 5. Background Services (`API/DVLD.Api/BackgroundServices/`)

Two `BackgroundService` hosted workers run periodically when enabled in configuration:

| Service | Options Section | Defaults | Responsibility |
|---|---|---|---|
| `LicenseExpirationBackgroundService` | `LicenseExpiration` | `IsEnabled: true`, `PollingIntervalSeconds: 60` | Periodically marks expired active licenses as inactive. |
| `NoShowBackgroundService` | `NoShowProcessor` | `IsEnabled: true`, `PollingIntervalSeconds: 60`, `BatchSize: 50`, `LockTimeoutMs: 5000`, `GracePeriodMinutes: 40` | Invokes `NoShowProcessorService` to transition overdue unexamined `Scheduled` test appointments past the grace period to `NoShow` and lock them. |

---

## Implementation References

- Startup & configuration: [`API/DVLD.Api/Program.cs`](../../API/DVLD.Api/Program.cs), [`API/DVLD.Api/Configuration/MiddlewareExtensions.cs`](../../API/DVLD.Api/Configuration/MiddlewareExtensions.cs), [`API/DVLD.Api/Configuration/DependencyInjectionExtensions.cs`](../../API/DVLD.Api/Configuration/DependencyInjectionExtensions.cs)
- Background workers: [`API/DVLD.Api/BackgroundServices/LicenseExpirationBackgroundService.cs`](../../API/DVLD.Api/BackgroundServices/LicenseExpirationBackgroundService.cs), [`API/DVLD.Api/BackgroundServices/NoShowBackgroundService.cs`](../../API/DVLD.Api/BackgroundServices/NoShowBackgroundService.cs)
- Persistence & infrastructure: [`Infrastructure/DVLDDbContext.cs`](../../Infrastructure/DVLDDbContext.cs), [`Infrastructure/UnitOfWork.cs`](../../Infrastructure/UnitOfWork.cs), [`Infrastructure/Services/JordanClock.cs`](../../Infrastructure/Services/JordanClock.cs), [`Infrastructure/Services/PhysicalFileStorageService.cs`](../../Infrastructure/Services/PhysicalFileStorageService.cs)
