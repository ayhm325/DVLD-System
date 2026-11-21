# Database Design

## 1. Overview

Persistence is managed by **Entity Framework Core 10** (`Microsoft.EntityFrameworkCore.SqlServer` `10.0.15`) through `Infrastructure/DVLDDbContext.cs` and **15** Fluent API configuration classes under `Infrastructure/Configurations/`.

The schema comprises **15 SQL Server tables** managed across **7 migrations** under `Infrastructure/Migrations/` (with `DVLDDbContextModelSnapshot` recording `ProductVersion` `"8.0.18"`).

---

## 2. Table Inventory

| Table | Entity | Primary Configuration Highlights |
|---|---|---|
| `Countries` | `Country` | Reference lookup; unique index `IX_Countries_CountryName`. |
| `People` | `Person` | Civic identity; unique index `IX_People_NationalNo`; `RowVersion` configured with `IsRowVersion()` optimistic concurrency token. |
| `Users` | `User` | System accounts; unique indexes `IX_Users_UserName` and `IX_Users_PersonId`; stores BCrypt password hash and `SecurityStamp`. |
| `UserSessions` | `UserSession` | Refresh token families; index `IX_UserSessions_RefreshTokenHash`; `Cascade` delete on `UserId`. |
| `ApplicationTypes` | `ApplicationType` | 7 licensing service categories and fees. |
| `Applications` | `ApplicationD` | Master licensing application records (`ApplicantPersonID`, `ApplicationTypeID`, `ApplicationStatus`, `PaidFees`, `CreatedByUserID`). |
| `LicenseClasses` | `LicenseClass` | License categories, minimum allowed ages, default validity lengths, and fees. |
| `LocalDrivingLicenseApplications` | `LocalDrivingLicenseApplication` | Links `ApplicationD` to `LicenseClass`; unique index `UX_LocalDrivingLicenseApplications_ApplicationID`. |
| `TestTypes` | `TestType` | Examination stage definitions (`Theory`, `Written`, `Practical`) and fees. |
| `TestAppointments` | `TestAppointment` | Scheduled examination slots; configured with `UseSqlOutputClause(false)`. |
| `Tests` | `Test` | Examination outcomes; unique/lookup index `IX_Tests_TestAppointmentID`. |
| `Drivers` | `Driver` | Licensed drivers; unique index `IX_Drivers_PersonID`. |
| `Licenses` | `License` | Local licenses; index `IX_Licenses_ApplicationID`; filtered unique index `IX_Licenses_DriverID_LicenseClass` (`[IsActive] = 1`). |
| `DetainedLicenses` | `DetainedLicense` | Detention & release records; filtered unique index `IX_DetainedLicenses_LicenseID_IsReleased` (`[IsReleased] = 0`). |
| `InternationalLicenses` | `InternationalLicense` | International permits; index `IX_InternationalLicenses_IssuedUsingLocalLicenseID`; filtered unique index `IX_InternationalLicenses_DriverID` (`[IsActive] = 1`). |

---

## 3. Notable Indexes & Concurrency Controls

- **Filtered Unique Indexes** (enforce business invariants at the database engine level):
  - `IX_Licenses_DriverID_LicenseClass` (`WHERE [IsActive] = 1`): Guarantees at most one active local license per `(DriverID, LicenseClass)`.
  - `IX_InternationalLicenses_DriverID` (`WHERE [IsActive] = 1`): Guarantees at most one active international license per driver.
  - `IX_DetainedLicenses_LicenseID_IsReleased` (`WHERE [IsReleased] = 0`): Guarantees at most one active (unreleased) detention per local license.
- **Unique Identity & Linking Indexes**:
  - `IX_People_NationalNo`, `IX_Users_UserName`, `IX_Users_PersonId`, `IX_Drivers_PersonID`, `UX_LocalDrivingLicenseApplications_ApplicationID`, `IX_Countries_CountryName`.
- **SQL Server Triggers / Output Clause**:
  - `TestAppointments` disables the `OUTPUT` clause via `UseSqlOutputClause(false)` in its EF Core configuration.
- **Optimistic Concurrency**:
  - `People.RowVersion` (`IsRowVersion()`) prevents lost updates on concurrent person and photo edits.

---

## Implementation References

- DbContext & configurations: [`Infrastructure/DVLDDbContext.cs`](../../Infrastructure/DVLDDbContext.cs), [`Infrastructure/Configurations/`](../../Infrastructure/Configurations/)
- Migrations & snapshot: [`Infrastructure/Migrations/`](../../Infrastructure/Migrations/), [`Infrastructure/Migrations/DVLDDbContextModelSnapshot.cs`](../../Infrastructure/Migrations/DVLDDbContextModelSnapshot.cs)
