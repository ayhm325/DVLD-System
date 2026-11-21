# Workflow: License Detention & Release

## 1. Purpose

Manages the administrative detention of an active, non-expired local license with an associated fine, and its subsequent release via a `Release Detained Driving License` application (`ApplicationTypeID = 5`).

---

## 2. Detention Flow (`DetainedLicenseService.AddAsync`)

```mermaid
sequenceDiagram
    participant C as ClientApp
    participant Ctrl as DetainedLicensesController
    participant Svc as DetainedLicenseService
    participant DB as SQL Server (Serializable Tx)

    C->>Ctrl: POST /api/DetainedLicenses
    Ctrl->>Svc: AddAsync(request)
    Svc->>Svc: Validate LicenseID > 0, 0 <= FineFees <= 9_999_999_999_999_999.99, auth
    Svc->>DB: Begin Serializable Transaction
    Svc->>DB: GetForUpdateAsync(LicenseID)
    Svc->>Svc: Verify !IsExpired, IsActive, !IsLicenseDetainedAsync
    Svc->>DB: Insert DetainedLicense (IsReleased = false)
    Svc->>DB: DeactivateLicenseAsync(LicenseID)
    Svc->>DB: SaveChangesAsync & Commit
    Svc-->>Ctrl: 200 OK (DetainedLicenseResponse)
```

---

## 3. Release Flow (`DetainedLicenseService.ReleaseAsync`)

```mermaid
sequenceDiagram
    participant C as ClientApp
    participant Ctrl as DetainedLicensesController
    participant Svc as DetainedLicenseService
    participant DB as SQL Server (Serializable Tx)

    C->>Ctrl: POST /api/DetainedLicenses/release
    Ctrl->>Svc: ReleaseAsync(request)
    Svc->>DB: Begin Serializable Transaction
    Svc->>DB: GetByIdForUpdateAsync(DetainID) [must exist & !IsReleased]
    Svc->>DB: GetForUpdateAsync(detention.LicenseID)
    Svc->>DB: Create ApplicationD (ApplicationTypeID = 5)
    Svc->>DB: Update DetainedLicense (IsReleased = true, ReleaseDate, ReleaseApplicationID)
    alt !HasAnotherActiveLicenseAsync AND !IsExpired
        Svc->>DB: ActivateLicenseAsync(LicenseID, today)
    end
    Svc->>DB: CompleteApplicationStateAsync(applicationId) & Commit
    Svc-->>Ctrl: 204 NoContent
```

---

## 4. Key Invariants

- **Filtered Unique Detention Index**: `IX_DetainedLicenses_LicenseID_IsReleased` (`[IsReleased] = 0`) guarantees at most one active detention per license at the database level.
- **Conditional Reactivation**: Releasing a detained license reactivates it **only if** the license has not expired and the driver does not already hold another active license in the same class.

---

## Implementation References

- Controller: [`API/DVLD.Api/Controllers/DetainedLicensesController.cs`](../../../API/DVLD.Api/Controllers/DetainedLicensesController.cs)
- Service: [`Application/Services/DetainedLicenseService.cs`](../../../Application/Services/DetainedLicenseService.cs)
- Validator: [`Application/Validators/DetainedLicenseValidator.cs`](../../../Application/Validators/DetainedLicenseValidator.cs)
