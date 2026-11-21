# Workflow: License Replacement

## 1. Purpose

Replaces an **active, non-expired** local driving license that was lost (`ApplicationTypeID = 3`, `IssueReason.ReplacementForLost`) or damaged (`ApplicationTypeID = 4`, `IssueReason.ReplacementForDamaged`), preserving the original expiration date.

---

## 2. Execution Sequence (`LicenseReplacementService.ReplaceLicenseAsync`)

```mermaid
sequenceDiagram
    participant C as ClientApp
    participant Ctrl as LicenseReplacementController
    participant Svc as LicenseReplacementService
    participant DB as SQL Server (Serializable Tx)

    C->>Ctrl: POST /api/LicenseReplacement
    Ctrl->>Svc: ReplaceLicenseAsync(oldLicenseId, replacementReason)
    Svc->>Svc: Validate ID, auth, reason ("Lost License" -> 3 | "Damaged License" -> 4)
    Svc->>DB: Begin Serializable Transaction
    Svc->>DB: GetForUpdateAsync(oldLicenseId)
    Svc->>Svc: Verify !IsExpired(oldLicense.ExpirationDate, _clock.Today) & oldLicense.IsActive
    Svc->>DB: Create ApplicationD (Type 3 or 4)
    Svc->>DB: DeactivateLicenseAsync(oldLicenseId)
    Svc->>DB: Insert new License (preserves oldLicense.ExpirationDate)
    Svc->>DB: CompleteApplicationStateAsync(applicationId) & Commit
    Svc-->>Ctrl: 200 OK (ReplaceLicenseResponse)
```

---

## 3. Business Rules & Guards

- **Supported Reasons**: `replacementReason` must match `"Lost License"` (`LostReplacementApplicationTypeId = 3`) or `"Damaged License"` (`DamagedReplacementApplicationTypeId = 4`), case-insensitively.
- **Active & Non-Expired**: Expired or inactive licenses cannot be replaced (`409 Conflict`).
- **Expiration Preservation**: Unlike renewal, replacement **preserves `oldLicense.ExpirationDate`** on the newly issued license (`PaidFees = oldLicense.LicenseClassInfo.ClassFees`, `Notes = replacementReason`).
- **Deactivation**: The old license is deactivated (`DeactivateLicenseAsync`) within the same `Serializable` transaction before inserting the replacement.

---

## Implementation References

- Controller: [`API/DVLD.Api/Controllers/LicenseReplacementController.cs`](../../../API/DVLD.Api/Controllers/LicenseReplacementController.cs)
- Service: [`Application/Services/LicenseReplacementService.cs`](../../../Application/Services/LicenseReplacementService.cs)
- Validator: [`Application/Validators/LicenseValidator.cs`](../../../Application/Validators/LicenseValidator.cs)
