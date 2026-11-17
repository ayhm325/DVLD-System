# DVLD Project Charter & Continuity Governance

> **Project Name:** Driver & Vehicle Licensing Department (DVLD) Management System  
> **Repository Root:** `D:\DVLD-System`
> **Document Role:** Authoritative Continuity Framework & Operating Charter  
> **Parent Governance Suite:** [`AGENTS.md`](../../AGENTS.md), [`AI-ENGINEERING-INSTRUCTIONS.md`](../../AI-ENGINEERING-INSTRUCTIONS.md), [`ENGINEERING-DECISIONS.md`](../../ENGINEERING-DECISIONS.md), [`ENGINEERING-ROADMAP.md`](../../ENGINEERING-ROADMAP.md)  
> **Governance Status:** ACTIVE & ENFORCED  
> **Version:** 1.1.0
> **Effective Date:** 2026-11-17

---

## 1. Mission and Continuity Framework

This Project Charter and Continuity Governance document establishes the operating authority, decision vocabulary, evidence standards, session logging obligations, and safety boundaries for all human engineers and AI coding assistants collaborating within the DVLD repository.

The objective of this continuity framework is to ensure seamless, evidence-driven engineering across multiple development sessions and AI invocations, preventing:
1. Hallucinated verification and false completion claims.
2. Silent drift or unauthorized alteration of approved architecture and security policies.
3. Conflation of local project file access with Git state management or commit/push authority.
4. Conflation of documentation preparation duties with repository write authorization.
5. Inadvertent damage to protected operational databases (`DVLDf`).

---

## 2. Authority Model

The DVLD engineering repository operates under a strict, hierarchical authority model:

```mermaid
graph TD
    PO["Project Owner (Final Human Authority)"]
    ADR["ENGINEERING-DECISIONS.md (Authoritative Decision Register)"]
    SPEC["Specialized Standards (Engineering, Security, Testing, Tech Stack)"]
    ROADMAP["ENGINEERING-ROADMAP.md (Prioritized Backlog & Scope)"]
    CHARTER["PROJECT-CHARTER-AND-CONTINUITY.md (Continuity & Governance Reference)"]
    LOGS["Session Logs, Correction Log & State Snapshots"]

    PO --> ADR
    PO --> SPEC
    PO --> ROADMAP
    ADR --> CHARTER
    SPEC --> CHARTER
    ROADMAP --> CHARTER
    CHARTER --> LOGS
```

### 2.1 Final Decision Authority
- The **Project Owner** is the supreme authority for all project approvals, phase authorizations, policy changes, scope adjustments, and conflict resolutions.
- No AI assistant or pair-programming agent may self-authorize implementation phases, database modifications, dependency upgrades, or policy relaxations.

### 2.2 Domain Authority Boundaries
- [`ENGINEERING-DECISIONS.md`](../../ENGINEERING-DECISIONS.md) is the **exclusive authoritative register** for architectural, security, and domain policy decisions.
- Specialized standards documents remain authoritative within their specific technical domains:
  - [`ENGINEERING-STANDARDS.md`](../../ENGINEERING-STANDARDS.md): Layering, persistence patterns, UnitOfWork, API design.
  - [`SECURITY-STANDARDS.md`](../../SECURITY-STANDARDS.md): Authentication, RBAC, Segregation of Duties (SoD), file upload security.
  - [`TESTING-STANDARDS.md`](../../TESTING-STANDARDS.md): Test isolation, LocalDB/SQL Server lifecycle, test data isolation.
  - [`TECHNOLOGY-STACK.md`](../../TECHNOLOGY-STACK.md): Verified technology inventory, versions, dependency gaps.
- This Charter serves as an operational governance and continuity reference. It does **not** override, replace, or establish independent engineering decisions.
- **Document recency alone does not establish authority:** A newer working document or session report cannot supersede an accepted decision in `ENGINEERING-DECISIONS.md` unless formally recorded and approved by the Project Owner.

---

## 3. Decision Status Vocabulary

All technical topics, work items, and proposals across the repository must strictly use the standardized decision vocabulary:

