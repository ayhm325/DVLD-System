# Workflow: License Renewal

## 1. Purpose

Renews an **expired**, non-detained local driving license by creating a `Renew Driving License` application (`ApplicationTypeID = 2`), deactivating the superseded license if still active, and issuing a new active license with a fresh validity period.

---

## 2. Execution Sequence (`LicenseRenewalService.RenewLicenseAsync`)

```mermaid
sequenceDiagram
    participant C as ClientApp
    participant Ctrl as LicenseRenewalController
    participant Svc as LicenseRenewalService
    participant DB as SQL Server (Serializable Tx)

    C->>Ctrl: POST /api/LicenseRenewal
    Ctrl->>Svc: RenewLicenseAsync(oldLicenseId, notes)
    Svc->>Svc: Validate ID, auth, ApplicationTypeID = 2
    Svc->>DB: Begin Serializable Transaction
    Svc->>DB: GetForUpdateAsync(oldLicenseId)
    Svc->>Svc: Verify IsExpired(oldLicense.ExpirationDate, _clock.Today)
    Svc->>DB: Verify !IsLicenseDetainedAsync(oldLicenseId)
    Svc->>DB: Verify GetNewerLicenseForDriverAndClassAsync == null
    Svc->>DB: Verify !HasAnotherActiveLicenseAsync
    Svc->>DB: Create ApplicationD (ApplicationTypeID = 2)
    alt oldLicense.IsActive
        Svc->>DB: DeactivateLicenseAsync(oldLicenseId)
    end
    Svc->>DB: Insert new License (IssueReason.Renew)
    Svc->>DB: CompleteApplicationStateAsync(applicationId) & Commit
    Svc-->>Ctrl: 200 OK (RenewLicenseResponse)
```

---

## 3. Business Rules & Guards

- **Expiration Requirement**: Renewal is rejected with `409 Conflict` (`"Cannot renew before expiration date."`) if `!LicenseExpirationPolicy.IsExpired(oldLicense.ExpirationDate, _clock.Today)`.
- **Detention Guard**: Detained licenses cannot be renewed (`409 Conflict`).
- **Supersession & Uniqueness Guards**: Rejected if a newer license already exists for the same `(DriverID, LicenseClass)` or if another active license exists in that class (`409 Conflict`).
- **Validity & Reason**: New license sets `IssueReason.Renew`, `ExpirationDate = now.AddYears(DefaultValidityLength)`, and `IsActive = true`.
- **Rollback**: Explicit `RollbackAsync` is invoked on every failure branch inside the `Serializable` transaction.

---

## Implementation References

- Controller: [`API/DVLD.Api/Controllers/LicenseRenewalController.cs`](../../../API/DVLD.Api/Controllers/LicenseRenewalController.cs)
- Service: [`Application/Services/LicenseRenewalService.cs`](../../../Application/Services/LicenseRenewalService.cs)
- Validator: [`Application/Validators/LicenseValidator.cs`](../../../Application/Validators/LicenseValidator.cs)
