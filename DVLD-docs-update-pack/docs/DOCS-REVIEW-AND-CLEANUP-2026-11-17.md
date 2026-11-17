# DVLD — Docs Folder Review & Cleanup Decisions

**Review date:** 2026-11-17  
**Reviewed commit:** `e4435e3006d88d99ed8ccac2f8fec741d5446afa` (`main`)  
**Scope:** All 35 Markdown files found recursively under `docs/`.  
**Method:** GitHub source/document inspection. No local working tree, tests or database were accessed.

## Executive result

The folder has too many documents that look authoritative, but most of them are useful as dated evidence. The correct cleanup is to make the authority hierarchy explicit, update a small set of active documents, and archive stale plans/audits. Do not mass-delete the audit folder.

- **Keep:** active execution evidence, continuity rules, domain specification, SQL test guide, and focused task audits.
- **Update:** charter, domain spec, SQL guide, correction log, template path and dated current snapshot.
- **Archive/mark historical:** old project completion master plan and frontend redesign audit/matrix.
- **Do not delete now:** audit reports and archive history.
- **Immediate hard-delete candidates:** none. Two frontend design documents can be removed only if the old visual direction and audit history are intentionally discarded; archiving them is safer.

## File-by-file inventory

| # | File | Decision | Reason / action |
|---:|---|---|---|
| 1 | `docs/IMPLEMENTATION-EXECUTION-LOG.md` | KEEP / append | Execution ledger. Preserve all old sessions; append dated evidence only. Update its root-path metadata when next touched. |
| 2 | `docs/PROJECT-COMPLETION-MASTER-PLAN.md` | ARCHIVE / mark superseded | Large, duplicates root governance and contains stale test/auth/phase statuses. Still referenced from root `AGENTS.md`, `AI-ENGINEERING-INSTRUCTIONS.md`, `ENGINEERING-ROADMAP.md`, `ENGINEERING-DECISIONS.md`, standards and execution log. Do not delete until those links are revised. |
| 3 | `docs/archive/README.md` | KEEP | Correctly defines archive as non-authoritative. |
| 4 | `docs/archive/historical-planning/DEVELOPMENT-PLAN.md` | KEEP ARCHIVED | Historical source for extracted domain rules; not active roadmap. |
| 5 | `docs/archive/historical-planning/DEVELOPMENT-VISION.md` | KEEP ARCHIVED | Historical product vision; no active technical authority. |
| 6 | `docs/archive/historical-planning/RISKS-AND-OPEN-DECISIONS.md` | KEEP ARCHIVED | Early decision register superseded by root `ENGINEERING-DECISIONS.md`. |
| 7 | `docs/archive/historical-planning/TARGET-ARCHITECTURE.md` | KEEP ARCHIVED | Early architecture draft superseded by root `ENGINEERING-STANDARDS.md`. |
| 8 | `docs/archive/security-sources/SECURITY-IMPLEMENTATION-READINESS.md` | KEEP ARCHIVED | Original assessment and source evidence; its findings are baseline-relative. |
| 9 | `docs/archive/security-sources/SECURITY-MASTER-PLAN.md` | KEEP ARCHIVED | Large historical proposal; contains old SEC-05 deferred state and must not be used as current security plan. |
| 10 | `docs/audit-report.md` | ARCHIVE | Frontend audit dated 2026-11-09. It records an older UI iteration and says no testing framework existed, which is now false: Vitest and Playwright configs/tests are in the repository. |
| 11 | `docs/audits/AUTH-01-REFRESH-SESSION-DIAGNOSTIC.md` | KEEP HISTORICAL | Root-cause report for refresh-cookie path/casing defect. Contains WPF context that is now legacy history. |
| 12 | `docs/audits/N1-COMPREHENSIVE-AUDIT-AND-REMEDIATION.md` | KEEP / OPEN | Static audit says no-show N1 profiling was blocked; runtime SQL command-count proof remains outstanding. |
| 13 | `docs/audits/NO-SHOW-SQL-PROFILING-AND-OPTIMIZATION.md` | KEEP / OPEN | Distinct no-show profiling design and concurrency-safety analysis; not redundant with N1 audit. |
| 14 | `docs/audits/POST-COMMIT-SECURITY-TEST-INFRASTRUCTURE-REVIEW-5ce6621.md` | KEEP HISTORICAL / RE-REVIEW | Static review against a specific commit; assess its disposal/cleanup findings against current `SqlServerApiWebApplicationFactory` before treating them as active. |
| 15 | `docs/audits/SQLSERVER-TEST-ENVIRONMENT-PREPARATION.md` | ARCHIVE OPTIONAL | Long machine-specific preparation timeline. Useful historically, but day-to-day commands and safeguards belong in `docs/testing/sql-server-integration-tests.md`. |
| 16 | `docs/audits/api-01-cancellation-token-inventory.md` | KEEP | Task-specific implementation audit and verified inventory. |
| 17 | `docs/audits/auth-01-cross-process-verification.md` | KEEP | Unique evidence for two-process refresh token rotation/reuse; do not discard. |
| 18 | `docs/audits/auth-01-post-sql-verification.md` | KEEP + ADDENDUM | SQL persistence/rotation/reuse proof. Its statement that cross-process was unverified is superseded by file 17; preserve original scope and append a cross-reference. |
| 19 | `docs/audits/auth-01-session-management.md` | KEEP HISTORICAL | AUTH-01 design/implementation narrative; later SQL and cross-process reports supplement it. |
| 20 | `docs/audits/ci-sql-01-azure-pipelines-sql-reliability.md` | KEEP + ADDENDUM | Original failure report says Azure runtime verification pending; the later implementation log records run `#20261111.2` as successful. Append that later evidence. |
| 21 | `docs/audits/concurrency-01-business-workflow-concurrency.md` | KEEP | Distinct SQL-backed evidence for last-administrator concurrency and scoped DbContext/UnitOfWork. |
| 22 | `docs/audits/phase-a-repository-discovery.md` | KEEP HISTORICAL | Baseline discovery at `29e43d6`; useful comparison point, not current truth. |
| 23 | `docs/audits/phase-a-security-architecture-audit.md` | KEEP HISTORICAL | Original 13-finding security audit; never represent as present-day scan. |
| 24 | `docs/audits/phase-a-supplementary-review.md` | KEEP HISTORICAL | Deep domain/workflow audit at the same old baseline. |
| 25 | `docs/audits/rest-01-error-handling-and-test-database-safety.md` | KEEP | Evidence for REST-01/ProblemDetails and test-host database safety. |
| 26 | `docs/continuity/CORRECTION-LOG.md` | KEEP / append | Required no-silent-rewrite correction ledger. It currently has no correction entries; append the documented status corrections. |
| 27 | `docs/continuity/PROJECT-CHARTER-AND-CONTINUITY.md` | UPDATE | Section 8.3 still calls refresh sessions design-only although AUTH-01 is implemented. Sections 8.4/8.5 should distinguish actual ILogger usage from planned Serilog/audit work and defer task status to the root roadmap. Also change `D:\DVLD-WPF` to `D:\DVLD-System`. |
| 28 | `docs/continuity/SESSION-LOGS/README.md` | KEEP IF USED | Process guide for significant session handoffs. Do not create logs for ordinary learning/chat messages. |
| 29 | `docs/continuity/snapshots/CURRENT-PROJECT-STATE-2026-11-08.md` | KEEP HISTORICAL | Initial snapshot; not current. Supersede it with the included 2026-11-17 snapshot, do not overwrite it. |
| 30 | `docs/continuity/templates/CORRECTION-LOG.template.md` | KEEP | Useful template for correction entries. |
| 31 | `docs/continuity/templates/CURRENT-PROJECT-STATE.template.md` | KEEP / UPDATE PATH | Update placeholder root path to `D:\DVLD-System`; retain template for future snapshots. |
| 32 | `docs/continuity/templates/SESSION-LOG.template.md` | KEEP | Useful template for real engineering sessions. |
| 33 | `docs/contrast-matrix.md` | ARCHIVE OR DELETE IF PALETTE ABANDONED | It calculates cream/lime token ratios, but current `ClientApp/src/index.css` uses dark/navy surfaces and Royal Sapphire Blue `#3b82f6`. It is not current WCAG evidence. Audit-report references it, so move both together or retain both. |
| 34 | `docs/domain/LICENSING-DOMAIN-SPECIFICATION.md` | UPDATE | It states detained licenses cannot be replaced. Current renewal service blocks active detention; replacement service does not. Domestic detention/replacement remains pending DEC-POL-01 / ADR-O02 external confirmation. |
| 35 | `docs/testing/sql-server-integration-tests.md` | KEEP / UPDATE | Current setup guide is useful. It documents .NET 8, which is still the current target in manifests but is out of support since 2026-11-10; add a clear migration warning and preserve the DB isolation rules. |