| Status Vocabulary | Operational Definition & Invariant Rules |
|---|---|
| `IMPLEMENTED` | Code, tests, and configurations are fully committed in the repository and verified passing. |
| `APPROVED` | Formally selected and ratified by the Project Owner in `ENGINEERING-DECISIONS.md`. |
| `APPROVED DESIGN` | Conceptual architecture or design parameters are accepted, but implementation is **NOT** authorized without a distinct phase authorization. |
| `PLANNED` | Scoped and prioritized on the roadmap backlog for a future phase; execution is **NOT** authorized. |
| `PROPOSED` | Under review or consideration; no formal acceptance or authorization granted. |
| `PROVISIONAL` | Provisionally selected direction pending external policy verification, empirical validation, or business sign-off. |
| `NEEDS VALIDATION` | Theoretical or plausible approach requiring empirical runtime proof or profiling before approval. |
| `NOT AUTHORIZED` | Explicitly prohibited from execution during the active phase. |
| `PROHIBITED` | Strictly forbidden by safety gates or governance (e.g., modifying `DVLDf`). |
| `SUPERSEDED` | Replaced by a subsequent formally approved decision or corrected architecture. |
| `CLOSED` | Formally concluded or rejected; not open for implementation. |

> [!IMPORTANT]
> **Approved Design vs. Implementation Authorization:**  
> An `APPROVED DESIGN` (such as the SEC-05 token lifetime model) confirms architectural consensus on design parameters, but does **NOT** grant authorization to modify source code, alter schemas, or execute database migrations.

---

## 4. AI Operating Authority and Boundaries

AI coding assistants operate strictly within defined authorization boundaries:

### 4.1 Autonomous Analysis vs. Explicit Authorization
- **Autonomous Analysis Permitted:** Reading repository files, tracing code execution, performing static analysis, identifying root causes, and drafting proposed plans.
- **Explicit Human Authorization Required:**
  1. Modifying any repository file (source code, tests, documentation, configurations).
  2. Executing shell commands that mutate files or launch background processes.
  3. Running builds, unit tests, integration tests, or linters.
  4. Executing Git commands (inspection, state modification, commit, push).
  5. Any interaction with relational database instances.

### 4.2 Bounded Authorization Framework
When the Project Owner grants authorization, it is strictly bounded by:
- **Objective:** The precise problem to be solved.
- **Paths:** Explicit list of authorized files or directories.
- **Operation Types:** Read, Create, Edit, or Verify.
- **Permitted Tools:** Explicit set of allowed IDE or CLI capabilities.
- **Exclusions:** Explicitly prohibited areas (e.g., `DVLDf`).

### 4.3 Stop-on-Risk Protocol
An AI assistant must immediately halt execution and request user guidance if:
- An unanticipated conflict arises with existing user modifications.
- An action would cause data loss, file overwrite, or schema corruption.
- An unauthorized file modification would be required to proceed.
- An undocumented security or architectural side effect is discovered.
- **Technical capability does not imply permission:** The ability to execute a tool or edit a file never substitutes for explicit authorization.

---

## 5.8 Local Repository Access vs. Git Authority

To prevent accidental repository mutation or unintended Git history modifications, permissions are strictly partitioned into independent categories:

```mermaid
graph LR
    subgraph Local Permissions
        A["1. Local File Access (Read)"]
        B["2. Local File Modification (Write)"]
    end
    
    subgraph Git Permissions
        C["3. Git Inspection Authority (git status / diff / log)"]
        D["4. Git Management Authority (add / commit / push / branch)"]
    end
    
    A -.->|Independent| B
    B -.->|Independent| C
    C -.->|Independent| D
```

### 5.8.1 Local Project File Access (Read & Write)
- Refers to reading, creating, or editing files on the local filesystem (`D:\DVLD-System`).
- Local file modification authority applies strictly to the specified target paths and never authorizes Git operations.

### 5.8.2 Git Inspection Authority
- Refers to non-destructive inspection of repository state (`git status`, `git diff`, `git log`, `git branch`).
- Requires separate authorization. When Git inspection is prohibited, AI assistants must rely solely on filesystem tools.

### 5.8.3 Git Management Authority
- Refers to altering Git index, staging, branching, or remote references (`git add`, `git commit`, `git push`, `git reset`, `git checkout`).
- **Strictly Prohibited without explicit, separate human authorization.**

