# Workflow: International License Issuance

## 1. Purpose

Issues a **1-year International Driving License** (`ApplicationTypeID = 6`, `InternationalLicenseValidityYears = 1`) backed by an active, non-expired **Class 3 (Ordinary)** local driving license (`OrdinaryLicenseClassId = 3`).

---

## 2. Execution Sequence (`InternationalService.IssueAsync`)

```mermaid
sequenceDiagram
    participant C as ClientApp
    participant Ctrl as InternationalLicensesController
    participant Svc as InternationalService
    participant DB as SQL Server (Serializable Tx)

    C->>Ctrl: POST /api/InternationalLicenses
    Ctrl->>Svc: IssueAsync(localLicenseId)
    Svc->>Svc: Validate localLicenseId > 0, auth, ApplicationTypeID = 6
    Svc->>DB: Begin Serializable Transaction
    Svc->>DB: Read local license (Class == 3, !IsExpired, IsActive, Driver != null)
    Svc->>DB: Verify !ExistsByLocalLicenseAsync(localLicenseId)
    Svc->>DB: Verify !HasActiveInternationalLicenseAsync(DriverID, today)
    Svc->>DB: Create ApplicationD (ApplicationTypeID = 6)
    Svc->>DB: Insert InternationalLicense (IsActive = true, Expiration = now + 1 year)
    Svc->>DB: CompleteApplicationStateAsync(applicationId) & Commit
    Svc-->>Ctrl: 200 OK (InternationalLicenseResponse)
```

---

## 3. Eligibility & Constraints

- **License Class Gate**: Only **Class 3 (Ordinary)** local licenses are eligible (`LicenseClassID == 3`).
- **Active & Non-Expired**: Local license must be active and not expired at issuance time.
- **Uniqueness**:
  - At most one international license per `IssuedUsingLocalLicenseID` (`ExistsByLocalLicenseAsync`).
  - At most one active international license per `DriverID` (`HasActiveInternationalLicenseAsync` + filtered unique index `IX_InternationalLicenses_DriverID` where `[IsActive] = 1`).
- **Pre-Flight Endpoint**: `GET /api/InternationalLicenses/license/{licenseId}/info` runs `GetLocalLicenseInfoAsync` to perform these eligibility checks before returning `DriverLicenseInfoResponse`.

---

## Implementation References

- Controller: [`API/DVLD.Api/Controllers/InternationalLicensesController.cs`](../../../API/DVLD.Api/Controllers/InternationalLicensesController.cs)
- Service: [`Application/Services/InternationalService.cs`](../../../Application/Services/InternationalService.cs)
- Validator: [`Application/Validators/InternationalLicenseValidator.cs`](../../../Application/Validators/InternationalLicenseValidator.cs)
