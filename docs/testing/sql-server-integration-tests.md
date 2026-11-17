# SQL Server Integration Testing Guide

This guide documents the local SQL Server integration-testing architecture, environment requirements, safety rules, execution commands, and troubleshooting steps for the **DVLD** repository.

---

## A. Requirements

1. **SQL Server** installed locally with the default `MSSQLSERVER` service running (or an approved local SQL Server instance).
2. **Windows Authentication** enabled for the current Windows user account (`Integrated Security=True`).
3. **`sqlcmd`** CLI available on `PATH` (for environment verification and diagnostics).
4. **Database Permissions:** The Windows login must have permission to create and drop databases on the target instance (for example, `CREATE ANY DATABASE`, `dbcreator`, or `sysadmin`) because integration tests create and drop ephemeral databases per test fixture.
5. **.NET 8 SDK** (`dotnet` CLI) matching the solution target framework (`net8.0`).

---

## B. SQL Server Verification

Run these read-only commands in PowerShell to verify the local SQL Server instance before running integration tests:

```powershell
# 1. Check SQL Server service status
Get-Service -Name MSSQLSERVER

# 2. Check sqlcmd availability
Get-Command sqlcmd

# 3. Verify Windows Authentication connectivity, instance identity, login, and permissions
sqlcmd -S localhost -d master -E -b -Q "SET NOCOUNT ON; SELECT CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')) + N'|' + CONVERT(nvarchar(128), @@SERVICENAME) AS ExpectedInstance, SUSER_SNAME() AS CurrentLogin, CASE WHEN HAS_PERMS_BY_NAME(NULL, NULL, N'CREATE ANY DATABASE') = 1 OR HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'CREATE DATABASE') = 1 OR IS_SRVROLEMEMBER(N'dbcreator') = 1 OR IS_SRVROLEMEMBER(N'sysadmin') = 1 THEN 1 ELSE 0 END AS CanCreateDatabase;"
```

The instance identity check in `SqlServerTestEnvironment.VerifyExpectedInstanceAsync` ([Test/Shared/SqlServerTestEnvironment.cs](../../Test/Shared/SqlServerTestEnvironment.cs)) constructs the expected identity string using:

```sql
SELECT CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')) + N'|' + CONVERT(nvarchar(128), @@SERVICENAME);
```

Copy that exact `ExpectedInstance` output into `DVLD_TEST_EXPECTED_INSTANCE`.

---

## C. Environment Variables

Integration tests require **two** environment variables:

| Variable | Purpose |
|---|---|
| `DVLD_TEST_CONNECTION` | Administrative connection string targeting `master` on the approved test SQL Server instance. |
| `DVLD_TEST_EXPECTED_INSTANCE` | Exact `SERVERPROPERTY('ServerName')|@@SERVICENAME` string of the approved instance. |

### Configuration Rules (`SqlServerTestEnvironment.ValidateConfiguration`)

- **Targets `master`:** The administrative connection opens `master` only to verify instance identity, create an ephemeral database, and drop that owned database during teardown.
- **Windows Authentication required:** `Integrated Security=True` is enforced; SQL login `User ID` and `Password` properties are rejected.
- **Writable endpoint:** `Application Intent` must be `ReadWrite` (not `ReadOnly`).
- **Forbidden catalogs:** `DVLD` and `DVLDf` are explicitly rejected.
- **No `AttachDBFilename`:** `AttachDBFilename`, `User Instance=True`, and `Failover Partner` are explicitly rejected.
- **Exact instance match:** `VerifyExpectedInstanceAsync` compares the live `ServerName|ServiceName` against `DVLD_TEST_EXPECTED_INSTANCE` (case-insensitive) before any create or drop operation.
- **Session-scoped recommended:** Set these variables in the current PowerShell process only (using the validation script below) rather than persisting them globally.

### Local Example (Machine-Specific Values)

> **Note:** Replace `Ayhm|MSSQLSERVER` with the exact `ExpectedInstance` returned by your local SQL Server instance.

