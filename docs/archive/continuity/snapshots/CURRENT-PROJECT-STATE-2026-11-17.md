# DVLD — Current Project State Snapshot

> **Snapshot ID:** `SNAPSHOT-2026-11-17-001`  
> **Supersedes:** `CURRENT-PROJECT-STATE-2026-11-08.md`  
> **Reviewed at:** `2026-11-17`  
> **Repository:** `ayhm325/DVLD-System`  
> **Branch / commit inspected:** `main` / `e4435e3006d88d99ed8ccac2f8fec741d5446afa`  
> **Review mode:** Read-only static inspection of current GitHub source and documentation  
> **Local Git status:** Not inspected  
> **Build/tests in this review:** Not executed  
> **Database connection:** Not attempted; `DVLDf` was not accessed

## 1. Executive summary

DVLD is currently structured as a layered .NET Web API and React SPA backed by SQL Server. The repository already contains mature business workflows, a scoped DbContext/UnitOfWork approach, SQL Server concurrency controls, persisted refresh-token sessions, and a broad test suite. The near-term engineering focus should be risk-driven modernization and operational reliability, not a rewrite or microservices conversion.

The most urgent platform issue is that the repository still targets .NET 8 / EF Core 8, and Microsoft ended .NET 8 support on 2026-11-10. The next runtime baseline should be a supported LTS release, with .NET 10 as the current LTS candidate, following a compatibility and regression plan.

## 2. Verified architecture and technology baseline

### Backend

- `global.json` pins SDK `8.0.427` with `rollForward: latestFeature`.
- API, Application and Infrastructure projects target `net8.0`.
- API uses ASP.NET Core Web API, JWT Bearer authentication, Swagger/OpenAPI, RFC 7807 ProblemDetails and rate limiting.
- Infrastructure uses EF Core and the SQL Server provider.
- The current architecture is a modular layered monolith: Domain, Application, Infrastructure and API, with `DVLD.Contracts` as the shared HTTP contract project.
- `Infrastructure/UnitOfWork.cs` contains shared-context transaction handling, execution-strategy integration, rollback and change-tracker cleanup logic. Preserve it unless a verified defect requires a narrowly scoped change.
- Repository and transaction workflows use SQL Server-specific mechanisms, including Serializable transactions, `UPDLOCK`/`HOLDLOCK`, and `sp_getapplock` where applicable.

### Package version discrepancy

`Infrastructure.csproj` declares `Microsoft.EntityFrameworkCore` `8.0.18` while `Microsoft.EntityFrameworkCore.SqlServer`, Design and Tools are declared as `8.0.0`. This is a verified manifest inconsistency. Aligning versions should be included in the runtime upgrade, validated through restore/build/migrations/tests, and not applied as an untested mass replacement.

### Frontend

- React `^19.3.1`, TypeScript `~6.0.3`, Vite `^8.3.4`, Tailwind CSS `^4.3.3`.
- The frontend is under `ClientApp/` and communicates with `DVLD.Api` over HTTP/JSON.
- Shared UI primitives and feature-level components are present.
- `vitest.config.ts` is configured; seven unit test files are present under `ClientApp/tests/unit/`.
- Playwright is configured; one E2E test file is present under `ClientApp/tests/e2e/`.
- The current `ClientApp/tsconfig.app.json` does not explicitly set `strict: true`, despite an older technology inventory claiming Strict Mode is enabled. Resolve this documentation/configuration discrepancy deliberately.

## 3. Authentication, login and session state

The current code implements AUTH-01 session modernization; do not schedule a second implementation of refresh sessions as though they were absent.

Verified from source:

- 15-minute JWT access token configuration and validation of issuer, audience, signature and lifetime.
- `sst` security-stamp and `sid` session claims are checked during `OnTokenValidated` through `IAuthService.IsPrincipalCurrentAsync`.
- `UserSession` rows store token hashes rather than raw refresh tokens and track token families, expiry, revocation and replacement links.
- Refresh uses a conditional consume operation and SQL Server locking/transaction coordination; concurrent callers are handled through the bounded 409/retry behavior.
- Refresh token cookie is HttpOnly, SameSite=Lax and Secure when the request is HTTPS; refresh/logout endpoints require `X-CSRF-Protection: 1`.
- Login, refresh and change-password rate-limit policies are configured in `Program.cs`.
- React API client implements single-flight refresh coordination and cross-tab/session generation handling.

Repository audit evidence reports SQL persistence/rotation/reuse/revocation tests and a two-process refresh test. The separate cross-process report records one winner (200) and one conflict (409), with family revocation after reuse. Lost HTTP-response and process-termination fault injection remain unverified.

### Current logout reliability gap

`ClientApp/src/services/apiClient.ts` invalidates local session state, attempts the server logout request, swallows request failures and clears the access token in `finally`. Therefore the browser can appear locally signed out even when server-side revocation was not confirmed. The server clears the HttpOnly cookie only when the logout endpoint completes successfully. Treat this as a follow-up reliability issue, not evidence that the entire session architecture is missing.

## 4. Logging and observability state

Keep these concerns separate:

1. **Diagnostic application logging:** the code uses `ILogger<T>` in selected services and infrastructure. A complete centralized Serilog/structured-JSON/correlation-ID platform is not confirmed as implemented; `OBS-03` remains a planned roadmap item.
2. **Security event telemetry:** selected security-related diagnostic messages exist, but a dedicated structured security audit event service and isolated sink (`SEC-04`) are not confirmed as implemented end-to-end.
3. **Transactional business audit trail:** durable SQL audit records atomically committed with key business operations (`AUD-01`) remain a separate schema/design/DBA approval task.