### 5.8.4 Separation of Permissions
- Authorizing local file creation or editing does **NOT** grant Git inspection authority.
- Authorizing Git inspection does **NOT** grant commit, push, or branch authority.
- Commit and push operations always require independent, explicit authorization.

---

## 6. Documentation Obligation vs. Repository Write Authorization

A fundamental governance rule governs all session logging and continuity documentation:

> **The obligation to prepare a session record does not grant permission to create or modify repository files.**

1. **In-Conversation Session Preparation:** If an AI assistant completes a significant engineering turn but lacks repository write authorization, it **must** prepare the complete session log within the conversation response.
2. **Persistence Status Tracking:** The assistant must mark the record with `Record Status: PREPARED IN CONVERSATION` and clearly indicate that repository persistence is pending user approval.
3. **No Silent Omission:** Failure to obtain repository write authorization must never result in omitting documentation.
4. **Application to Charter & Templates:** Creating or updating governance documents, templates, or logs requires explicit authorization for the exact target paths.

---

## 7. Continuity and Evidence Integrity

Project continuity relies on accurate, evidence-backed historical records governed by traceability and no-silent-rewrite controls:

### 7.1 Current Project State as a Snapshot
- [`snapshots/CURRENT-PROJECT-STATE-2026-11-08.md`](./snapshots/CURRENT-PROJECT-STATE-2026-11-08.md) represents a **point-in-time dated snapshot**, not a real-time reflection of the repository.
- Changes to the repository do not automatically mutate the snapshot until an authorized state update is performed.

### 7.2 Session Log Governance
- **Significant Engineering Sessions:** Every session involving file modifications, architectural evaluations, root-cause analyses, security reviews, or decision formulating requires an independent session log in `docs/continuity/SESSION-LOGS/`.
- **Zero-Change Significant Sessions:** Sessions that evaluate architecture or perform deep research require a log even if no code was changed, no tests were run, or no final decision was reached.
- **Minor Learning Sessions:** Lightweight conversational queries or routine clarifications that do not materially impact project continuity do not require independent log files.
- **No Silent Rewriting:** Persisted historical session logs must not be silently rewritten or deleted. This is a governance rule, not a claim of technical immutability; corrections are appended to the Correction Log.
- **Correction Log Mandatory:** Factual errors or superseded historical claims must be formally documented in [`CORRECTION-LOG.md`](./CORRECTION-LOG.md).

### 7.3 Evidence Confidence Levels
All technical assertions in continuity records must carry explicit confidence labels:
- `VERIFIED`: Confirmed within the explicitly stated verification scope by direct evidence, such as captured runtime execution, test output, or static code inspection. A documentation-only review verifies only the documented content, not runtime behavior.
- `REPORTED`: Sourced from previous documentation or external review, but not independently re-verified in the current session.
- `INFERRED`: Derived logically from architecture patterns or indirect evidence; requires empirical confirmation.
- `PENDING`: Unverified assumption or open investigation item awaiting test/runtime proof.
- `SUPERSEDED`: Previously documented claim confirmed to be inaccurate or replaced.

---

## 8. Current Project Decisions and Invariants

All continuity records must maintain strict consistency with the following accepted and provisional decisions:

### 8.1 Segregation of Duties (`ADR-O01`)
- **Status:** `APPROVED — Model B (Explicit Permission Separation)` (Decision Date: 2026-11-08).
- Admin users do not automatically inherit Staff operational counter permissions.
- Staff counter users do not receive administrative system management rights.
- Authorization policy `StaffOnly` remains enforced on licensing counter endpoints.

### 8.2 Detained License Replacement Policy (`ADR-O02`)
- **Status:** `PROVISIONALLY SELECTED — OFFICIAL POLICY VERIFICATION REQUIRED` (Provisional Date: 2026-11-08).
- Internal project simulation rule: A driving license with an active detention must not be replaced until released.
- **Crucial Boundaries:**
  - This is an internal simulation rule, **not** a confirmed legal requirement or verified official Jordanian government procedure. Official policy verification remains required.
  - Lost-license and damaged-license replacement workflows must **not** be assumed to have identical requirements.
  - Technical questions regarding `LicenseId` vs `DriverId` binding and historical relationship preservation remain unresolved.
  - Implementation remains strictly **NOT AUTHORIZED**.

### 8.3 Authentication, Refresh Tokens & Multi-Session Management

