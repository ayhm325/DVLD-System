# DVLD Structured Diagnostic Logging & Observability

> **Scope:** `DVLD.Api` (Composition Root) & `Microsoft.Extensions.Logging` (`ILogger<T>`)  
> **Classification:** Operational Diagnostic & Request Observability System  
> **Non-Goal:** **NOT** an authoritative or immutable Business Audit Trail

---

## 1. Architecture Overview

DVLD uses standard `Microsoft.Extensions.Logging.ILogger<T>` abstractions in `Application`, `Infrastructure`, and `DVLD.Api`, backed by **Serilog** configured exclusively at the API composition root (`API/DVLD.Api`):

```text
Application / Infrastructure / API (ILogger<T>)
        │
        ▼
Serilog Pipeline (DVLD.Api composition root)
  ├── Enrichers: Application, Environment, LogContext, SensitiveDataRedactingEnricher
  ├── Sinks:
  │     ├── Console (always enabled)
  │     ├── Seq (optional, configured via Seq:ServerUrl + Seq:ApiKey)
  │     └── Rolling File (optional, disabled by default via DiagnosticLogging:File:Enabled)
```

### Design Rationale
- **Why `ILogger<T>` remains the application abstraction:** `Domain` has zero logging dependencies; `Application` and `Infrastructure` depend only on `Microsoft.Extensions.Logging.Abstractions`. No Serilog types or static `Serilog.Log` calls exist in business services.
- **Why Serilog is configured at the API composition root:** `DVLD.Api` owns hosting, HTTP request diagnostics (`UseSerilogRequestLogging`), sink lifetimes, and environment-specific filtering (`LoggingExtensions.cs`, `Program.cs`).
- **Why Serilog was selected:** Mature structured-event pipeline (`MessageTemplate` + scalar properties), native ASP.NET Core request-completion logging, built-in `Seq` sink, and zero-boilerplate compatibility with `ILogger<T>`.

---

## 2. Diagnostic Logging vs. Business Audit Trail (Mandatory Distinction)

> [!IMPORTANT]
> **Serilog and Seq are strictly a Diagnostic Logging and Operational Observability system.**  
> They are **NOT** an authoritative, immutable, or legally reliable Business Audit Trail.

- **What Seq provides:** Searchable operational diagnostics, HTTP request timing/status, exception traces, and authentication/workflow diagnostic events for troubleshooting.
- **What Seq does NOT guarantee:**
  - Permanent or tamper-resistant storage.
  - Transactional consistency or atomic persistence with SQL Server.
  - Guaranteed delivery of every emitted event (if Seq is down or overloaded, events are dropped after sink buffering limits).
  - Compliance-grade or legally admissible proof of business operations.
- **Explicit Exclusions:** This system introduces **no** `AuditEvents` / `AuditLogs` entities, **no** audit tables, **no** EF Core migrations, **no** transactional outbox, and **no** SQL Server Serilog sink.

### Future Business Audit Trail Recommendation (Not Implemented)
If formal compliance or legal non-repudiation is required in a future authorized phase, a separate **Business Audit Trail** architecture should be evaluated for:
- User account creation, deactivation, and role/permission changes
- Session revocation and security-stamp rotation events
- Test appointment creation, cancellation, and rescheduling
- Test-result recording and modification
- License issuance, renewal, replacement, detention, and release

Requirements for retention, ownership, access control, tamper-resistance, and legal obligations require a separate ADR and explicit authorization before implementation.

---

## 3. Configuration

### 3.1 Seq Configuration
Configuration is read from `IConfiguration` (`appsettings.json`, `appsettings.Development.json`, environment variables, or .NET User Secrets):

- `Seq:ServerUrl`: Base URL of the Seq server (`http://localhost:5341` in `appsettings.Development.json`; empty in base `appsettings.json`).
- `Seq:ApiKey`: Ingestion API key (associated with the `DVLD-Local-Development` Seq input, `Ingest` permission, `Application = DVLD`, `Environment = Development`). **Never commit API keys** to `appsettings*.json` or paste them into terminals/scripts.