Never log passwords, password hashes, JWT values, refresh tokens, cookie values, signing keys or full connection strings. OpenTelemetry should be introduced only as an approved observability design with defined exporters, privacy filters and operational ownership.

## 5. Security and deployment considerations

- Authorization policies include AdminOnly, StaffOnly and StaffOrAdmin; backend authorization remains the security boundary.
- Login/refresh/change-password rate limits use `RemoteIpAddress` partition keys. Validate forwarded-header trust and actual proxy topology before production deployment; do not trust arbitrary client-supplied forwarded headers.
- CORS currently explicitly allows `http://localhost:5173`. Production origins must be configured from the actual deployment topology; the local origin is not a production allow-list.
- `UseForwardedHeaders` was not found in the inspected Program.cs. This is a deployment-readiness item if TLS terminates at a reverse proxy, not proof of an exploitable production configuration (the hosting topology was not supplied).
- File uploads use image-processing validation and physical storage outside `wwwroot`; the default storage path is local application data. Multi-instance deployments need a deliberate shared-storage decision.
- Background services are hosted in the API process. Multiple replicas require review of idempotency, duplicate polling and coordination before scaling out.

## 6. Domain policy boundary: detained licenses

The current `LicenseRenewalService` explicitly blocks renewal when the license is actively detained. The current `LicenseReplacementService` validates existence, expiry and active status but does not check detention. The question of whether domestic lost/damaged replacement is prohibited while detained remains governed by `DEC-POL-01` / `ADR-O02` and requires external licensing-authority confirmation. Do not present the proposed rule as confirmed Jordanian law or as an already implemented system check.

## 7. Testing and CI baseline

### Test projects

- `Test/Application.UnitTests`
- `Test/API.IntegrationTests`
- `Test/Infrastructure.IntegrationTests`
- `ClientApp/tests/unit` — seven files
- `ClientApp/tests/e2e` — one Playwright file

File counts are not test-case counts and are not execution results.

### CI configuration observed

- Frontend stage installs Node 22, installs dependencies with `npm ci`, runs ESLint, TypeScript validation and production build.
- Backend stage restores/builds the solution and includes the three backend test projects using isolated SQL test environment variables.
- No `npm run test` or `npm run test:e2e` step was found in the checked-in pipeline YAML. Add frontend tests to CI only after confirming reliability and runtime prerequisites.
- The implementation log records Azure pipeline run `#20261111.2` as successful for the SQL isolation/pipeline repair. This review did not query Azure DevOps or independently validate that run.

### Test database safety

Integration tests are designed to use generated `DVLD_IntegrationTests_<GUID>` databases, expected server identity checks and ownership-verified cleanup. Never point automated tests at `DVLD` or `DVLDf`. This review made no SQL connection and performed no database operation.

## 8. Performance and scalability follow-ups

- `TestAppointmentRepository.GetAllAsync` materializes an unpaged list. Review actual callers and endpoint contract before deciding whether pagination is required; do not impose pagination on small static lookup tables without a data-growth reason.
- The no-show SQL audit identifies per-candidate database operations, but runtime query-count/latency profiling remains outstanding. Do not replace per-record transactional locking with bulk updates until equivalent concurrency and idempotency guarantees are proven.
- `LicenseQueryService` N+1 remediation is recorded as implemented in the execution log; the separate no-show finding is not thereby closed.
- Rate limiting is in-process; horizontal scaling may multiply effective quotas across instances. Distributed limiting is a future decision only if the deployment topology requires it.
- Physical file storage and hosted background services are single-host assumptions until deployment requirements demonstrate otherwise. Do not add Redis, Kafka, RabbitMQ, Kubernetes or microservices without a measurable need.

## 9. Priority decisions

| Priority | Workstream | Current disposition |
|---|---|---|
| P0 | Move from unsupported .NET 8 / EF Core 8 baseline to supported LTS | Urgent planned upgrade; no code change in this review |
| P0 | Reconcile EF Core package versions | Include in upgrade plan and test as a set |
| P1 | Make server-side logout/revocation failures observable in the React client | Follow-up reliability work; preserve current server contract |
| P1 | Verify production CORS, forwarded headers, HTTPS cookie behavior and secret isolation | Requires actual deployment topology |
| P1 | Reconcile stale documentation about AUTH-01, frontend tests, Azure SQL CI and detention policy | Documentation correction |
| P2 | Structured logging/correlation IDs (OBS-03) | Planned; package/configuration approval per roadmap |
| P2 | Security audit telemetry (SEC-04) | Planned; decision on sink and redaction needed |
| P2 | Transactional business audit (AUD-01) | Planned; DBA/schema approval needed |
| P2 | No-show SQL profiling | Measure before optimizing |
| P2 | Pagination for genuinely growing collections | Review API clients/contracts and protect small lookups |
| P3 | Worker/shared file storage/distributed rate limiting | Only when multi-instance deployment is approved |

## 10. Sources and integrity

- [Microsoft .NET Support Policy](https://dotnet.microsoft.com/en-us/platform/support/policy) — .NET 8 ended support on 2026-11-10; .NET 10 is the current LTS line.
- [Microsoft announcement: .NET 8 and 9 end of support](https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/).
- [EF Core efficient querying](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying).
- [OWASP ASVS](https://owasp.org/www-project-application-security-verification-standard/).
- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/dotnet/).

This snapshot is a static review of source and repository documents at the stated commit. It is not a test report, penetration test, DBA certification, production deployment assessment, or legal interpretation. Refresh it after substantive source/architecture changes.
