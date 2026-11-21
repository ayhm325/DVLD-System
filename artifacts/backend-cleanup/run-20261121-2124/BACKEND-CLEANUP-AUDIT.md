# Backend Cleanup Audit (`DVLD.sln`)

**Baseline Commit:** `3181e804c439320d22fe684cb77a44b725c1f0e6` (`development`)  
**Audit Date:** 2026-11-21  
**Architectural Boundaries:** Verified compliant (`Domain` has 0 project refs; `Application` refs `Domain` only; `Infrastructure` refs `Application` + `Domain`; `DVLD.Api` refs `Application`, `Infrastructure`, `DVLD.Contracts` with `Infrastructure` isolated to composition root `Configuration/`).

---

## Confirmed Findings & Classification

| ID | Classification | File(s) & Location | Summary | Action |
|---|---|---|---|---|
| B-01 | Confirmed Duplication | `LicenseIssuanceService.cs:L288-296`, `LicenseRenewalService.cs:L244-253`, `LicenseReplacementService.cs:L210-219`, `InternationalService.cs:L327-336` | Identical `PropagateFailure<T>(Result)` helpers (20 callers) duplicating `Result<T>.FromResult(Result)` | **Replaced with `Result<T>.FromResult`** |
| B-02 | Confirmed Dead Code | `LicenseIssuanceService.cs:L23,L46-47,L262-286` | Unused `RollbackAsync<T>`, `RollbackSafelyAsync`, and transitively unused `ILogger<LicenseIssuanceService>` / `_logger` | **Removed** |
| B-03 | Redundant Abstraction | `LicenseService.cs`, `ILicenseService.cs`, `LicensesController.cs:L14`, `DependencyInjectionExtensions.cs:L97` | 15-method 1:1 pass-through wrapper delegating directly to `ILicenseQueryService` with zero added behavior | **Removed; injected `ILicenseQueryService` directly** |
| B-04 | Confirmed Duplication | `PersonService.Queries.cs`, `DriverService.cs`, `TestAppointmentService.Queries.cs`, `InternationalService.cs`, `DetainedLicenseService.cs` | Identical `GetAllInternalAsync` pagination DTO projection blocks | **Extracted `PagedResult<T>.Map<TResult>`** |
| B-05 | Confirmed Duplication | 9 services (`ApplicationService`, `DetainedLicenseService`, `DriverService`, `InternationalService`, `LicenseIssuanceService`, `LicenseRenewalService`, `LicenseReplacementService`, `LocalDrivingLicenseApplicationService`, `TestAppointmentService`) | Identical `private bool IsAuthenticated() => _currentUserService.IsLoggedIn && _currentUserService.UserId > 0;` | **Consolidated into `ICurrentUserService.IsAuthenticated()` extension** |
| B-06 | Architectural Inconsistency | `CountryMapper.cs`, `PersonMapper.cs`, `UserMapper.cs` (`Application.Mappings`) vs 8 mappers in `Application.Mappers` | Split namespaces under `Application/Mappings/` (causing `LicenseQueryService.cs` to import both) | **Unified to `Application.Mappers`** |
| B-07 | Performance Inefficiency + Bug | `LicenseQueryService.cs:L51,L221-282`, `LicenseRepository.cs:L466-489` | `GetAllAsync(bool? isExpired)` passed `checkExternalNewerIfMissing: false` (missed newer renewals for filtered expired lists); `O(N²)` in-memory scans in `MapToDtosBatchAsync` & `GetNewerLicensesBatchAsync` | **Fixed `isExpired.HasValue` + `O(N)` dictionary lookups** |
| B-08 | Retained (Safety) | `UnitOfWork*.cs` & explicit `RollbackAsync(transaction, result)` in 5 services | Explicit rollback helpers pass `CancellationToken.None` whereas `UnitOfWork.cs:L134` passes request `cancellationToken` | **Deliberately retained** |
| B-09 | Retained (Tooling / Tests) | `DVLDDbContextFactory.cs`; unused `_logger` fields in `DetainedLicenseService`, `InternationalService`, `LicenseReplacementService`, `TestAppointmentService` | `DVLDDbContextFactory` is discovered implicitly by EF tooling; the 4 other unused `_logger` params are left untouched to avoid churn across integration test constructors | **Deliberately retained** |
