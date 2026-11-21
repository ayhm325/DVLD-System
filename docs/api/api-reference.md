# API Reference

## 1. Conventions, Pagination, and Error Format

- **Base Route**: All 20 controllers use `[ApiController]` and `[Route("api/[controller]")]`.
- **Content-Type**: JSON (`application/json`), except person photo upload (`multipart/form-data`) and photo download (image stream with `ETag` and `Cache-Control: private, max-age=86400, must-revalidate`).
- **Dual-Mode Pagination**: `DetainedLicenses`, `Drivers`, `InternationalLicenses`, `Licenses`, `People`, and `TestAppointments` return a paged envelope `{ items, pageNumber, pageSize, totalCount, totalPages, hasPreviousPage, hasNextPage }` (`PageNumber = 1`, `PageSize = 10`, `MaxPageSize = 100`) when pagination/filter query parameters are present, or an unpaged `List<T>` otherwise. `LocalDrivingLicenseApplications` and `GET /api/People/available-for-user` always return paged envelopes.
- **ProblemDetails (`application/problem+json`)**: All errors return RFC-style `ProblemDetails` / `ValidationProblemDetails` with `status`, `title`, `detail`, `instance`, `type` (`urn:dvld:error:{validation|unauthorized|forbidden|not-found|method-not-allowed|conflict|unsupported-media-type|too-many-requests|internal}`), and `traceId`.

---

## 2. Authentication & Account (`/api/Auth`)

Controller base: `[Authorize]`.

| Method & Route | Auth / Rate Limit | Request / Headers | Success Response |
|---|---|---|---|
| `POST /api/Auth/login` | `[AllowAnonymous]`, `LoginRateLimit` (5/min) | Body: `LoginRequest` | `200 OK` `LoginResponse` + sets `dvld_rt` cookie |
| `POST /api/Auth/refresh` | `[AllowAnonymous]`, `RefreshRateLimit` (30/min) | Header `X-CSRF-Protection: 1`, `dvld_rt` cookie | `200 OK` `LoginResponse` + rotates `dvld_rt` (`409` preserves cookie) |
| `POST /api/Auth/logout` | `[AllowAnonymous]` | Header `X-CSRF-Protection: 1`, optional Bearer/`dvld_rt` | `204 NoContent` + deletes `dvld_rt` |
| `POST /api/Auth/logout-all` | `[Authorize]` | Header `X-CSRF-Protection: 1` | `204 NoContent` + deletes `dvld_rt` |
| `GET /api/Auth/me` | `[Authorize]` | — | `200 OK` `{ userId, username, fullName, role }` |
| `GET /api/Auth/profile` | `[Authorize]` | — | `200 OK` `UserProfileResponse` |
| `POST /api/Auth/change-password` | `[Authorize]`, `ChangePasswordRateLimit` (5/min) | Body: `ChangePasswordRequest` | `200 OK` `LoginResponse` + rotates `dvld_rt` |

---

## 3. People & Drivers (`/api/People`, `/api/Drivers`)

### `PeopleController` (`[Authorize(Policy = "StaffOrAdmin")]`)

| Method & Route | Notes / Query / Body | Success Response |
|---|---|---|
| `GET /api/People` | `[FromQuery] PersonQueryRequest` (dual-mode paged/unpaged) | `200 OK` paged or `List<PersonListResponse>` |
| `GET /api/People/statistics` | — | `200 OK` `{ totalCount, maleCount, femaleCount }` |
| `GET /api/People/available-for-user` | `[FromQuery] PersonQueryRequest` (always paged) | `200 OK` paged `PersonListResponse` |
| `GET /api/People/{id:int}` | — | `200 OK` `PersonResponse` |
| `GET /api/People/national/{nationalNo}` | Sanitized in logs | `200 OK` `PersonResponse` |
| `POST /api/People` | `CreatePersonRequest` | `201 Created` `CreatePersonResponse` |
| `PUT /api/People/{id:int}` | `UpdatePersonRequest` (includes Base64 `RowVersion`) | `204 NoContent` |
| `DELETE /api/People/{id:int}` | — | `204 NoContent` |
| `POST /api/People/{id:int}/photo` | `multipart/form-data` (`IFormFile? file`) | `200 OK` `{ personId, imagePath }` |
| `GET /api/People/{id:int}/photo` | Supports `If-None-Match` -> `304 NotModified` | `200 OK` file stream + `ETag` |
| `DELETE /api/People/{id:int}/photo` | — | `204 NoContent` |

