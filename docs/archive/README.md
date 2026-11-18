# DVLD — Historical Documentation Archive

> **Directory Role:** Permanent Historical Repository Archive  
> **Repository Root:** `D:\DVLD-System`  
> **Governance Authority:** [`AGENTS.md`](../../AGENTS.md) | [`ENGINEERING-DECISIONS.md`](../../ENGINEERING-DECISIONS.md)  
> **Archival Status:** HISTORICAL REFERENCE ONLY — ZERO ACTIVE AUTHORITY  

---

## 1. Overview and Archival Purpose

This directory (`docs/archive/`) permanently preserves historical planning deliverables, point-in-time audit reports, early architecture drafts, continuity records, dated frontend visual audits, and superseded strategy documents from earlier phases of the DVLD project.

### Core Governance Rules for Archived Assets:
1. **Preserved for Historical Traceability:** These files provide an audit trail of architectural evolution, initial discovery findings, and dated verification evidence.
2. **Zero Active Implementation Authority:** Archived documents do **NOT** represent active execution plans, current architecture standards, approved decisions, or implementation authorization.
3. **No Overriding Active Documentation:** Archived files must **never** be cited to override, weaken, or challenge active root governance or current standards.
4. **Historical Status of Proposals:** Proposals, candidate parameters, or unapproved workflows found within archived documents remain strictly unapproved unless formally ratified in [`ENGINEERING-DECISIONS.md`](../../ENGINEERING-DECISIONS.md).

---

## 2. Active Governance & Standards References

All active engineering, architecture, decision-making, and implementation tasks must consult the authoritative active documents:

| Governance Role | Authoritative Active Document |
|---|---|
| **Universal AI Governance & Operational Workflow** | [`AGENTS.md`](../../AGENTS.md) |
| **Sole Execution Roadmap & Backlog** | [`ENGINEERING-ROADMAP.md`](../../ENGINEERING-ROADMAP.md) |
| **Sole Architectural & Policy Decision Register** | [`ENGINEERING-DECISIONS.md`](../../ENGINEERING-DECISIONS.md) |
| **Developer Onboarding & Workflow Guide** | [`DEVELOPMENT-GUIDE.md`](../development/DEVELOPMENT-GUIDE.md) |
| **System Architecture & Concurrency Standards** | [`ENGINEERING-STANDARDS.md`](../development/ENGINEERING-STANDARDS.md) |
| **Security Architecture, Auth & Client Security** | [`SECURITY-STANDARDS.md`](../development/SECURITY-STANDARDS.md) |
| **Testing Standards, SQL Guide & Ephemeral DB Lifecycle** | [`TESTING-STANDARDS.md`](../development/TESTING-STANDARDS.md) |
| **Verified Technology & Manifest Inventory** | [`TECHNOLOGY-STACK.md`](../development/TECHNOLOGY-STACK.md) |
| **Authoritative Civic Domain Business Rules** | [`LICENSING-DOMAIN-SPECIFICATION.md`](../reference/LICENSING-DOMAIN-SPECIFICATION.md) |

---

## 3. Archive Catalog and Subdirectories

### 3.1 Audits (`docs/archive/audits/`)
Point-in-time discovery, security, concurrency, cancellation-token, REST contract, N+1, and AUTH-01 verification reports (`phase-a-*`, `AUTH-01-*`, `auth-01-*`, `api-01-*`, `rest-01-*`, `concurrency-01-*`, `ci-sql-01-*`, `N1-*`, `NO-SHOW-*`, `POST-COMMIT-*`, `SQLSERVER-TEST-ENVIRONMENT-PREPARATION.md`).

