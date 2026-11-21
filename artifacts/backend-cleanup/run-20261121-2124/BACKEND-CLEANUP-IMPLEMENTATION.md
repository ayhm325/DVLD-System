# Backend Cleanup Implementation (`DVLD.sln`)

All changes are strictly scoped to `API/DVLD.Api`, `Application`, `Infrastructure`, and `Test`. Zero changes to `ClientApp/**`, `DVLD.Contracts/**`, `Infrastructure/Migrations/**`, or `Infrastructure/DVLDDbContextFactory.cs`.

---

## P1 — Low-Risk Cleanup

1. **B-01 (`PropagateFailure<T>` -> `Result<T>.FromResult`):**
   - Replaced all 20 `PropagateFailure<T>` call sites across `LicenseIssuanceService.cs`, `LicenseRenewalService.cs`, `LicenseReplacementService.cs`, and `InternationalService.cs` with `Result<T>.FromResult(...)`, and deleted the 4 duplicate private helpers.
   - Equivalence: Every call site is preceded by `.IsFailure` / `!IsSuccess`; `Result<T>.FromResult` preserves exact `Error` and `ErrorType` across all `ErrorType` values.
2. **B-02 (Dead Code in `LicenseIssuanceService.cs`):**
   - Removed unused `RollbackAsync<T>`, `RollbackSafelyAsync`, `ILogger<LicenseIssuanceService>` constructor parameter, and `_logger` field.
   - Updated constructor calls in `LicenseIssuanceServiceTests.cs`, `ApplicationCancellationConcurrencyRegressionTests.cs`, and `RepositoryConcurrencyLostUpdateTests.cs`.
3. **B-03 (Redundant `LicenseService` / `ILicenseService` Wrapper):**
   - Injected `ILicenseQueryService` directly into `LicensesController.cs`.
   - Removed `ILicenseService` registration from `DependencyInjectionExtensions.cs`, updated `ApiWebApplicationFactory.cs` (`Mock<ILicenseQueryService>`), and deleted `LicenseService.cs`, `ILicenseService.cs`, and `LicenseServiceTests.cs` (`LicenseQueryServiceTests.cs` already provides 40 unit tests for `LicenseQueryService`).

## P2 — Shared Implementation Improvements

4. **B-04 (`PagedResult<T>.Map<TResult>`):**
   - Added `PagedResult<T>.Map<TResult>(Func<T, TResult> mapper)` in `Application/Common/Pagination/PagedResult.cs`.
   - Simplified `GetAllInternalAsync` in `PersonService.Queries.cs`, `DriverService.cs`, `TestAppointmentService.Queries.cs`, `InternationalService.cs`, and `DetainedLicenseService.cs`.
   - Kept `LocalDrivingLicenseApplicationService.cs` and `LicenseQueryService.cs` separate due to async batched DTO enrichment.
5. **B-05 (`CurrentUserServiceExtensions.IsAuthenticated`):**
   - Added `CurrentUserServiceExtensions.IsAuthenticated(this ICurrentUserService)` in `Application/Interfaces/ICurrentUserService.cs`.
   - Removed the 9 duplicated `private bool IsAuthenticated()` methods across services and updated all call sites (including `TestAppointmentService.Lifecycle.cs` and `TestAppointmentService.Scheduling.cs`).
6. **B-06 (Mapper Namespace Unification):**
   - Consolidated `CountryMapper.cs`, `PersonMapper.cs`, and `UserMapper.cs` from `Application.Mappings` to `Application.Mappers` (matching the other 8 mappers under `Application/Mappings/`) and updated the 7 `using` directives.

## P3 — Verified Performance & Correctness Improvements

7. **B-07 (`LicenseQueryService` & `LicenseRepository`):**
   - Fixed `LicenseQueryService.GetAllAsync(bool? isExpired)` to pass `checkExternalNewerIfMissing: isExpired.HasValue` so expired-only filtered queries still detect unexpired renewals/replacements.
   - Replaced `O(N²)` `.Any(...)` / `.Where(...).OrderByDescending(...).FirstOrDefault()` scans in `LicenseQueryService.MapToDtosBatchAsync` and `LicenseRepository.GetNewerLicensesBatchAsync` with `O(N)` `Dictionary<(int DriverID, int LicenseClass), License>` lookups.
   - Added 3 regression tests in `Test/Application.UnitTests/Services/ApiDataEfficiencyOptimizationTests.cs`.