### `DriversController` (`[Authorize(Policy = "StaffOnly")]`)

| Method & Route | Notes / Body | Success Response |
|---|---|---|
| `GET /api/Drivers` | `[FromQuery] DriverQueryRequest` (dual-mode) | `200 OK` paged or `List<DriverListResponse>` |
| `GET /api/Drivers/{id:int}` | — | `200 OK` `DriverResponse` |
| `GET /api/Drivers/person/{personId:int}` | — | `200 OK` `DriverResponse` |
| `GET /api/Drivers/created-by/{userId:int}` | — | `200 OK` `List<DriverResponse>` |
| `POST /api/Drivers` | `CreateDriverRequest` | `201 Created` `{ driverId }` |
| `PUT /api/Drivers/{id:int}` | `UpdateDriverRequest` (`id == request.DriverId`) | `204 NoContent` |
| `DELETE /api/Drivers/{id:int}` | — | `204 NoContent` |

---

## 4. Users (`/api/Users`)

Controller policy: `[Authorize(Policy = "AdminOnly")]`.

| Method & Route | Notes / Body | Success Response |
|---|---|---|
| `GET /api/Users` | — | `200 OK` `List<UserResponse>` |
| `GET /api/Users/{id:int}` | — | `200 OK` `UserResponse` |
| `GET /api/Users/person/{personId:int}` | — | `200 OK` `UserResponse` |
| `GET /api/Users/{id:int}/details` | — | `200 OK` `UserDetailsResponse` |
| `GET /api/Users/username/{username}` | Sanitized in logs | `200 OK` `UserResponse` |
| `POST /api/Users` | `CreateUserRequest` | `201 Created` `int` (user ID) |
| `PUT /api/Users/{id:int}` | `UpdateUserRequest` | `204 NoContent` |
| `DELETE /api/Users/{id:int}` | — | `204 NoContent` |

---

## 5. Applications & Local Driving License Applications

### `ApplicationsController` (`[Authorize(Policy = "StaffOnly")]`)

- `GET /api/Applications` -> `List<ApplicationResponse>`
- `GET /api/Applications/{id:int}` -> `ApplicationResponse`
- `GET /api/Applications/{id:int}/basic-info` -> `ApplicationBasicInfoResponse`
- `POST /api/Applications` (`CreateApplicationRequest`) -> `201 Created` `{ applicationId }`
- `PUT /api/Applications/{id:int}` (`UpdateApplicationRequest`) -> `204 NoContent`
- `DELETE /api/Applications/{id:int}` -> `204 NoContent`
- `POST /api/Applications/{id:int}/cancel` -> `204 NoContent`

### `LocalDrivingLicenseApplicationsController` (`[Authorize(Policy = "StaffOnly")]`)

- `GET /api/LocalDrivingLicenseApplications` (`LocalDrivingLicenseApplicationQueryRequest`) -> always paged `LocalDrivingLicenseApplicationResponse`
- `GET /api/LocalDrivingLicenseApplications/summary` -> `LocalDrivingLicenseApplicationSummaryResponse`
- `GET /api/LocalDrivingLicenseApplications/{id:int}` -> `LocalDrivingLicenseApplicationResponse`
- `GET /api/LocalDrivingLicenseApplications/{localId:int}/application-basic-info` -> `ApplicationBasicInfoResponse`
- `GET /api/LocalDrivingLicenseApplications/application/{applicationId:int}` -> `List<LocalDrivingLicenseApplicationResponse>`
- `GET /api/LocalDrivingLicenseApplications/license-class/{licenseClassId:int}` -> `List<LocalDrivingLicenseApplicationResponse>`
- `GET /api/LocalDrivingLicenseApplications/person/{personId:int}` -> `List<LocalDrivingLicenseApplicationResponse>`
- `GET /api/LocalDrivingLicenseApplications/create-info` -> `CreateLocalDrivingLicenseApplicationInfoResponse`
- `GET /api/LocalDrivingLicenseApplications/{localId:int}/application-id` -> `{ applicationId }`
- `POST /api/LocalDrivingLicenseApplications` (`CreateLocalDrivingLicenseApplicationRequest`) -> `201 Created` `CreateLocalDrivingLicenseApplicationResponse`
- `PUT /api/LocalDrivingLicenseApplications/{id:int}` (`UpdateLocalDrivingLicenseApplicationRequest`) -> `204 NoContent`
- `DELETE /api/LocalDrivingLicenseApplications/{id:int}` -> `204 NoContent`
- `POST /api/LocalDrivingLicenseApplications/{id:int}/cancel` -> `204 NoContent`

