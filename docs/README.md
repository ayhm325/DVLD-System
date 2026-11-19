# DVLD Documentation Index

> **Last Reorganized:** 2026-11-18  
> **Purpose:** Central navigation index for `docs/` and Single Source of Truth authority map.

---

## 1. Documentation Categories (`docs/`)

| Folder | Audience & Purpose | Active Contents |
|---|---|---|
| [`client/`](./client/) | End-user guidance for DVLD counter staff (`Staff`) and administrators (`Admin`) | [`USER-GUIDE.md`](./client/USER-GUIDE.md) |
| [`development/`](./development/) | Developer onboarding, architecture standards, security, testing, observability, and tech stack | [`DEVELOPMENT-GUIDE.md`](./development/DEVELOPMENT-GUIDE.md), [`ENGINEERING-STANDARDS.md`](./development/ENGINEERING-STANDARDS.md), [`SECURITY-STANDARDS.md`](./development/SECURITY-STANDARDS.md), [`TESTING-STANDARDS.md`](./development/TESTING-STANDARDS.md), [`OBSERVABILITY-AND-LOGGING.md`](./development/OBSERVABILITY-AND-LOGGING.md), [`TECHNOLOGY-STACK.md`](./development/TECHNOLOGY-STACK.md) |
| [`reference/`](./reference/) | Stable domain business rules and technical specifications | [`LICENSING-DOMAIN-SPECIFICATION.md`](./reference/LICENSING-DOMAIN-SPECIFICATION.md) |
| [`archive/`](./archive/) | Non-authoritative historical audits, plans, continuity snapshots, and execution logs | [`archive/README.md`](./archive/README.md) (`audits/`, `historical-planning/`, `security-sources/`, `frontend/`, `continuity/`, `execution-history/`, `superseded/`) |

> [!IMPORTANT]
> **Archive Boundary:** Nothing under `docs/archive/` carries active authorization or overrides root governance or `docs/development/` / `docs/reference/` specifications.

---

## 2. Single Source of Truth (Authority Model)

Each topic has exactly **one** authoritative source in the repository:

| Topic | Authoritative Single Source |
|---|---|
| **Product Overview & Quick Start** | [`README.md`](../README.md) (with [`USER-GUIDE.md`](./client/USER-GUIDE.md) for staff/admin usage) |
| **AI Agent Rules, Safety Gates & Workflow** | [`AGENTS.md`](../AGENTS.md) (plus frontend specifics in [`ClientApp/AGENTS.md`](../ClientApp/AGENTS.md)) |
| **Task Priorities, Status & Authorization** | [`ENGINEERING-ROADMAP.md`](../ENGINEERING-ROADMAP.md) |
| **Architectural, Security & Policy Decisions (ADRs)** | [`ENGINEERING-DECISIONS.md`](../ENGINEERING-DECISIONS.md) |
| **Developer Environment Setup, Run & CI Guide** | [`DEVELOPMENT-GUIDE.md`](./development/DEVELOPMENT-GUIDE.md) (and [`ClientApp/README.md`](../ClientApp/README.md) for frontend scripts) |
| **Layered Architecture, UoW, Locking & Error Handling** | [`ENGINEERING-STANDARDS.md`](./development/ENGINEERING-STANDARDS.md) |
| **Authentication (`AUTH-01`), RBAC/SoD, Rate Limits & Client Security** | [`SECURITY-STANDARDS.md`](./development/SECURITY-STANDARDS.md) |
| **Test Isolation, Ephemeral SQL Lifecycle & SQL Testing Guide** | [`TESTING-STANDARDS.md`](./development/TESTING-STANDARDS.md) |
| **Structured Diagnostic Logging & Seq Observability** | [`OBSERVABILITY-AND-LOGGING.md`](./development/OBSERVABILITY-AND-LOGGING.md) |
| **Verified SDK, Target Framework, NuGet, npm & CI Inventory** | [`TECHNOLOGY-STACK.md`](./development/TECHNOLOGY-STACK.md) |
| **Civic Licensing Business Rules & Pending Policy Boundaries** | [`LICENSING-DOMAIN-SPECIFICATION.md`](./reference/LICENSING-DOMAIN-SPECIFICATION.md) (with [`ENGINEERING-DECISIONS.md`](../ENGINEERING-DECISIONS.md)) |
| **Historical Evidence, Past Audits & Execution Logs** | [`docs/archive/README.md`](./archive/README.md) |