#### Setting `Seq:ApiKey` Locally via .NET User Secrets (Development Only)
User Secrets (`UserSecretsId: 01db273f-2194-45c0-8b00-a6b4e5d19918` in `API/DVLD.Api/DVLD.Api.csproj`) stores development secrets outside the repository (unencrypted local JSON, for development only—not a production vault). Set the key without echoing it to shell history:

```powershell
$sec = Read-Host "Enter Seq API key (hidden)" -AsSecureString
$ptr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec)
try {
    $plain = [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
    dotnet user-secrets set "Seq:ApiKey" $plain --project D:\DVLD-System\API\DVLD.Api\DVLD.Api.csproj
} finally {
    [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)
}
```

Verify key presence without printing its value:
```powershell
(Get-Content "$env:APPDATA\Microsoft\UserSecrets\01db273f-2194-45c0-8b00-a6b4e5d19918\secrets.json" -Raw | ConvertFrom-Json).PSObject.Properties.Name
```

#### Key Rotation & Revoking a Compromised Key
1. In the Seq UI (**Settings → API Keys**), create a new key with `Ingest` permission only and attached properties (`Application = DVLD`, `Environment = Development`).
2. Update local User Secrets (`Seq:ApiKey`) or production secret store with the new key and restart `DVLD.Api`.
3. Immediately **revoke/delete** the old or compromised API key in Seq (**Settings → API Keys**).

### 3.2 Behavior When Seq Is Unconfigured or Unavailable
- If `Seq:ServerUrl` is empty or invalid, the Seq sink is skipped and the API starts normally with Console logging.
- If Seq is temporarily unreachable or rejects ingestion, `Serilog.Sinks.Seq` handles retries/drops asynchronously in the background without blocking HTTP requests, crashing the API, or altering database transaction outcomes.

### 3.3 Optional Bounded Rolling File Sink
File logging is **disabled by default** (`DiagnosticLogging:File:Enabled: false` in `appsettings.json`) because Console + Seq are the primary destinations. Operators who need local file fallback can enable it:

```json
"DiagnosticLogging": {
  "File": {
    "Enabled": true,
    "Path": "logs/dvld-.log",
    "RetainedFileCountLimit": 7,
    "FileSizeLimitBytes": 10485760
  }
}
```

- Uses daily rolling (`RollingInterval.Day`) + size-based rolling (`10 MB` default, minimum `1 MB`) + bounded retention (`7` files default).
- `logs/` is excluded in `.gitignore`. Ensure the host process has write permissions to the configured directory if enabled.

---

## 4. Middleware Ordering, Request Logging & Correlation

In `MiddlewareExtensions.UseDvldPipeline`:
1. `UseSwagger()` / `UseSwaggerUI()` (Development only)
2. `UseDvldRequestLogging()` (`UseSerilogRequestLogging`)
3. `UseExceptionHandler()` (`GlobalExceptionHandler`)
4. `UseStatusCodePages(...)`
5. `UseHttpsRedirection()`, `UseRouting()`, `UseCors(...)`, `UseRateLimiter()`, `UseAuthentication()`, `UseAuthorization()`, `MapControllers()`

### Why This Ordering Is Used
- Placing `UseDvldRequestLogging()` **before** `UseExceptionHandler()` and `UseStatusCodePages()` ensures it observes the final HTTP status code (including `401`, `403`, `404`, `429`, and `500`) and total elapsed time, while `GlobalExceptionHandler` handles and logs unhandled exceptions **once** without `UseSerilogRequestLogging` emitting a duplicate exception stack trace.
- `UseRouting()` executes downstream inside `next()`, so `httpContext.GetEndpoint()` is populated by the time `UseDvldRequestLogging` records the completion event.

