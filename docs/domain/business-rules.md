# Business Rules

This document catalogs the authoritative validation rules, eligibility checks, fee/date calculations, and concurrency invariants enforced across `Application/Validators/` and `Application/Services/`.

---

## 1. Person & Civic Identity Rules (`PersonValidator`, `PersonService`)

- **National Number (`NationalNo`)**: Must be a unique 10-digit identifier (`IsNationalNoDuplicatedAsync`).
- **Names**: `FirstName`, `SecondName`, `ThirdName`, and `LastName` are trimmed and capped at `<= 50` characters.
- **Age**: Must be between **18 and 120 years** old relative to Jordan local date (`_clock.Today`).
- **Phone**: Must match Jordanian mobile format `^(077|078|079)\d{7}$`.
- **Email**: Optional/validated against email format, maximum `100` characters.
- **Address**: Maximum `200` characters.
- **Nationality & Gender**: Requires valid `Gender` enum and `NationalityCountryID > 0`.
- **Optimistic Concurrency**: Updates require a valid Base64-encoded 8-byte `RowVersion` token; mismatches return `409 Conflict`.
- **Deletion Guard**: A person with existing applications (`HasApplicationsAsync`) cannot be deleted (`409 Conflict`).

---

## 2. User Account & Administration Rules (`UserValidator`, `UserService`)

- **Username**: `3`–`20` characters matching `^[a-zA-Z][a-zA-Z0-9_]*$`; reserved names `["admin", "root", "system", "null"]` are rejected.
- **Password**: `8`–`100` characters, hashed with BCrypt (`BCrypt.Net.BCrypt.HashPassword`). On password change, `NewPassword` must differ from `CurrentPassword`.
- **Role & Uniqueness**: Valid `UserRole` (`Admin` or `Staff`); 1:1 binding to `PersonId`; duplicate username or `PersonId` returns `409 Conflict`.
- **Admin Safety Guards**:
  - Only `Admin` callers can create, update, delete, or list all users (`Forbidden` otherwise).
  - Primary admin (`id == 2 && Role == Admin`) cannot be deactivated, have role changed, or have `"ayhm"` username/`PersonId` modified.
  - Admins cannot deactivate or demote themselves, nor delete themselves.
  - Last active admin cannot be deactivated, demoted, or deleted (`GetActiveAdminCountAsync <= 1` under `AcquireAdminMutationLockAsync`).
  - Role or `IsActive` changes rotate `SecurityStamp` and revoke all active sessions (`SecurityStateChanged`).

---

## 3. Local License Application Rules (`LocalDrivingLicenseApplicationService`, `LocalDrivingLicenseApplicationValidator`)

- **Application Type**: Always uses `ApplicationTypeID = 1` (New Local Driving License).
- **Minimum Age**: Applicant's exact age on `_clock.Now` must be `>= licenseClass.MinimumAllowedAge` (and `DateOfBirth <= today`).
- **Duplicate Prevention**: An applicant cannot hold another active/duplicate local application for the same `LicenseClassID` (`HasDuplicateApplicationAsync` inside `IsolationLevel.Serializable`).
- **Mutation & Cancellation Guards**: Only applications in `AppStatus.New` may be updated, deleted, or cancelled. Cancelling a local application also cancels and locks all active `Scheduled` test appointments for that local application.

---

## 4. Test Appointment & Examination Rules (`TestAppointmentValidator`, `TestAppointmentService`, `TestWorkflowService`, `TestService`, `NoShowProcessorService`)

- **Required Test Sequence**: `Theory` -> `Written` -> `Practical` (`TestWorkflowService.RequiredTests`). Tests cannot be scheduled or taken out of order, nor after all three are passed, nor when the parent application is not `AppStatus.New`.
- **Working Days & Time Slots (Jordan Facility Time)**:
  - Facility operates Sunday–Thursday (**Friday and Saturday are excluded**).
  - 12 valid 40-minute slots from **`08:00` through `15:20`** (`ValidSlotTimesSet`), with zero seconds and zero fractional ticks.
- **Two-Day Cutoff**:
  - Booking (`AppointmentDate`), rescheduling, and cancellation all require `(facilityDate.Date - currentJordanDate).Days >= 2`.
- **Single Active Appointment & Slot Exclusivity**:
  - Only one active scheduled appointment per `(localAppId, testTypeId)` is allowed.
  - Same-day applicant conflicts (`HasApplicantSameDayConflictAsync`) and occupied slot conflicts (`IsSlotOccupiedAsync`) are prevented under `AcquireSchedulingLocksAsync` (`5000ms` timeout).
  - After a prior test attempt, `earliestEligibleDate = GetFirstWorkingDayAfter(lastTestDate)` is enforced in availability calculation.
- **Retake Applications**: On `ScheduleAsync`, if `trial = GetTrialCountAsync(...) + 1 > 1`, a retake fee application (`ApplicationTypeID = 7`) is automatically created and linked via `RetakeTestApplicationID`.
- **Recording Results**:
  - Appointment must be `Scheduled`, unlocked (`!IsLocked`), `AppointmentDate <= _clock.Now`, and have no existing `Test` record.
  - Recording a result sets `Status = AppointmentStatus.Completed` and `IsLocked = true`.
  - `Notes` (trimmed) must be `<= 500` characters.
- **No-Show Processing**: Unexamined `Scheduled` appointments older than `GracePeriodMinutes` (default `40` minutes) are marked `AppointmentStatus.NoShow` and locked (`IsLocked = true`).

---

## 5. License Lifecycle Rules

| Operation | Application Type ID | Eligibility & Guard Rules |
|---|---|---|
| **First License Issuance** | `1` (existing) | Local app must be `AppStatus.New`, all 3 tests passed (`HasPassedAllTestsAsync`), no existing license for `ApplicationID`, and no active license for `(DriverID, LicenseClassID)`. Creates `Driver` if missing. Validity = `DefaultValidityLength` years. |
| **License Renewal** | `2` | Old license must be **expired** (`LicenseExpirationPolicy.IsExpired`), **not detained**, not already superseded by a newer license (`GetNewerLicenseForDriverAndClassAsync == null`), and driver must have no other active license in that class. Deactivates old license if active; sets `IssueReason.Renew`. |
| **License Replacement** | `3` (Lost) / `4` (Damaged) | Old license must be **active** and **not expired**. Preserves `oldLicense.ExpirationDate` on the new license, charges `LicenseClassInfo.ClassFees`, deactivates old license, and sets `IssueReason.ReplacementForLost` or `ReplacementForDamaged`. |
| **Detention** | N/A | Target license must be **active**, **not expired**, and **not already detained**. `FineFees` must be in `[0, 9_999_999_999_999_999.99]`. Deactivates the license immediately. |
| **Release Detained** | `5` | Detention must exist and `!IsReleased`. Creates Release application (type `5`), marks detention released, and reactivates the license **only if** it is not expired and the driver has no other active license in that class. |
| **International License** | `6` | Local license must be **Class 3 (Ordinary)** (`OrdinaryLicenseClassId = 3`), **active**, **not expired**, have no existing international license for that local license, and driver must have no active international license. Validity = **1 year** (`InternationalLicenseValidityYears = 1`). |

---

## Implementation References

- Validators: [`Application/Validators/`](../../Application/Validators/)
- Services: [`Application/Services/`](../../Application/Services/)