**Current status: IMPLEMENTED in the repository under AUTH-01.** The old SEC-05 design-only description has been superseded by the implemented session lifecycle. The current implementation contract is documented in [SECURITY-STANDARDS.md Â§12](../../SECURITY-STANDARDS.md) and the execution evidence is recorded in [IMPLEMENTATION-EXECUTION-LOG.md](../IMPLEMENTATION-EXECUTION-LOG.md), [AUTH-01 SQL verification](../audits/auth-01-post-sql-verification.md), and [cross-process verification](../audits/auth-01-cross-process-verification.md).

| Area | Current verified implementation | Remaining qualification |
|---|---|---|
| Access token | 15-minute JWT with issuer, audience, signature and lifetime validation; security-stamp and session claims | Production secret rotation and configuration still need hosting-environment verification |
| Refresh token | Cryptographically random opaque token; SHA-256 hash persisted in UserSessions; raw token is not stored in SQL | Lost HTTP response and process-termination fault injection remain outstanding |
| Session lifetime | 14-day absolute lifetime and 7-day idle lifetime | Re-review if product policy changes |
| Rotation | Conditional consume with a single winner; token family and replacement relationship tracked | Cross-process rotation/reuse is verified; lost-response recovery is not |
| Reuse/concurrency | 30-second grace behavior; bounded 409 retry; family revocation after confirmed reuse | Preserve current bounded retry and grace semantics |
| Cookie transport | HttpOnly, SameSite=Lax, Secure when request is HTTPS, scoped to the auth API path | Production reverse-proxy and HTTPS termination configuration needs deployment verification |
| CSRF | Required X-CSRF-Protection header on refresh/logout endpoints; explicit credential-enabled CORS origin | Production origins and proxy behavior must be confirmed from deployment configuration |
| Revocation | Logout, logout-all, password changes, role/activation changes and security-stamp rotation | Keep regression tests for every revocation path |
| Browser client | Access token held in memory; refresh uses HttpOnly cookie; single-flight refresh and cross-tab coordination | Current client logout helper clears local state even when the server request fails; server revocation failure visibility needs follow-up |

**Evidence boundary:** repository test records report SQL persistence/rotation/reuse/revocation verification and two-process refresh verification (one successful rotation and one conflict). Those historical runs are linked above; no test suite or database operation is run by this script.

**Remaining AUTH-01 follow-ups:**
- Add lost-response and process-termination fault-injection coverage.
- Review client logout semantics so the UI can distinguish local sign-out from confirmed server-side session revocation.
- Verify cookie Secure behavior, trusted forwarded headers, CORS origins, and secret management against the actual production hosting topology.
- Keep session/security audit events separate from diagnostic application logs.
### 8.4 Logging Architecture Boundaries

Logging is divided into three distinct concerns. Their implementation status must not be conflated:

1. **Diagnostic & Application Logging (OBS-03):**
   - The application already uses the standard Microsoft.Extensions.Logging / ILogger<T> abstraction in services and infrastructure.
   - Centralized structured logging, correlation-ID propagation, and a durable/central sink are not yet implemented as a complete platform capability.
   - Serilog integration and correlation middleware are recorded as planned work in the canonical [ENGINEERING-ROADMAP.md](../../ENGINEERING-ROADMAP.md); package addition and configuration require the approval specified by that roadmap.
2. **Security Event Telemetry (SEC-04):**
   - Security-relevant diagnostic messages exist in selected authentication/user workflows.
   - A dedicated structured security audit service and isolated audit sink are not yet an implemented end-to-end capability. Login, logout, refresh reuse, password changes and account-state changes require consistent event coverage.
3. **Transactional Business Audit Logs (AUD-01):**
   - Durable audit records for high-impact business mutations are a separate design.
   - Schema/table design, retention, privacy/redaction, failure policy and DBA approval remain prerequisites. No specific audit table or UnitOfWork interception strategy is authorized by this charter.

**Never log:** passwords, password hashes, raw JWTs, refresh tokens, cookie values, signing keys, or full connection strings. Security event metadata must be minimized and must not turn diagnostic logs into a second ungoverned citizen-data store.
### 8.5 Phase & Task Authorization Invariants