### 3.2 Historical Planning (`docs/archive/historical-planning/`)
- [`DEVELOPMENT-PLAN.md`](./historical-planning/DEVELOPMENT-PLAN.md) — Original modernization plan (domain rules extracted to [`LICENSING-DOMAIN-SPECIFICATION.md`](../reference/LICENSING-DOMAIN-SPECIFICATION.md)).
- [`DEVELOPMENT-VISION.md`](./historical-planning/DEVELOPMENT-VISION.md) — Early modernization vision and context.
- [`RISKS-AND-OPEN-DECISIONS.md`](./historical-planning/RISKS-AND-OPEN-DECISIONS.md) — Early decision register superseded by [`ENGINEERING-DECISIONS.md`](../../ENGINEERING-DECISIONS.md).
- [`TARGET-ARCHITECTURE.md`](./historical-planning/TARGET-ARCHITECTURE.md) — Initial architecture draft superseded by [`ENGINEERING-STANDARDS.md`](../development/ENGINEERING-STANDARDS.md).

### 3.3 Security Sources (`docs/archive/security-sources/`)
- [`SECURITY-MASTER-PLAN.md`](./security-sources/SECURITY-MASTER-PLAN.md) — Original 20-risk master plan (superseded by root roadmap/decisions and active security standards).
- [`SECURITY-IMPLEMENTATION-READINESS.md`](./security-sources/SECURITY-IMPLEMENTATION-READINESS.md) — Initial security posture assessment (consolidated into [`phase-a-security-architecture-audit.md`](./audits/phase-a-security-architecture-audit.md)).

### 3.4 Frontend Design & Audit History (`docs/archive/frontend/`)
- [`audit-report.md`](./frontend/audit-report.md) — Historical frontend UI/tooling audit (`2026-11-09`).
- [`contrast-matrix.md`](./frontend/contrast-matrix.md) — Historical cream/lime WCAG contrast matrix (superseded by the current dark/navy + `#3b82f6` palette).
- [`RESPONSIVE-DESIGN-IMPLEMENTATION-REPORT.md`](./frontend/RESPONSIVE-DESIGN-IMPLEMENTATION-REPORT.md) — Dated responsive layout implementation pass (`2026-11-09`).

### 3.5 Continuity, Snapshots & Templates (`docs/archive/continuity/`)
- [`PROJECT-CHARTER-AND-CONTINUITY.md`](./continuity/PROJECT-CHARTER-AND-CONTINUITY.md) — Historical operating charter (operational AI rules consolidated into root [`AGENTS.md`](../../AGENTS.md)).
- [`CORRECTION-LOG.md`](./continuity/CORRECTION-LOG.md) — Historical factual correction register.
- [`snapshots/CURRENT-PROJECT-STATE-2026-11-08.md`](./continuity/snapshots/CURRENT-PROJECT-STATE-2026-11-08.md) and [`snapshots/CURRENT-PROJECT-STATE-2026-11-17.md`](./continuity/snapshots/CURRENT-PROJECT-STATE-2026-11-17.md) — Point-in-time project state snapshots.
- [`SESSION-LOGS/README.md`](./continuity/SESSION-LOGS/README.md) and [`templates/`](./continuity/templates/) — Session handoff conventions and markdown templates.

### 3.6 Execution History (`docs/archive/execution-history/`)
- [`IMPLEMENTATION-EXECUTION-LOG.md`](./execution-history/IMPLEMENTATION-EXECUTION-LOG.md) — Chronological record of past implementation sessions and test runs.

### 3.7 Superseded Plans & Reviews (`docs/archive/superseded/`)
- [`PROJECT-COMPLETION-MASTER-PLAN.md`](./superseded/PROJECT-COMPLETION-MASTER-PLAN.md) — Superseded project completion baseline.
- [`DVLD-System-Engineering-Improvement-Strategy.md`](./superseded/DVLD-System-Engineering-Improvement-Strategy.md) — Superseded strategic engineering improvement document (`2026-11-17`).
- [`DOCS-REVIEW-AND-CLEANUP-2026-11-17.md`](./superseded/DOCS-REVIEW-AND-CLEANUP-2026-11-17.md) — Pre-restructure documentation review snapshot (`2026-11-17`).