---

## 6. Examinations (`/api/TestAppointments`, `/api/Tests`, `/api/TestWorkflow`)

All three controllers enforce `[Authorize(Policy = "StaffOnly")]`.

### `TestAppointmentsController`

- `GET /api/TestAppointments` (`TestAppointmentQueryRequest?`, dual-mode) -> paged or `List<TestAppointmentResponse>`
- `GET /api/TestAppointments/{id:int}` -> `TestAppointmentResponse`
- `GET /api/TestAppointments/local-application/{localAppId:int}` -> `List<TestAppointmentResponse>`
- `GET /api/TestAppointments/test-type/{testType:int}` -> `List<TestAppointmentResponse>`
- `GET /api/TestAppointments/schedule-preparation/{localAppId:int}/{testTypeId:int}` -> `ScheduleTestResponse`
- `GET /api/TestAppointments/{appointmentId:int}/schedule-info` -> `ScheduleTestResponse`
- `GET /api/TestAppointments/fees/{testTypeId:int}` -> `{ fees }`
- `GET /api/TestAppointments/trial-count?localAppId=&testTypeId=` -> `{ trialCount }`
- `GET /api/TestAppointments/scheduled?localAppId=&testTypeId=` -> `{ scheduled }`
- `GET /api/TestAppointments/attempts/{localAppId:int}/{testTypeId:int}` -> `ExaminationAttemptStatsResponse`
- `GET /api/TestAppointments/availability?localAppId=&testTypeId=&startDate=&endDate=` -> `AppointmentAvailabilityResponse`
- `POST /api/TestAppointments/schedule` (`ScheduleTestRequest`) -> `204 NoContent`
- `POST /api/TestAppointments` (`CreateTestAppointmentRequest`) -> `204 NoContent`
- `PUT /api/TestAppointments/{id:int}` (`UpdateTestAppointmentRequest`) -> `204 NoContent`
- `DELETE /api/TestAppointments/{id:int}` -> `204 NoContent`
- `POST` and `PUT /api/TestAppointments/{id:int}/cancel` -> `204 NoContent`

### `TestsController` & `TestWorkflowController`

- `GET /api/Tests`, `GET /api/Tests/{id:int}`, `GET /api/Tests/appointment/{appointmentId:int}`, `GET /api/Tests/created-by/{userId:int}` -> `TestResponse` / `List<TestResponse>`
- `POST /api/Tests` (`SaveTestResultRequest`) -> `200 OK` `int`
- `GET /api/TestWorkflow/can-schedule?localAppId=&testType=` -> `TestWorkflowResponse`
- `GET /api/TestWorkflow/next-test/{localAppId:int}` -> `TestWorkflowResponse`
- `GET /api/TestWorkflow/can-take/{appointmentId:int}` -> `TestWorkflowResponse`

---

## 7. Licenses, Detentions & International Permits

All controllers below enforce `[Authorize(Policy = "StaffOnly")]`.

- **Issuance, Renewal & Replacement**:
  - `POST /api/LicenseIssuance/first-license` (`IssueFirstLicenseRequest`) -> `200 OK` `IssueFirstLicenseResponse`
  - `POST /api/LicenseRenewal` (`RenewLicenseRequest`) -> `200 OK` `RenewLicenseResponse`
  - `POST /api/LicenseReplacement` (`ReplaceLicenseRequest`) -> `200 OK` `ReplaceLicenseResponse`