```powershell
$env:DVLD_TEST_CONNECTION = "Server=localhost;Initial Catalog=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Application Intent=ReadWrite"
$env:DVLD_TEST_EXPECTED_INSTANCE = "Ayhm|MSSQLSERVER"
```

Or use the automated validation and session-setup script:

```powershell
# Validate existing configuration
powershell -ExecutionPolicy Bypass -File .\scripts\testing\Test-DatabaseIntegrationEnvironment.ps1

# Validate and configure current PowerShell session (dot-sourced)
. .\scripts\testing\Test-DatabaseIntegrationEnvironment.ps1 -Setup
```

---

## D. Database Lifecycle

The integration-test lifecycle is implemented in:

- [Test/Shared/SqlServerTestEnvironment.cs](../../Test/Shared/SqlServerTestEnvironment.cs)
- [Test/Infrastructure.IntegrationTests/Fixtures/SqlServerTestDatabase.cs](../../Test/Infrastructure.IntegrationTests/Fixtures/SqlServerTestDatabase.cs)
- [Test/API.IntegrationTests/Infrastructure/SqlServerApiWebApplicationFactory.cs](../../Test/API.IntegrationTests/Infrastructure/SqlServerApiWebApplicationFactory.cs)

### Step-by-Step Lifecycle

1. **Validate configuration:** `ReadConfigurationFromEnvironment()` validates `DVLD_TEST_CONNECTION` and `DVLD_TEST_EXPECTED_INSTANCE`.
2. **Connect to `master`:** Opens an administrative `SqlConnection` to `master`.
3. **Verify expected instance:** Runs `VerifyExpectedInstanceAsync` against `SERVERPROPERTY('ServerName') + '|' + @@SERVICENAME`.
4. **Generate unique database name:** Uses `BuildDatabaseName` with an approved prefix and 32-character GUID (`N` format):
   - Infrastructure tests: `DVLD_IntegrationTests_<GUID>`
   - API integration tests: `DVLD_IntegrationTests_Api_<GUID>`
5. **Create dedicated database:** Verifies `DB_ID(@databaseName)` is `NULL` (never reuses/overwrites an existing database) and executes `CREATE DATABASE [<name>]`.
6. **Capture ownership identity:** Reads `database_id` and `service_broker_guid` from `sys.databases`.
7. **Add ownership marker:** Stores a random `runToken` in database extended property `DVLD_Test_Run_Token`.
8. **Apply EF Core migrations:** `MigrateAsync()` / `Migrate()` runs against the newly created database (API tests throttle concurrent migrations via `SemaphoreSlim(2, 2)`).
9. **Run tests:** All queries and requests execute exclusively against the ephemeral database.
10. **Verify ownership before cleanup:** `DropOwnedDatabaseAsync` verifies `master` connection, generated name format, expected instance identity, matching `database_id`, matching `service_broker_guid`, and matching `DVLD_Test_Run_Token` extended property.
11. **Drop owned database:** Executes `ALTER DATABASE ... SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ...` only if all ownership checks succeed.

### Failure & Cleanup Safeguards

- If ownership cannot be verified (`SqlServerTestDatabaseCleanupOutcome.OwnershipMismatch`), teardown throws without dropping the database.
- If capturing `database_id`/`service_broker_guid` fails immediately after `CREATE DATABASE`, or if the test process is forcefully terminated (`SIGKILL` / crash), an orphaned `DVLD_IntegrationTests_*` database may remain on the server and is **never** automatically reused.

---

## E. Running Tests