## Important code/document discrepancies found

### 1. Authentication: implemented, not deferred

Current source has `UserSession`, hashed refresh tokens, rotation, revocation, HttpOnly cookie, CSRF header, session-claim validation and React refresh coordination. `SECURITY-STANDARDS.md` §12 documents the current model. The charter's §8.3 and older archive plans still describe it as deferred. Correct the live charter; keep the old audit narrative unchanged.

### 2. Logout still deserves a small reliability task

In `ClientApp/src/services/apiClient.ts`, `logoutSession()` invalidates local state and attempts the API request but does not inspect the response status; failures are swallowed and local access-token state is cleared in `finally`. This can leave a server session/cookie active when the server request fails. Do not conflate this narrow gap with missing refresh-token support.

### 3. Logging is not a single completed feature

`ILogger<T>` already exists, but the repository roadmap separately tracks centralized structured application logging/correlation (`OBS-03`), security event telemetry (`SEC-04`) and transactional business audit (`AUD-01`). Serilog/OpenTelemetry/audit persistence must not be described as operational without evidence.

### 4. Frontend tests exist, but CI does not run them yet

There are seven Vitest unit test files and one Playwright E2E file. The checked-in Azure YAML statically includes lint, type checking and build; no `npm run test` / `npm run test:e2e` step was found. Do not retain the old claim of zero frontend tests; do not claim CI test coverage is already active.