- **`LicensesController` (`/api/Licenses`)**:
  - `GET /api/Licenses` (`LicenseQueryRequest`, dual-mode) -> paged or `List<LicenseResponse>`
  - `GET /api/Licenses/renewal-summary` -> `{ totalExpired, eligibleForRenewal, ineligibleForRenewal }`
  - `GET /api/Licenses/{id:int}`, `/driver/{driverId:int}`, `/application/{applicationId:int}`, `/license-class/{licenseClassId:int}`, `/person/{personId:int}` -> `LicenseResponse` / `List<LicenseResponse>`
  - `GET /api/Licenses/local-application/{localAppId:int}/details`, `GET /api/Licenses/{licenseId:int}/details` -> `DriverLicenseInfoResponse`
- **`DetainedLicensesController` (`/api/DetainedLicenses`)**:
  - `GET /api/DetainedLicenses` (`DetainedLicenseQueryRequest?`, dual-mode) -> paged or `List<DetainedLicenseResponse>`
  - `GET /api/DetainedLicenses/{id:int}`, `/license/{licenseId:int}/active` -> `DetainedLicenseResponse`
  - `GET /api/DetainedLicenses/license/{licenseId:int}/detained` -> `{ detained: bool }`
  - `POST /api/DetainedLicenses` (`CreateDetainedLicenseRequest`) -> `200 OK` `DetainedLicenseResponse`
  - `POST /api/DetainedLicenses/release` (`ReleaseDetainedLicenseRequest`) -> `204 NoContent`
- **`InternationalLicensesController` (`/api/InternationalLicenses`)**:
  - `GET /api/InternationalLicenses` (`InternationalLicenseQueryRequest?`, dual-mode) -> paged or `List<InternationalLicenseListResponse>`
  - `GET /api/InternationalLicenses/{internationalLicenseId:int}`, `/application/{applicationId:int}` -> `InternationalLicenseResponse`
  - `GET /api/InternationalLicenses/driver/{driverId:int}`, `/license/{localLicenseId:int}` -> `List<InternationalLicenseListResponse>`
  - `GET /api/InternationalLicenses/license/{licenseId:int}/info` -> `DriverLicenseInfoResponse`
  - `POST /api/InternationalLicenses` (`IssueInternationalLicenseRequest`) -> `200 OK` `InternationalLicenseResponse`

---

## 8. Reference Data & Dashboard

- **`DashboardController`**: `GET /api/Dashboard/statistics` (`[Authorize]`) -> `DashboardResponse`
- **`CountriesController`**: `GET /api/Countries` (`[Authorize]`) -> `List<CountryResponse>`
- **`ApplicationTypesController`**: `GET /api/ApplicationTypes`, `GET /api/ApplicationTypes/{id:int}` (`[Authorize]`); `PUT /api/ApplicationTypes/{id:int}` (`[Authorize(Policy = "AdminOnly")]`)
- **`LicenseClassesController`**: `GET /api/LicenseClasses`, `GET /api/LicenseClasses/{id:int}` (`[Authorize]`); `PUT /api/LicenseClasses/{id:int}` (`[Authorize(Policy = "AdminOnly")]`)
- **`TestTypesController`**: `GET /api/TestTypes`, `GET /api/TestTypes/{id:int}` (`[Authorize]`); `PUT /api/TestTypes/{id:int}` (`[Authorize(Policy = "AdminOnly")]`)

---

## Implementation References

- Controllers: [`API/DVLD.Api/Controllers/`](../../API/DVLD.Api/Controllers/)
- Error & result mapping: [`API/DVLD.Api/Results/ResultHttpMapper.cs`](../../API/DVLD.Api/Results/ResultHttpMapper.cs), [`API/DVLD.Api/Results/ApiProblemDetails.cs`](../../API/DVLD.Api/Results/ApiProblemDetails.cs)
- Shared contracts: [`DVLD.Contracts/`](../../DVLD.Contracts/)
