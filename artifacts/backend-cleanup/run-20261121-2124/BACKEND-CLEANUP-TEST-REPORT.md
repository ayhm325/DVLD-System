# Backend Cleanup Test Report (`DVLD.sln`)

**Baseline Commit:** `3181e804c439320d22fe684cb77a44b725c1f0e6` (`development`)  
**Configuration:** `Release` (`--no-restore -p:UseSharedCompilation=false -m:1`; NuGet restore skipped in offline sandbox)

---

## 1. Baseline vs Final Results

| Suite | Baseline | Final | Status |
|---|---|---|---|
| **Full Solution Build (`DVLD.sln`)** | `0` warnings, `0` errors | `0` warnings, `0` errors | **Passed** |
| **`Application.UnitTests`** | `983` passed, `0` failed, `0` skipped | `971` passed, `0` failed, `0` skipped | **Passed** (`-15` wrapper tests from deleted `LicenseServiceTests.cs` + `3` new tests; `LicenseQueryServiceTests.cs` retains `40` unit tests) |
| **Database-Free `API.IntegrationTests`** | `574` passed, `0` failed, `0` skipped | `574` passed, `0` failed, `0` skipped | **Passed** (`LicensesControllerTests` switched to `Mock<ILicenseQueryService>`) |
| **SQL-Backed `API.IntegrationTests` (`Auth`, `Workflows`)** | Not Executed | Not Executed | **Skipped** (`DVLD_TEST_CONNECTION` and `DVLD_TEST_EXPECTED_INSTANCE` not set in sandbox) |
| **`Infrastructure.IntegrationTests`** | Built (`0` errors) | Built (`0` errors) | **Not Executed** (`DVLD_TEST_CONNECTION` and `DVLD_TEST_EXPECTED_INSTANCE` not set in sandbox) |

---

## 2. Newly Added Regression Tests (`Test/Application.UnitTests/Services/ApiDataEfficiencyOptimizationTests.cs`)

1. `LicenseQueryService_GetAllAsync_WhenIsExpiredTrue_ChecksExternalNewerLicenses`
2. `PagedResult_Map_PreservesMetadataAndProjectsItems`
3. `CurrentUserServiceExtensions_IsAuthenticated_RequiresLoggedInAndPositiveUserId`

---

## 3. Remaining Verification Gaps

- SQL-backed integration suites (`Infrastructure.IntegrationTests` and `API.IntegrationTests` `Auth`/`Workflows`) require an isolated SQL Server instance configured via `DVLD_TEST_CONNECTION` and `DVLD_TEST_EXPECTED_INSTANCE` (e.g., via `scripts/testing/Test-DatabaseIntegrationEnvironment.ps1 -Setup`).