```powershell
# 1. Application Unit Tests (no SQL Server required)
dotnet test .\Test\Application.UnitTests\Application.UnitTests.csproj -c Release --no-restore

# 2. LocalDrivingLicenseApplicationRepositoryTests (requires SQL Server env vars)
dotnet test .\Test\Infrastructure.IntegrationTests\Infrastructure.IntegrationTests.csproj -c Release --no-restore --filter "FullyQualifiedName~LocalDrivingLicenseApplicationRepositoryTests"

# 3. Full Infrastructure.IntegrationTests (requires SQL Server env vars)
dotnet test .\Test\Infrastructure.IntegrationTests\Infrastructure.IntegrationTests.csproj -c Release --no-restore

# 4. API Controller Tests (uses ApiWebApplicationFactory mocks; no SQL Server required)
dotnet test .\Test\API.IntegrationTests\API.IntegrationTests.csproj -c Release --no-restore --filter "FullyQualifiedName~Controllers"

# 5. API SQL Server Workflow Tests (requires SQL Server env vars)
dotnet test .\Test\API.IntegrationTests\API.IntegrationTests.csproj -c Release --no-restore --filter "FullyQualifiedName~Workflows"

# 6. Full API.IntegrationTests (requires SQL Server env vars)
dotnet test .\Test\API.IntegrationTests\API.IntegrationTests.csproj -c Release --no-restore
```

> **Note on `Debug` vs `Release`:** If `DVLD.Api` is running locally in `Debug` mode, `API\DVLD.Api\bin\Debug\net8.0\*.dll` may be locked by the running process. Building and running tests with `-c Release` avoids this file lock without stopping your dev server.

---

## F. Troubleshooting

| Symptom | Likely Cause | Classification | Resolution |
|---|---|---|---|
| `DVLD_TEST_CONNECTION must be explicitly configured.` | Env var missing in current shell | Environment | Run `.\scripts\testing\Test-DatabaseIntegrationEnvironment.ps1 -Setup`. |
| `DVLD_TEST_EXPECTED_INSTANCE must identify the approved SQL Server instance.` | Expected instance var missing | Environment | Run `.\scripts\testing\Test-DatabaseIntegrationEnvironment.ps1 -Setup`. |
| `MSSQLSERVER` service is `Stopped` | SQL Server service not running | Environment | Start `SQL Server (MSSQLSERVER)` in Services / SQL Server Configuration Manager. |
| Connection timeout / login failed | Service stopped, TCP disabled, or Windows login unauthorized | Environment | Verify `MSSQLSERVER` is running and current Windows user has SQL Server login & `dbcreator` permissions. |
| `The connected SQL Server instance does not match DVLD_TEST_EXPECTED_INSTANCE.` | Wrong instance string or stale env var | Environment | Query `SERVERPROPERTY('ServerName') + '|' + @@SERVICENAME` and update `DVLD_TEST_EXPECTED_INSTANCE`. |
| `CREATE DATABASE permission denied` | Windows login lacks `CREATE ANY DATABASE` / `dbcreator` | Environment | Grant `dbcreator` or `CREATE ANY DATABASE` on the test SQL Server instance. |
| EF Core migration failure during fixture setup | Schema/migration defect or SQL version incompatibility | Code / Schema | Inspect `migrationException` in test output; verify `DVLDDbContext` migrations. |
| `The test database was not dropped because ownership could not be confirmed.` | `DVLD_Test_Run_Token` missing or mismatched | Environment / Fixture | Inspect `sys.databases` for `DVLD_IntegrationTests_*`; do not drop unknown databases. |
| `MSB3027` / `MSB3021` file lock on `DVLD.Api\bin\Debug` | `DVLD.Api.exe` is running in Debug | Process Lock | Use `-c Release` for `dotnet build` / `dotnet test`, or stop the running dev API process. |

---

## G. Safety Rules

- **Never** use `DVLD` or `DVLDf` as a test database.
- **Never** delete an unknown database manually.
- **Never** bypass `VerifyExpectedInstanceAsync` or disable ownership checks.
- **Never** report blocked or skipped tests as passed.
- A successful `dotnet build` does **not** prove SQL Server runtime compatibility or EF Core LINQ-to-SQL translation.

---

## H. Verified Execution Record (Local Baseline)

> **Disclaimer:** This is a historical local execution record for reference only, not a universal expected test count.

- **Date:** `2026-11-17`
- **Suite:** `LocalDrivingLicenseApplicationRepositoryTests`
- **Total:** `74` | **Passed:** `74` | **Failed:** `0`
- **Execution Time:** `16.855 seconds`
- **SQL Server Instance:** `Ayhm|MSSQLSERVER`
- **Authentication:** Windows Authentication
