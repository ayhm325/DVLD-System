# DVLD Documentation Index

> **Reviewed:** 2026-11-17  
> **Repository baseline inspected:** `main` — `e4435e3006d88d99ed8ccac2f8fec741d5446afa`  
> **Purpose:** Navigation and status index for the documentation under `docs/`.  
> **Important:** This index does not replace the canonical engineering governance documents located in the repository root.

## 1. Which document is authoritative?

Use the following hierarchy. Do not treat an old audit report, archived proposal, or snapshot as permission to change the application.

| Need | Authoritative source |
|---|---|
| What engineering task may be implemented and its priority/status | [`../ENGINEERING-ROADMAP.md`](../ENGINEERING-ROADMAP.md) |
| Accepted, rejected, pending, or policy-dependent decisions | [`../ENGINEERING-DECISIONS.md`](../ENGINEERING-DECISIONS.md) |
| Layer boundaries, Unit of Work, transactions, SQL locking, engineering invariants | [`../ENGINEERING-STANDARDS.md`](../ENGINEERING-STANDARDS.md) |
| Authentication, authorization, secrets, security and session standards | [`../SECURITY-STANDARDS.md`](../SECURITY-STANDARDS.md) |
| Test isolation, database safety, and test execution requirements | [`../TESTING-STANDARDS.md`](../TESTING-STANDARDS.md) |
| Verified package/runtime inventory | [`../TECHNOLOGY-STACK.md`](../TECHNOLOGY-STACK.md) (reconcile with current manifests when it differs) |
| AI agent scope, safety gates, Git/database permissions | [`../AGENTS.md`](../AGENTS.md) and [`../AI-ENGINEERING-INSTRUCTIONS.md`](../AI-ENGINEERING-INSTRUCTIONS.md) |
| Chronological implementation and verification evidence | [`IMPLEMENTATION-EXECUTION-LOG.md`](./IMPLEMENTATION-EXECUTION-LOG.md) |
| Current point-in-time project state | [`continuity/snapshots/CURRENT-PROJECT-STATE-2026-11-17.md`](./continuity/snapshots/CURRENT-PROJECT-STATE-2026-11-17.md) |
| Licensing domain rules and unresolved policy limits | [`domain/LICENSING-DOMAIN-SPECIFICATION.md`](./domain/LICENSING-DOMAIN-SPECIFICATION.md), interpreted together with the decision register |
| SQL integration test setup | [`testing/sql-server-integration-tests.md`](./testing/sql-server-integration-tests.md) |

**Conflict rule:** root governance files own the active task and decision status. Detailed audit reports supply evidence for a stated point in time. If they conflict, do not choose the most recently edited file automatically; reconcile the conflict against current source and the formal decision register.

## 2. Current documentation areas

### 2.1 Active reference documents

| File | Role | Disposition |
|---|---|---|
| [`IMPLEMENTATION-EXECUTION-LOG.md`](./IMPLEMENTATION-EXECUTION-LOG.md) | Append-only record of implementation, test evidence, blockers and handoffs | **Keep.** Add new evidence as dated entries; do not rewrite past sessions. |
| [`continuity/PROJECT-CHARTER-AND-CONTINUITY.md`](./continuity/PROJECT-CHARTER-AND-CONTINUITY.md) | Continuity rules and evidence/authorization conventions | **Keep and update.** The authentication, logging and phase-status sections must agree with current implementation and root governance. |
| [`continuity/CORRECTION-LOG.md`](./continuity/CORRECTION-LOG.md) | Record factual corrections to prior documents | **Keep.** Append verified corrections; do not silently rewrite historical reports. |
| [`continuity/snapshots/CURRENT-PROJECT-STATE-2026-11-17.md`](./continuity/snapshots/CURRENT-PROJECT-STATE-2026-11-17.md) | Current snapshot at the reviewed commit | **Keep as current snapshot** until superseded by a later dated snapshot. |
| [`continuity/snapshots/CURRENT-PROJECT-STATE-2026-11-08.md`](./continuity/snapshots/CURRENT-PROJECT-STATE-2026-11-08.md) | Initial historical project snapshot | **Keep as history.** Do not use as the current state. |
| [`domain/LICENSING-DOMAIN-SPECIFICATION.md`](./domain/LICENSING-DOMAIN-SPECIFICATION.md) | Civic licensing business invariants | **Keep and correct.** Detention-vs-replacement policy is not a settled implemented rule. |
| [`testing/sql-server-integration-tests.md`](./testing/sql-server-integration-tests.md) | Practical SQL Server integration test guide | **Keep and update.** Keep it aligned with the fixture code and current .NET support status. |