### Correlation & Structured Properties
- `TraceId`: `Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier` (matches `traceId` in RFC 7807 `ProblemDetails` responses and `ClientApp`'s `apiClient.ts`).
- `RequestId`: `httpContext.TraceIdentifier`.
- `SpanId`: `Activity.Current?.SpanId.ToString()` when an `Activity` is active.
- `HttpMethod`, `Route` (endpoint route template), `StatusCode`, `Elapsed`, `Application` (`"DVLD"`), `Environment`.

---

## 5. Log Levels & Framework Noise Control

Configured in `appsettings.json` under `Serilog:MinimumLevel`:
- **Default:** `Information`
- **`Microsoft`, `Microsoft.AspNetCore`, `Microsoft.AspNetCore.Hosting.Diagnostics`, `System`:** `Warning` (prevents duplicate ASP.NET Core request logs alongside `UseSerilogRequestLogging`).
- **`Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Database.Command`:** `Warning` (suppresses routine SQL command spam and prevents parameter-value exposure while preserving database operational warnings/errors).
- **HTTP Request Status Mapping (`UseDvldRequestLogging`):**
  - `5xx` or unhandled exception: `Error`
  - `/swagger*`, `/health*`: `Debug`
  - `2xx`, `3xx`, and expected `4xx` (`400`, `401`, `403`, `404`, `409`, `429`): `Information`

---

## 6. Sensitive-Data Protection

### Prohibited Data (Never Logged)
- Passwords, password hashes, security stamps, JWTs, refresh tokens, `Authorization` headers, cookies (`dvld_rt`), Seq API keys, connection strings, private keys.
- National numbers, full names, phone numbers, addresses, dates of birth, emails, personal photos, or full request/response DTOs and HTTP bodies.

### Implemented Safeguards
1. **Safe Route Templates:** `LoggingExtensions.GetSafeRoute` resolves `RouteEndpoint.RoutePattern.RawText` (including via `IExceptionHandlerFeature.Endpoint` in `GlobalExceptionHandler`) instead of raw `Request.Path`, preventing parameters in `/api/People/national/{nationalNo}` and `/api/Users/username/{username}` from being logged.
2. **Defense-in-Depth Enricher (`SensitiveDataRedactingEnricher`):** Redacts any accidentally emitted property matching sensitive names (`Password`, `AccessToken`, `RefreshToken`, `ApiKey`, `ConnectionString`, `NationalNo`, `Phone`, `Address`, etc.) to `"[REDACTED]"` and sanitizes `RequestPath` for parameterized person/user routes, while preserving safe correlation/entity IDs (`TraceId`, `RequestId`, `AccountId`, `TokenFamilyId`).
3. **Production Error Responses:** `GlobalExceptionHandler` returns RFC 7807 `ProblemDetails` without stack traces or internal exception messages.
4. **EF Core:** `EnableSensitiveDataLogging()` is not enabled.

---

## 7. Verification & Troubleshooting

- **Run logging & security integration tests (database-free):**
  ```powershell
  dotnet test Test/API.IntegrationTests/API.IntegrationTests.csproj --filter "FullyQualifiedName~StructuredLoggingTests"
  ```
- **Local Seq UI:** Start `DVLD.Api` in `Development` (`dotnet run --project API/DVLD.Api/DVLD.Api.csproj`), open `http://localhost:5341/`, and filter by `Application = 'DVLD' and Environment = 'Development'` or `TraceId = '<traceId>'`. Confirm events show the `DVLD-Local-Development` input rather than unauthenticated HTTP ingestion.
- **Known Limitations:**
  - `Program.cs` uses `preserveStaticLogger: true` in `UseDvldSerilog` with `options.Logger` bound to DI's `Serilog.ILogger` so concurrent `WebApplicationFactory` test hosts remain isolated; static `Serilog.Log` is used only for bootstrap/fatal startup errors in `Program.cs`.
  - Event delivery to Seq is best-effort and asynchronous; check `Console` output if Seq is unreachable.