The old blanket phase labels in this charter are superseded by the task-level lifecycle in the root [ENGINEERING-ROADMAP.md](../../ENGINEERING-ROADMAP.md) and the accepted/rejected decisions in [ENGINEERING-DECISIONS.md](../../ENGINEERING-DECISIONS.md).

- Completed work remains completed only to the scope and evidence recorded in the execution log.
- A task marked Planned, Proposed, Deferred, or Not Authorized must not be implemented without its required explicit approval and prerequisites.
- A completed authentication milestone does not authorize unrelated security changes, audit-schema migrations, regulatory-policy changes, or deployment changes.
- Roadmap inclusion is not implementation authorization.
- If this charter and the canonical roadmap disagree on a task status, stop and reconcile the documents before implementation; do not infer approval.
## 9. Protected Repository Boundaries

### 9.1 Local/Production Database Protection (`DVLDf`)
- Under **NO CIRCUMSTANCES** may any script, command, migration, or test connect to, query, modify, truncate, or drop the development/production database `DVLDf`.
- Integration tests must strictly run against dynamic ephemeral test databases named `DVLD_IntegrationTests_<GUID>` under `DVLD_TEST_CONNECTION`.

### 9.2 Decommissioned Codebases (`Presentation (WPF)/`)
- The legacy WPF desktop client located in `Presentation (WPF)/` has been completely and permanently removed from the repository.
- The active repository architecture consists solely of the ASP.NET Core Web API and the React SPA (`ClientApp/`).

---

## 10. Foundational Engineering Discovery References

The DVLD repository permanently maintains two foundational engineering discovery reports authored during the initial architectural baseline discovery on 2026-11-08:

1. [`docs/audits/phase-a-repository-discovery.md`](../audits/phase-a-repository-discovery.md): Initial deep repository discovery covering solution architecture, technology inventory, business workflow maps, execution paths, and security posture.
2. [`docs/audits/phase-a-supplementary-review.md`](../audits/phase-a-supplementary-review.md): Supplementary deep discovery providing 11 detailed workflow analyses, UnitOfWork and transaction analysis, SQL Server locking and concurrency analysis, and reconciled findings.

### 10.1 Classification and Invariants
- **Classification:** `KEEP — FOUNDATIONAL HISTORICAL ENGINEERING EVIDENCE`.
- **Baseline Commit:** `29e43d6ce26ff6e7f516c45f4f475b10bdc2bfd3` (`main`).
- **Date of Discovery:** `2026-11-08`.
- **Evidentiary Limitations:** Both reports reflect a static code analysis conducted at the baseline commit. They do not reflect subsequent phase remediations and must **never** be cited as real-time guarantees of current codebase state.
- **Evidentiary Weight:** Observations in these reports constitute historical evidence. They must not be converted into approved business rules or architectural decisions without separate policy sign-off and current source verification.

### 10.2 Mandatory Scoped Consultation Rule
All future AI coding assistants, system architects, and pair-programming agents **must consult these foundational reports** if and only if their assigned task materially involves:
- System architecture or cross-layer dependency boundaries.
- Database transactions, UnitOfWork mechanics, or SQL Server concurrency/locking behavior.
- Core domain licensing workflows (e.g., LDL application submission, 3-tier test sequencing, first-time license issuance, renewals, replacements, or detentions).
- Cross-cutting security, JWT authentication lifecycles, or file upload storage mechanics.
- Test suite architecture or ephemeral test database provisioning.

*Note:* Consultation is not required for isolated styling, typographical, or documentation metadata adjustments. Consultation does not grant authority to modify repository files, run tests, or access databases.

---

## 11. Modernization Development & Domain Documentation Structure

Active domain specifications and preserved historical planning reside across specialized documentation directories:

- [`LICENSING-DOMAIN-SPECIFICATION.md`](../domain/LICENSING-DOMAIN-SPECIFICATION.md) — Authoritative domain business constraints, Local Driving License lifecycle, 3-tier sequential exam prerequisites, and post-issuance workflows.
- [`docs/archive/historical-planning/`](../archive/historical-planning/) — Preserved original development plan, modernization vision, target architecture draft, and scoped blocker register.
- [`docs/archive/security-sources/`](../archive/security-sources/) — Preserved historical Security Master Plan and Security Implementation Readiness Assessment.