### 2.2 Focused audit evidence

These documents have distinct evidentiary value. They are not duplicate implementation plans, and should not be deleted merely because their work is complete.

| File | What it preserves | Disposition |
|---|---|---|
| [`audits/phase-a-repository-discovery.md`](./audits/phase-a-repository-discovery.md) | Initial source-level repository discovery at commit `29e43d6` | **Keep as historical baseline.** Findings are not current status. |
| [`audits/phase-a-supplementary-review.md`](./audits/phase-a-supplementary-review.md) | Deep workflow, transaction, and concurrency analysis at the same baseline | **Keep as historical baseline.** |
| [`audits/phase-a-security-architecture-audit.md`](./audits/phase-a-security-architecture-audit.md) | Original security risk inventory before remediation | **Keep as historical baseline.** |
| [`audits/AUTH-01-REFRESH-SESSION-DIAGNOSTIC.md`](./audits/AUTH-01-REFRESH-SESSION-DIAGNOSTIC.md) | Root-cause investigation of the refresh-cookie path/casing issue | **Keep as diagnostic history.** WPF references describe the old diagnostic context, not an active client. |
| [`audits/auth-01-session-management.md`](./audits/auth-01-session-management.md) | Initial AUTH-01 design and implementation phases | **Keep as implementation history.** |
| [`audits/auth-01-post-sql-verification.md`](./audits/auth-01-post-sql-verification.md) | SQL persistence, token rotation, reuse and rollback test evidence | **Keep.** Its original “cross-process not verified” result is superseded by the separate cross-process report. |
| [`audits/auth-01-cross-process-verification.md`](./audits/auth-01-cross-process-verification.md) | Two-process refresh rotation/reuse verification | **Keep.** Lost-response/process-termination scenarios remain unverified. |
| [`audits/ci-sql-01-azure-pipelines-sql-reliability.md`](./audits/ci-sql-01-azure-pipelines-sql-reliability.md) | Original Azure SQL test provisioning failure and repair | **Keep with dated status addendum.** The execution log later records Azure run `#20261111.2` as successful. |
| [`audits/concurrency-01-business-workflow-concurrency.md`](./audits/concurrency-01-business-workflow-concurrency.md) | UnitOfWork/DbContext and last-administrator concurrency verification | **Keep.** |
| [`audits/api-01-cancellation-token-inventory.md`](./audits/api-01-cancellation-token-inventory.md) | End-to-end CancellationToken inventory and verification | **Keep.** |
| [`audits/rest-01-error-handling-and-test-database-safety.md`](./audits/rest-01-error-handling-and-test-database-safety.md) | ProblemDetails contract and isolated test-host safety work | **Keep.** |
| [`audits/N1-COMPREHENSIVE-AUDIT-AND-REMEDIATION.md`](./audits/N1-COMPREHENSIVE-AUDIT-AND-REMEDIATION.md) | Static N+1/performance findings, including the still-open no-show profiling question | **Keep as open performance evidence.** Do not claim runtime closure. |
| [`audits/NO-SHOW-SQL-PROFILING-AND-OPTIMIZATION.md`](./audits/NO-SHOW-SQL-PROFILING-AND-OPTIMIZATION.md) | Proposed SQL profiling and safe no-show optimization boundary | **Keep until actual SQL profiling and regression proof exist.** |
| [`audits/POST-COMMIT-SECURITY-TEST-INFRASTRUCTURE-REVIEW-5ce6621.md`](./audits/POST-COMMIT-SECURITY-TEST-INFRASTRUCTURE-REVIEW-5ce6621.md) | Static review of test-factory disposal and cleanup behavior at a specific commit | **Keep as historical review.** Re-review against current factory code before treating findings as open defects. |
| [`audits/SQLSERVER-TEST-ENVIRONMENT-PREPARATION.md`](./audits/SQLSERVER-TEST-ENVIRONMENT-PREPARATION.md) | Timeline of local SQL/test-environment blockers and corrective work | **Keep as historical troubleshooting evidence** or move to archive; current instructions belong in the testing guide. |

### 2.3 Historical archive

Everything below `archive/` is historical reference, not active authorization. Keep it if traceability matters; do not copy old proposals into the active backlog without reconciling them with root governance.