### 5. Current frontend theme differs from the contrast matrix

The active tokens in `ClientApp/src/index.css` include `--accent: #3b82f6`, dark navy page/surface colors and the Royal Sapphire Blue brand. The cream/lime contrast matrix should be archived or recalculated from current token combinations before it is used as an accessibility sign-off.

### 6. Detained replacement is an unresolved business-policy decision

`LicenseRenewalService` checks `IsLicenseDetainedAsync`; `LicenseReplacementService` does not. The decision register requires external confirmation for replacement policy. The domain specification must not assert the stricter rule as approved policy or implemented behavior.

### 7. .NET 8 is out of support

The current project targets `net8.0`, and `global.json` pins SDK `8.0.427`. Microsoft ended .NET 8 support on 2026-11-10. .NET 10 is currently LTS and supported through 2028-11-14. This is an urgent upgrade planning issue; the review did not upgrade code or execute compatibility tests.

## Exact deletion guidance

**No file is safe for immediate hard deletion based on this review alone.** Recommended sequence:

1. Add the new docs index and current snapshot.
2. Correct the active charter/domain/SQL guide and append correction addenda.
3. Mark `docs/PROJECT-COMPLETION-MASTER-PLAN.md` as superseded and update all root references before moving/deleting it.
4. If the cream/lime redesign is permanently abandoned, move `docs/audit-report.md` and `docs/contrast-matrix.md` together to `docs/archive/frontend/`. Delete only if you explicitly accept losing the historical visual audit and no active reference remains.
5. Optionally move `docs/audits/SQLSERVER-TEST-ENVIRONMENT-PREPARATION.md` to `docs/archive/operations/`; it is historical operational investigation, not the primary current guide.
6. Do not delete `docs/audits/`, `docs/archive/`, continuity records, or the old snapshot as a general cleanup operation.

## Verification boundary

The source and all 35 Markdown paths were inspected at the listed commit. No code or repository files were changed during the review. The user's local working-tree changes were not visible. No tests, builds, database connections or SQL operations were performed.
