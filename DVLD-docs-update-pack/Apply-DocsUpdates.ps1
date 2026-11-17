[CmdletBinding()]
param(
    [string]$RepoRoot = 'D:\DVLD-System',
    [switch]$Apply,
    [switch]$AllowDirtyDocs
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepoPath([string]$RelativePath) {
    return Join-Path $script:RepoRoot $RelativePath
}

function Read-RequiredFile([string]$RelativePath) {
    $path = Get-RepoPath $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required file not found: $path"
    }
    return [System.IO.File]::ReadAllText($path)
}

function Replace-ExactlyOnce([string]$Content, [string]$Pattern, [string]$Replacement, [string]$Label) {
    $matches = [regex]::Matches($Content, $Pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
    if ($matches.Count -ne 1) {
        throw "Expected exactly one match for '$Label'; found $($matches.Count). No files have been written."
    }
    return [regex]::Replace(
        $Content,
        $Pattern,
        [System.Text.RegularExpressions.MatchEvaluator]{ param($m) return $Replacement },
        [System.Text.RegularExpressions.RegexOptions]::Singleline
    )
}

function Add-Once([string]$Content, [string]$Marker, [string]$Addition, [string]$Label) {
    if ($Content.Contains($Marker)) { return $Content }
    return $Content.TrimEnd() + "`r`n`r`n" + $Addition.Trim() + "`r`n"
}

function Prepend-Once([string]$Content, [string]$Marker, [string]$Addition, [string]$Label) {
    if ($Content.Contains($Marker)) { return $Content }
    return $Addition.Trim() + "`r`n`r`n" + $Content.TrimStart()
}

function Set-ContentUtf8([string]$Path, [string]$Content) {
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

$script:RepoRoot = [System.IO.Path]::GetFullPath($RepoRoot)
$packageRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$packageDocs = Join-Path $packageRoot 'docs'

if (-not (Test-Path (Join-Path $script:RepoRoot 'DVLD.sln'))) {
    throw "RepoRoot does not look like DVLD-System (DVLD.sln missing): $script:RepoRoot"
}
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'Git CLI is required for the safety check. Install Git for Windows and retry.'
}

Push-Location $script:RepoRoot
try {
    $dirty = @(git status --porcelain -- docs)
    if ($LASTEXITCODE -ne 0) { throw 'git status failed; refusing to continue.' }
    if ($dirty.Count -gt 0 -and -not $AllowDirtyDocs) {
        $dirty | ForEach-Object { Write-Host $_ -ForegroundColor Yellow }
        throw 'There are existing local changes under docs/. Review/commit them first, or rerun with -AllowDirtyDocs after making a backup.'
    }
} finally {
    Pop-Location
}

# Build every proposed result in memory first. A mismatch aborts before any file is written.
$changes = [ordered]@{}

# 1. Active continuity charter: refresh the obsolete AUTH/SEC-05, logging and phase-status text.
$charterPath = 'docs/continuity/PROJECT-CHARTER-AND-CONTINUITY.md'
$charter = (Read-RequiredFile $charterPath).Replace('D:\DVLD-WPF', 'D:\DVLD-System')
$authSection = @'
### 8.3 Authentication, Refresh Tokens & Multi-Session Management

**Current status: IMPLEMENTED in the repository under AUTH-01.** The old SEC-05 design-only description has been superseded by the implemented session lifecycle. The current implementation contract is documented in [SECURITY-STANDARDS.md §12](../../SECURITY-STANDARDS.md) and the execution evidence is recorded in [IMPLEMENTATION-EXECUTION-LOG.md](../IMPLEMENTATION-EXECUTION-LOG.md), [AUTH-01 SQL verification](../audits/auth-01-post-sql-verification.md), and [cross-process verification](../audits/auth-01-cross-process-verification.md).

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

'@
$charter = Replace-ExactlyOnce $charter '(?ms)^### 8\.3 Refresh Token & Multi-Session Architecture.*?(?=^### 8\.4 Logging Architecture Boundaries)' $authSection 'charter AUTH section'

$loggingSection = @'
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

'@
$charter = Replace-ExactlyOnce $charter '(?ms)^### 8\.4 Logging Architecture Boundaries.*?(?=^### 8\.5 Phase Authorization Invariants)' $loggingSection 'charter logging section'

$phaseSection = @'
### 8.5 Phase & Task Authorization Invariants

The old blanket phase labels in this charter are superseded by the task-level lifecycle in the root [ENGINEERING-ROADMAP.md](../../ENGINEERING-ROADMAP.md) and the accepted/rejected decisions in [ENGINEERING-DECISIONS.md](../../ENGINEERING-DECISIONS.md).

- Completed work remains completed only to the scope and evidence recorded in the execution log.
- A task marked Planned, Proposed, Deferred, or Not Authorized must not be implemented without its required explicit approval and prerequisites.
- A completed authentication milestone does not authorize unrelated security changes, audit-schema migrations, regulatory-policy changes, or deployment changes.
- Roadmap inclusion is not implementation authorization.
- If this charter and the canonical roadmap disagree on a task status, stop and reconcile the documents before implementation; do not infer approval.

'@
$charter = Replace-ExactlyOnce $charter '(?ms)^### 8\.5 Phase Authorization Invariants.*?(?=^## 9\. Protected Repository Boundaries)' $phaseSection 'charter phase section'
$charter = $charter.Replace('**Version:** 1.0.0', '**Version:** 1.1.0').Replace('**Effective Date:** 2026-11-08', '**Effective Date:** 2026-11-17')
$changes[$charterPath] = $charter

# 2. Domain specification: distinguish verified renewal rule from pending replacement policy.
$domainPath = 'docs/domain/LICENSING-DOMAIN-SPECIFICATION.md'
$domain = (Read-RequiredFile $domainPath).Replace('D:\DVLD-WPF', 'D:\DVLD-System')
$domainSection = @'
### 2.4 Post-Issuance Workflows
1. **License Renewal:**
   - Allowed only when the existing license meets the current renewal eligibility rules.
   - The current renewal service blocks renewal when an active detention record exists; the license must be released first.
   - The old license is deactivated and a new license record is created with the configured validity period and renewal issue reason.
2. **License Replacement (Damaged or Lost):**
   - The current LicenseReplacementService validates that the license exists, is not expired, and is active; it then performs replacement inside a Serializable UnitOfWork transaction.
   - The service currently does not check whether the license has an active detention record.
   - Whether a detained domestic license may be replaced, and whether Lost and Damaged cases differ, is governed by DEC-POL-01 / ADR-O02 and remains subject to written confirmation from the competent licensing authority.
   - Until that external policy confirmation is recorded and the decision is formally accepted, a detention-based replacement prohibition must not be stated as an approved legal or implemented business rule.
3. **License Detention & Release:**
   - An active license may be detained with a recorded fine amount.
   - The renewal workflow's detention check and the replacement workflow's unresolved detention policy are separate rules; do not infer one from the other.
   - Release processing must follow the approved application/fine-payment policy and current implementation. Exact statutory fees and procedures are not established by this engineering specification.
'@
$domain = Replace-ExactlyOnce $domain '(?ms)^### 2\.4 Post-Issuance Workflows.*\z' $domainSection 'domain post-issuance section'
$domain = $domain.Replace('**Effective Date:** 2026-11-08', '**Last Reviewed:** 2026-11-17')
$changes[$domainPath] = $domain

# 3. Update current-state template path only; historical snapshots and audits remain unchanged.
$templatePath = 'docs/continuity/templates/CURRENT-PROJECT-STATE.template.md'
$template = (Read-RequiredFile $templatePath).Replace('D:\DVLD-WPF', 'D:\DVLD-System')
$changes[$templatePath] = $template

# 4. Mark the large completion plan as superseded without rewriting its historic body.
$masterPath = 'docs/PROJECT-COMPLETION-MASTER-PLAN.md'
$master = Read-RequiredFile $masterPath
$masterNotice = @'
> [!WARNING]
> **HISTORICAL / SUPERSEDED BASELINE — NOT THE ACTIVE EXECUTION ROADMAP.** This document preserves a prior consolidation snapshot. Its technology, test counts, authentication status and phase labels may be stale. Current implementation priority is controlled by the root [ENGINEERING-ROADMAP.md](../ENGINEERING-ROADMAP.md); accepted decisions by [ENGINEERING-DECISIONS.md](../ENGINEERING-DECISIONS.md); architecture/security/testing rules by the corresponding root standards. Current point-in-time state is recorded in [the 2026-11-17 snapshot](./continuity/snapshots/CURRENT-PROJECT-STATE-2026-11-17.md). Do not execute tasks from this historical body without reconciling them with those authorities.

'@
$master = Prepend-Once $master 'HISTORICAL / SUPERSEDED BASELINE' $masterNotice 'master plan notice'
$changes[$masterPath] = $master

# 5. Make the frontend audit and contrast matrix explicitly historical, not current compliance/design authority.
$auditPath = 'docs/audit-report.md'
$audit = Read-RequiredFile $auditPath
$auditNotice = @'
> [!NOTE]
> **Historical frontend audit (2026-11-09).** This report records the UI and test-tooling state at its stated baseline commit. The current repository now contains Vitest configuration, seven frontend unit-test files and one Playwright E2E file. Its palette/design commentary is historical and must not be treated as a current visual specification without rechecking ClientApp source.

'@
$audit = Prepend-Once $audit 'Historical frontend audit (2026-11-09)' $auditNotice 'frontend audit notice'
$changes[$auditPath] = $audit

$contrastPath = 'docs/contrast-matrix.md'
$contrast = Read-RequiredFile $contrastPath
$contrastNotice = @'
> [!WARNING]
> **Historical palette matrix — not current WCAG evidence.** This matrix covers cream/lime token pairs. At the reviewed main commit, ClientApp/src/index.css declares a dark/navy surface system and Royal Sapphire Blue accent (#3b82f6). Recalculate ratios from the actual current foreground/background/focus tokens before using this matrix as a compliance or release sign-off.

'@
$contrast = Prepend-Once $contrast 'Historical palette matrix — not current WCAG evidence' $contrastNotice 'contrast matrix notice'
$changes[$contrastPath] = $contrast

# 6. Keep the SQL guide but warn that the checked-in target framework is unsupported.
$sqlGuidePath = 'docs/testing/sql-server-integration-tests.md'
$sqlGuide = Read-RequiredFile $sqlGuidePath
$sqlNote = @'
> [!WARNING] **Runtime lifecycle:** this repository currently targets net8.0 and pins SDK 8.0.427, but Microsoft ended .NET 8 support on 2026-11-10. These commands describe the repository's current manifest state, not a supported production runtime. Prioritize a controlled upgrade to a supported LTS release (currently .NET 10 LTS) after validating EF Core provider compatibility, migrations, build, all test suites and CI. Do not change target frameworks/packages as part of a test-environment setup.

'@
$sqlGuide = Add-Once $sqlGuide 'Microsoft ended .NET 8 support on 2026-11-10' $sqlNote 'SQL guide runtime note'
$changes[$sqlGuidePath] = $sqlGuide

# 7. Append corrections/addenda; preserve the original point-in-time statements.
$correctionPath = 'docs/continuity/CORRECTION-LOG.md'
$correction = Read-RequiredFile $correctionPath
$correctionEntry = @'
## [CORR-2026-11-17-001] Reconcile authentication, frontend testing, Azure CI and domain-policy status

- **Recorded At:** 2026-11-17
- **Correction Status:** EVIDENCE VERIFIED (repository source/document inspection)
- **Decision Approval:** NOT REQUIRED for factual correction; any policy change remains subject to the root decision register.
- **Affected records:** `continuity/PROJECT-CHARTER-AND-CONTINUITY.md` §8.3-8.5; `domain/LICENSING-DOMAIN-SPECIFICATION.md` §2.4; `audits/auth-01-post-sql-verification.md`; `audits/ci-sql-01-azure-pipelines-sql-reliability.md`; `audit-report.md`; `contrast-matrix.md`.

### Verified corrections

1. AUTH-01 refresh/session lifecycle is implemented in current source; old design-only SEC-05 statements are superseded. Cross-process test evidence is recorded in `audits/auth-01-cross-process-verification.md`. Lost-response/process-termination fault injection remains unverified.
2. Frontend tests are present: seven Vitest unit-test files and one Playwright E2E file. The checked-in Azure pipeline was not found to run `npm run test` or `npm run test:e2e`; the presence of tests must not be confused with CI execution.
3. The implementation execution log records Azure pipeline run `#20261111.2` as successful for the SQL CI repair. The earlier CI-SQL audit's “runtime pending” statement was accurate at its original date and is superseded by that later execution record.
4. Current theme tokens in `ClientApp/src/index.css` use a dark/navy palette and Royal Sapphire Blue accent `#3b82f6`; the cream/lime `contrast-matrix.md` is not current accessibility sign-off.
5. Current `LicenseRenewalService` checks active detention; `LicenseReplacementService` does not. Detention-vs-replacement policy remains governed by DEC-POL-01 / ADR-O02 pending external confirmation.

### Evidence boundary

This correction is based on static inspection and previously recorded execution evidence. No tests, database operations, Azure DevOps queries, or legal-policy confirmations were performed during this documentation review. It does not approve any implementation or regulatory decision.

'@
$correction = Add-Once $correction 'CORR-2026-11-17-001' $correctionEntry 'correction log entry'
$changes[$correctionPath] = $correction

$postSqlPath = 'docs/audits/auth-01-post-sql-verification.md'
$postSql = Read-RequiredFile $postSqlPath
$postSqlNote = @'
## Addendum — 2026-11-17: Subsequent cross-process evidence

The original statement in Section 1 that cross-process concurrency was not verified accurately described the scope of this report when it was written. A later, separate verification is recorded in [auth-01-cross-process-verification.md](./auth-01-cross-process-verification.md): two independent API OS processes sharing one isolated SQL database produced exactly one successful refresh response (200) and one conflict (409), with one active replacement generation. Reuse after the grace window revoked the token family. Lost-response recovery and process-termination fault injection remain unverified. Preserve the original result as historical evidence; use the later report for the broader scope.

'@
$postSql = Add-Once $postSql 'Subsequent cross-process evidence' $postSqlNote 'AUTH SQL report addendum'
$changes[$postSqlPath] = $postSql

$ciPath = 'docs/audits/ci-sql-01-azure-pipelines-sql-reliability.md'
$ci = Read-RequiredFile $ciPath
$ciNote = @'
## Addendum — 2026-11-17: Later Azure runtime record

The original “NOT YET VERIFIED” statement above reflects the report date and evidence available at that time. The later [implementation execution log](../IMPLEMENTATION-EXECUTION-LOG.md) records Azure pipeline run `#20261111.2` as successful, including isolated SQL environment provisioning, SQL integration suites, cleanup and artifact publication. This documentation review did not query Azure DevOps independently; treat the execution log as the repository's later recorded evidence and do not rewrite the original historical result.

'@
$ci = Add-Once $ci 'Later Azure runtime record' $ciNote 'CI SQL audit addendum'
$changes[$ciPath] = $ci

# 8. Append a dated documentation-maintenance record to the execution ledger.
$execPath = 'docs/IMPLEMENTATION-EXECUTION-LOG.md'
$execLog = Read-RequiredFile $execPath
$execEntry = @'
## Session 21: Documentation inventory and status reconciliation (2026-11-17)

- **Scope:** Read-only review of all Markdown files under `docs/` and cross-check against current source and root governance documents at commit `e4435e3006d88d99ed8ccac2f8fec741d5446afa`.
- **Changes:** Added the docs index, a current project-state snapshot and a file-by-file cleanup decision record. Reconciled active charter/domain statements and appended dated corrections/addenda to preserve historical evidence.
- **Key reconciliations:** AUTH-01 is implemented; centralized OBS-03/SEC-04/AUD-01 remain distinct planned concerns; frontend tests exist but are not invoked by the checked-in pipeline; detention-vs-replacement remains pending DEC-POL-01; cream/lime contrast ratios do not describe the current blue/dark theme.
- **Verification:** Static GitHub source/document inspection only. No local Git status, build, test suite, database connection, SQL command, Azure DevOps query or production operation was performed.
- **Safety:** No source code, application configuration, database schema or data was changed. No documentation files were deleted.

'@
$execLog = Add-Once $execLog 'Session 21: Documentation inventory and status reconciliation' $execEntry 'execution log session 21'
$changes[$execPath] = $execLog

# 9. New documents supplied in this package. Refuse to overwrite an existing local file.
$newFiles = @(
    @{ Source = (Join-Path $packageDocs 'README.md'); Target = 'docs/README.md' },
    @{ Source = (Join-Path $packageDocs 'DOCS-REVIEW-AND-CLEANUP-2026-11-17.md'); Target = 'docs/DOCS-REVIEW-AND-CLEANUP-2026-11-17.md' },
    @{ Source = (Join-Path $packageDocs 'continuity/snapshots/CURRENT-PROJECT-STATE-2026-11-17.md'); Target = 'docs/continuity/snapshots/CURRENT-PROJECT-STATE-2026-11-17.md' }
)
foreach ($item in $newFiles) {
    if (-not (Test-Path -LiteralPath $item.Source -PathType Leaf)) { throw "Package file missing: $($item.Source)" }
    $target = Get-RepoPath $item.Target
    if (Test-Path -LiteralPath $target) { throw "Refusing to overwrite existing new-document target: $target" }
}

Write-Host "Planned existing-file updates: $($changes.Count)" -ForegroundColor Cyan
$changes.Keys | ForEach-Object { Write-Host "  UPDATE $_" }
Write-Host 'Planned new files:' -ForegroundColor Cyan
$newFiles | ForEach-Object { Write-Host "  CREATE $($_.Target)" }
Write-Host 'No files will be deleted.' -ForegroundColor Green

if (-not $Apply) {
    Write-Host "Dry run only. Review the plan, then rerun with -Apply to write documentation changes." -ForegroundColor Yellow
    return
}

# Back up every changed existing document outside the repository before writing anything.
$backupRoot = Join-Path $env:TEMP ("DVLD-docs-backup-" + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
foreach ($relative in $changes.Keys) {
    $source = Get-RepoPath $relative
    $backup = Join-Path $backupRoot $relative
    $backupDirectory = Split-Path -Parent $backup
    New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $backup
}

$utf8 = New-Object System.Text.UTF8Encoding($false)
foreach ($relative in $changes.Keys) {
    $target = Get-RepoPath $relative
    [System.IO.File]::WriteAllText($target, $changes[$relative], $utf8)
}
foreach ($item in $newFiles) {
    $target = Get-RepoPath $item.Target
    $directory = Split-Path -Parent $target
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    Copy-Item -LiteralPath $item.Source -Destination $target
}

Write-Host "Documentation updates applied. Backup: $backupRoot" -ForegroundColor Green
Write-Host 'Next: inspect git diff -- docs and git status --short. No source files, tests, databases or Git commits were changed.' -ForegroundColor Green