| File | Disposition |
|---|---|
| [`archive/README.md`](./archive/README.md) | **Keep.** Explains archive status and active authorities. |
| [`archive/historical-planning/DEVELOPMENT-PLAN.md`](./archive/historical-planning/DEVELOPMENT-PLAN.md) | **Keep as superseded historical planning.** Domain rules were extracted into the active domain specification. |
| [`archive/historical-planning/DEVELOPMENT-VISION.md`](./archive/historical-planning/DEVELOPMENT-VISION.md) | **Keep as historical vision.** |
| [`archive/historical-planning/RISKS-AND-OPEN-DECISIONS.md`](./archive/historical-planning/RISKS-AND-OPEN-DECISIONS.md) | **Keep as historical only.** Current decisions live in root `ENGINEERING-DECISIONS.md`. |
| [`archive/historical-planning/TARGET-ARCHITECTURE.md`](./archive/historical-planning/TARGET-ARCHITECTURE.md) | **Keep as historical architecture draft.** Current architecture authority is root `ENGINEERING-STANDARDS.md`. |
| [`archive/security-sources/SECURITY-IMPLEMENTATION-READINESS.md`](./archive/security-sources/SECURITY-IMPLEMENTATION-READINESS.md) | **Keep as original security assessment.** Risk descriptions are baseline-relative. |
| [`archive/security-sources/SECURITY-MASTER-PLAN.md`](./archive/security-sources/SECURITY-MASTER-PLAN.md) | **Keep as superseded proposal.** It is large and contains obsolete task states; never use it as the current security roadmap. |

### 2.4 Frontend design history

| File | Observed mismatch | Disposition |
|---|---|---|
| [`audit-report.md`](./audit-report.md) | 2026-11-09 UI audit; contains historical statements such as no test framework and an older visual design review. The current repository has Vitest config, seven frontend unit-test files and one Playwright E2E file. | **Archive or retain as historical audit.** Do not use its old test inventory as current. |
| [`contrast-matrix.md`](./contrast-matrix.md) | Matrix describes cream/lime light and dark token pairs; current `ClientApp/src/index.css` uses a dark/navy surface and Royal Sapphire Blue accent (`#3b82f6`). | **Not a current accessibility sign-off.** Archive it or delete only if the old palette is permanently abandoned; create a new measured matrix from current tokens before claiming WCAG conformance. |

### 2.5 Templates

| File | Disposition |
|---|---|
| [`continuity/SESSION-LOGS/README.md`](./continuity/SESSION-LOGS/README.md) | **Keep if the project continues to require significant-session handoff records.** Do not create logs for trivial learning/chat sessions. |
| [`continuity/templates/CORRECTION-LOG.template.md`](./continuity/templates/CORRECTION-LOG.template.md) | **Keep.** |
| [`continuity/templates/CURRENT-PROJECT-STATE.template.md`](./continuity/templates/CURRENT-PROJECT-STATE.template.md) | **Keep and update repository-root placeholder to `D:\DVLD-System`.** |
| [`continuity/templates/SESSION-LOG.template.md`](./continuity/templates/SESSION-LOG.template.md) | **Keep.** |

## 3. Verified current findings that affect documentation

1. **Authentication is implemented.** The current source contains persisted `UserSessions`, hashed refresh-token storage, rotation/reuse handling, HttpOnly cookie transport, CSRF header checks, and React single-flight refresh. Do not describe refresh sessions as future/unauthorized work. The current client logout helper still treats server logout as best-effort and clears local state even on request failure; this remains a reliability follow-up.
2. **Centralized Logging is not fully implemented.** `ILogger<T>` is used, but the roadmap's Serilog/correlation-ID task (`OBS-03`), dedicated security event telemetry (`SEC-04`), and transactional business audit trail (`AUD-01`) are separate incomplete workstreams. Do not say Serilog/OpenTelemetry is already operational.
3. **Frontend tests exist locally.** Seven files are under `ClientApp/tests/unit/`; one E2E file is under `ClientApp/tests/e2e/`. `vitest.config.ts` and `playwright.config.ts` exist. The current Azure pipeline statically shows lint, TypeScript validation and frontend build, but no `npm run test` or `npm run test:e2e` step was found.
4. **The current frontend theme does not match the saved contrast matrix.** `ClientApp/src/index.css` declares Royal Sapphire Blue (`#3b82f6`) and dark/navy surfaces. The cream/lime ratios must not be represented as current WCAG evidence.
5. **The detention/replacement policy is unresolved.** The current renewal service checks active detention. The current replacement service does not. `DEC-POL-01` / `ADR-O02` requires external licensing-authority confirmation; do not claim that replacement of a detained license is legally or technically blocked today.
6. **The runtime is unsupported.** The repository targets `net8.0`, pins SDK `8.0.427`, and uses EF Core 8. Microsoft ended .NET 8 support on 2026-11-10. Upgrade planning should target a supported LTS line (currently .NET 10 LTS) only after package, database-provider, migration, runtime and CI compatibility checks.
7. **EF Core versions are inconsistent in project manifests.** `Infrastructure.csproj` declares EF Core `8.0.18` while SQL Server, Design and Tools packages are `8.0.0`. Align package versions deliberately as part of the runtime upgrade, not by a blind text replacement.
8. **No tests were run during this documentation review.** All test figures and Azure run claims in the snapshot are explicitly attributed to prior committed execution records.

## 4. Recommended cleanup decision

### Do not delete now

- Anything under `docs/audits/`: these are distinct point-in-time evidence reports.
- Anything under `docs/archive/`: they preserve decision and architecture history.
- `IMPLEMENTATION-EXECUTION-LOG.md`, `CORRECTION-LOG.md`, the active charter, the domain specification, or the SQL testing guide.
- The 2026-11-08 snapshot; it is historical, not a duplicate of the current snapshot.

### Candidates to archive (not hard-delete)

1. `docs/PROJECT-COMPLETION-MASTER-PLAN.md` — large and materially stale/duplicative. It is still referenced by root governance files, so do not delete it until those cross-references are deliberately updated. Mark it superseded or move it under `docs/archive/`.
2. `docs/audit-report.md` and `docs/contrast-matrix.md` — move together to `docs/archive/frontend/` if the cream/lime visual iteration is no longer the active design. Their palette values do not match the current design tokens.
3. `docs/audits/SQLSERVER-TEST-ENVIRONMENT-PREPARATION.md` — may move to `docs/archive/operations/` after the current SQL guide and implementation log remain linked. It is a detailed historical machine-environment timeline, not the primary how-to guide.

**Immediate deletion recommendation: none.** The repository's documentation is over-fragmented and some files are stale, but deleting audit evidence would lose traceability. First archive the superseded documents and update links; delete only after confirming no active references and that their historical value is intentionally discarded.

## 5. Required follow-up edits to existing documents

- `continuity/PROJECT-CHARTER-AND-CONTINUITY.md`: replace the old SEC-05 design-only section with the implemented AUTH-01 state; clarify current `ILogger` versus planned Serilog/OBS-03, SEC-04 and AUD-01; remove blanket phase statuses in favor of task-level root roadmap status.
- `domain/LICENSING-DOMAIN-SPECIFICATION.md`: remove the asserted rule that detained licenses cannot be replaced. Separate the verified renewal check from the unresolved replacement policy.
- `continuity/CORRECTION-LOG.md`: append a verified correction noting the stale SEC-05 description, old cross-process “not verified” line superseded by the separate cross-process report, and earlier CI-SQL report superseded by the Azure execution record.
- `audits/auth-01-post-sql-verification.md`: append a dated cross-reference to `auth-01-cross-process-verification.md`; preserve the original report as a point-in-time result.
- `audits/ci-sql-01-azure-pipelines-sql-reliability.md`: append a note that the implementation execution log later records Azure run `#20261111.2` as successful; cite the execution log rather than silently rewriting the original test state.
- `testing/sql-server-integration-tests.md`: retain the net8 instructions only as the current manifest baseline, but add the explicit unsupported-runtime warning and the .NET 10 LTS migration gate.
- `continuity/templates/CURRENT-PROJECT-STATE.template.md`: replace the old root path `D:\DVLD-WPF` with `D:\DVLD-System`.
- `PROJECT-COMPLETION-MASTER-PLAN.md`: add an explicit superseded/historical notice and current authority links; do not maintain two active roadmaps.

---

## 6. External technical source

- [Microsoft .NET Support Policy](https://dotnet.microsoft.com/en-us/platform/support/policy) — .NET 8 support ended 2026-11-10; .NET 10 is LTS and supported through 2028-11-14.
- [Microsoft announcement: .NET 8 and 9 end of support](https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/).
- [Microsoft EF Core efficient querying guidance](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying).

## 7. Review integrity

This review inspected the public GitHub `main` tree and source/document contents at commit `e4435e3006d88d99ed8ccac2f8fec741d5446afa`. It did not access the user's local `D:\DVLD-System` working tree, did not inspect local uncommitted changes, did not connect to SQL Server, did not access `DVLDf`, and did not run build/tests. This file is a documentation review, not a runtime certification.
