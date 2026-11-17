#Requires -Version 5.1
<#
.SYNOPSIS
  Validates and optionally configures the local SQL Server integration-test environment for DVLD.

.DESCRIPTION
  Performs non-destructive checks against the local workspace and local SQL Server instance:
    1. Locates the DVLD repository root and DVLD.sln.
    2. Checks the MSSQLSERVER Windows service status (read-only; never starts or restarts services).
    3. Checks sqlcmd availability on PATH.
    4. Connects to 'master' via Windows Authentication (read-only) to retrieve ServerName,
       ServiceName, exact ExpectedInstance ("SERVERPROPERTY('ServerName')|@@SERVICENAME"),
       current Windows login, and database-creation permissions.
    5. Validates DVLD_TEST_CONNECTION and DVLD_TEST_EXPECTED_INSTANCE against DVLD safety rules
       (rejects DVLD/DVLDf catalogs, AttachDBFilename, User Instance, Failover Partner,
       SQL login credentials, and ReadOnly intent).
    6. In -Setup mode, configures DVLD_TEST_CONNECTION and DVLD_TEST_EXPECTED_INSTANCE in the
       current PowerShell process only (never User or Machine scope). Refuses to silently
       overwrite conflicting existing values.

  No database is ever created, modified, or dropped by this script.

.PARAMETER Setup
  When specified, sets DVLD_TEST_CONNECTION and DVLD_TEST_EXPECTED_INSTANCE in the current
  PowerShell process after all SQL Server safety and permission checks pass.

.PARAMETER Server
  SQL Server endpoint to probe with sqlcmd. Defaults to 'localhost'.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\scripts\testing\Test-DatabaseIntegrationEnvironment.ps1
  Validates the local SQL Server environment and existing environment variables.

.EXAMPLE
  . .\scripts\testing\Test-DatabaseIntegrationEnvironment.ps1 -Setup
  Validates the environment and configures DVLD_TEST_CONNECTION and DVLD_TEST_EXPECTED_INSTANCE
  for the current PowerShell session only (dot-sourced so variables persist in your shell).

.EXAMPLE
  Get-Help .\scripts\testing\Test-DatabaseIntegrationEnvironment.ps1 -Full
  Displays full script documentation and parameter details.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [switch]$Setup,

    [Parameter(Mandatory = $false)]
    [string]$Server = 'localhost'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Find-DvldSolutionRoot {
    param([string[]]$CandidateStartPaths)

    foreach ($startPath in $CandidateStartPaths) {
        if ([string]::IsNullOrWhiteSpace($startPath) -or -not (Test-Path -LiteralPath $startPath)) {
            continue
        }

        $current = (Resolve-Path -LiteralPath $startPath).Path
        while (-not [string]::IsNullOrWhiteSpace($current)) {
            $slnPath = Join-Path -Path $current -ChildPath 'DVLD.sln'
            if (Test-Path -LiteralPath $slnPath -PathType Leaf) {
                return [PSCustomObject]@{
                    RootPath     = $current
                    SolutionPath = $slnPath
                }
            }

            $parent = Split-Path -Path $current -Parent
            if ($parent -eq $current) {
                break
            }
            $current = $parent
        }
    }

    return $null
}

function Test-ConnectionStringSafety {
    param(
        [string]$ConnectionString,
        [string]$ExpectedInstanceEnv,
        [string]$VerifiedInstanceIdentity
    )

    if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
        return [PSCustomObject]@{ IsValid = $false; Reason = 'DVLD_TEST_CONNECTION is not set.' }
    }

    if ([string]::IsNullOrWhiteSpace($ExpectedInstanceEnv)) {
        return [PSCustomObject]@{ IsValid = $false; Reason = 'DVLD_TEST_EXPECTED_INSTANCE is not set.' }
    }

    $builder = New-Object System.Data.Common.DbConnectionStringBuilder
    try {
        $builder.ConnectionString = $ConnectionString
    }
    catch {
        return [PSCustomObject]@{ IsValid = $false; Reason = 'DVLD_TEST_CONNECTION is not a valid connection string.' }
    }

    $keys = @($builder.Keys | ForEach-Object { $_.ToString().ToLowerInvariant() })

    if ($keys -contains 'attachdbfilename') {
        return [PSCustomObject]@{ IsValid = $false; Reason = 'DVLD_TEST_CONNECTION must not include AttachDBFilename.' }
    }

    if ($keys -contains 'user instance' -and $builder['user instance'].ToString() -match '^(true|1|yes)$') {
        return [PSCustomObject]@{ IsValid = $false; Reason = 'DVLD_TEST_CONNECTION must not use User Instance.' }
    }

    if ($keys -contains 'failover partner' -and -not [string]::IsNullOrWhiteSpace($builder['failover partner'].ToString())) {
        return [PSCustomObject]@{ IsValid = $false; Reason = 'DVLD_TEST_CONNECTION must not use Failover Partner.' }
    }

    if ($keys -contains 'user id' -or $keys -contains 'uid' -or $keys -contains 'password' -or $keys -contains 'pwd') {
        return [PSCustomObject]@{ IsValid = $false; Reason = 'Windows-authenticated test connections must not include SQL login credentials.' }
    }

    $hasIntegratedSecurity = $false
    if ($keys -contains 'integrated security' -and $builder['integrated security'].ToString() -match '^(true|sspi|1|yes)$') {
        $hasIntegratedSecurity = $true
    }
    if ($keys -contains 'trusted_connection' -and $builder['trusted_connection'].ToString() -match '^(true|1|yes)$') {
        $hasIntegratedSecurity = $true
    }
    if (-not $hasIntegratedSecurity) {
        return [PSCustomObject]@{ IsValid = $false; Reason = 'DVLD_TEST_CONNECTION must use Windows Authentication (Integrated Security=True).' }
    }

    $catalog = ''
    if ($keys -contains 'initial catalog') { $catalog = $builder['initial catalog'].ToString() }
    elseif ($keys -contains 'database') { $catalog = $builder['database'].ToString() }

    if ($catalog -ieq 'DVLD' -or $catalog -ieq 'DVLDf') {
        return [PSCustomObject]@{ IsValid = $false; Reason = "DVLD_TEST_CONNECTION cannot target forbidden application database '$catalog'." }
    }

    if ($keys -contains 'application intent' -and $builder['application intent'].ToString() -ine 'ReadWrite') {
        return [PSCustomObject]@{ IsValid = $false; Reason = 'DVLD_TEST_CONNECTION must use Application Intent=ReadWrite.' }
    }

    $hasDataSource = ($keys -contains 'server' -and -not [string]::IsNullOrWhiteSpace($builder['server'].ToString())) -or
                     ($keys -contains 'data source' -and -not [string]::IsNullOrWhiteSpace($builder['data source'].ToString()))
    if (-not $hasDataSource) {
        return [PSCustomObject]@{ IsValid = $false; Reason = 'DVLD_TEST_CONNECTION must specify a SQL Server endpoint.' }
    }

    if (-not [string]::IsNullOrWhiteSpace($VerifiedInstanceIdentity)) {
        if (-not [string]::Equals($ExpectedInstanceEnv.Trim(), $VerifiedInstanceIdentity.Trim(), [System.StringComparison]::OrdinalIgnoreCase)) {
            return [PSCustomObject]@{
                IsValid = $false
                Reason  = "DVLD_TEST_EXPECTED_INSTANCE ('$ExpectedInstanceEnv') does not match verified instance ('$VerifiedInstanceIdentity')."
            }
        }
    }

    return [PSCustomObject]@{ IsValid = $true; Reason = 'Valid and matches verified SQL Server instance.' }
}

# Results tracking
$servicePass = $false
$sqlcmdPass = $false
$connectionPass = $false
$identityPass = $false
$winAuthPass = $false
$permissionPass = $false
$envPass = $false

$verifiedIdentity = ''
$verifiedLogin = ''

Write-Host '=== DVLD SQL Server Integration Test Environment Check ===' -ForegroundColor Cyan

# Check 1: Project Context
$projectInfo = Find-DvldSolutionRoot -CandidateStartPaths @($PWD.Path, $PSScriptRoot)
if ($null -eq $projectInfo) {
    Write-Host '[FAIL] Project Context: Could not locate DVLD.sln from current directory or script root.' -ForegroundColor Red
    exit 1
}
Write-Host "[PASS] Project Root: $($projectInfo.RootPath) (Solution: $($projectInfo.SolutionPath))" -ForegroundColor Green

# Check 2: SQL Server Service
try {
    $svc = Get-Service -Name 'MSSQLSERVER' -ErrorAction Stop
    if ($svc.Status -eq [System.ServiceProcess.ServiceControllerStatus]::Running) {
        $servicePass = $true
        Write-Host '[PASS] SQL Server Service (MSSQLSERVER): Running' -ForegroundColor Green
    }
    else {
        Write-Host "[FAIL] SQL Server Service (MSSQLSERVER): $($svc.Status) (start service manually)" -ForegroundColor Red
    }
}
catch {
    Write-Host "[FAIL] SQL Server Service (MSSQLSERVER): Could not query service ($($_.Exception.Message))" -ForegroundColor Red
}

# Check 3: sqlcmd
$sqlcmdCmd = Get-Command -Name 'sqlcmd' -ErrorAction SilentlyContinue
if ($null -ne $sqlcmdCmd) {
    $sqlcmdPass = $true
    Write-Host "[PASS] sqlcmd: $($sqlcmdCmd.Source)" -ForegroundColor Green
}
else {
    Write-Host '[FAIL] sqlcmd: Not found on PATH. Install SQL Server Command Line Utilities (sqlcmd).' -ForegroundColor Red
}

# Check 4: SQL Server Connectivity (Read-only query to master)
if ($sqlcmdPass -and $servicePass) {
    $probeQuery = "SET NOCOUNT ON; SELECT CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')) + N'|' + CONVERT(nvarchar(128), @@SERVICENAME), SUSER_SNAME(), CASE WHEN HAS_PERMS_BY_NAME(NULL, NULL, N'CREATE ANY DATABASE') = 1 OR HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'CREATE DATABASE') = 1 OR IS_SRVROLEMEMBER(N'dbcreator') = 1 OR IS_SRVROLEMEMBER(N'sysadmin') = 1 THEN 1 ELSE 0 END, CONVERT(nvarchar(64), SERVERPROPERTY('AuthMode'));"
    try {
        $rawLines = @(& $sqlcmdCmd.Source -S $Server -d master -E -b -h -1 -W -s '|' -Q $probeQuery 2>&1)
        if ($LASTEXITCODE -eq 0) {
            $dataLine = $rawLines | ForEach-Object { $_.ToString().Trim() } | Where-Object { $_ -match '\|' } | Select-Object -First 1
            if ($null -ne $dataLine) {
                $parts = $dataLine -split '\|'
                if ($parts.Count -ge 4) {
                    $serverName = $parts[0].Trim()
                    $serviceName = $parts[1].Trim()
                    $verifiedIdentity = "$serverName|$serviceName"
                    $verifiedLogin = $parts[2].Trim()
                    $canCreateDb = ($parts[3].Trim() -eq '1')

                    $connectionPass = $true
                    $identityPass = -not [string]::IsNullOrWhiteSpace($serverName) -and -not [string]::IsNullOrWhiteSpace($serviceName)
                    $winAuthPass = -not [string]::IsNullOrWhiteSpace($verifiedLogin)
                    $permissionPass = $canCreateDb

                    Write-Host "[PASS] SQL Server Connection: Connected to $Server (master)" -ForegroundColor Green
                    Write-Host "[PASS] Instance Identity: $verifiedIdentity" -ForegroundColor Green
                    Write-Host "[PASS] Windows Authentication Login: $verifiedLogin" -ForegroundColor Green
                    if ($permissionPass) {
                        Write-Host '[PASS] Database Creation Permission: Granted' -ForegroundColor Green
                    }
                    else {
                        Write-Host '[FAIL] Database Creation Permission: Denied (CREATE ANY DATABASE / dbcreator required)' -ForegroundColor Red
                    }
                }
            }
        }
        else {
            Write-Host "[FAIL] SQL Server Connection: sqlcmd exited with code $LASTEXITCODE" -ForegroundColor Red
        }
    }
    catch {
        Write-Host "[FAIL] SQL Server Connection: $($_.Exception.Message)" -ForegroundColor Red
    }
}
elseif (-not $servicePass) {
    Write-Host '[FAIL] SQL Server Connection: Skipped because MSSQLSERVER service is not running.' -ForegroundColor Red
}
else {
    Write-Host '[FAIL] SQL Server Connection: Skipped because sqlcmd is unavailable.' -ForegroundColor Red
}

# Check 5 & 6: Environment Variables & Safety Validation
$currentConn = [Environment]::GetEnvironmentVariable('DVLD_TEST_CONNECTION', 'Process')
$currentExpected = [Environment]::GetEnvironmentVariable('DVLD_TEST_EXPECTED_INSTANCE', 'Process')

if ($Setup) {
    if (-not ($connectionPass -and $identityPass -and $winAuthPass -and $permissionPass)) {
        Write-Host '[FAIL] Setup Mode: Cannot configure environment variables until SQL Server connectivity, identity, and permissions pass.' -ForegroundColor Red
    }
    else {
        $hasExisting = (-not [string]::IsNullOrWhiteSpace($currentConn)) -or (-not [string]::IsNullOrWhiteSpace($currentExpected))
        if ($hasExisting) {
            $existingCheck = Test-ConnectionStringSafety -ConnectionString $currentConn -ExpectedInstanceEnv $currentExpected -VerifiedInstanceIdentity $verifiedIdentity
            if ($existingCheck.IsValid) {
                $envPass = $true
                Write-Host '[PASS] Setup Mode: Existing process environment variables are already valid and match the verified instance.' -ForegroundColor Green
            }
            else {
                Write-Host "[FAIL] Setup Mode: Existing environment variables conflict with safety/instance checks ($($existingCheck.Reason)). Refusing to silently overwrite." -ForegroundColor Red
            }
        }
        else {
            $safeConn = "Server=$Server;Initial Catalog=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Application Intent=ReadWrite"
            [Environment]::SetEnvironmentVariable('DVLD_TEST_CONNECTION', $safeConn, 'Process')
            [Environment]::SetEnvironmentVariable('DVLD_TEST_EXPECTED_INSTANCE', $verifiedIdentity, 'Process')
            $envPass = $true
            Write-Host "[PASS] Setup Mode: Configured DVLD_TEST_CONNECTION and DVLD_TEST_EXPECTED_INSTANCE ($verifiedIdentity) in current process scope only." -ForegroundColor Green
        }
    }
}
else {
    $valCheck = Test-ConnectionStringSafety -ConnectionString $currentConn -ExpectedInstanceEnv $currentExpected -VerifiedInstanceIdentity $verifiedIdentity
    if ($valCheck.IsValid) {
        $envPass = $true
        Write-Host "[PASS] Environment Variables: $($valCheck.Reason)" -ForegroundColor Green
    }
    else {
        Write-Host "[FAIL] Environment Variables: $($valCheck.Reason)" -ForegroundColor Red
    }
}

# Check 7: Final Summary
$allReady = $servicePass -and $sqlcmdPass -and $connectionPass -and $identityPass -and $winAuthPass -and $permissionPass -and $envPass
Write-Host ''
Write-Host '=== Environment Summary ===' -ForegroundColor Cyan
Write-Host ("  SQL Server service:           {0}" -f $(if ($servicePass) { 'PASS' } else { 'FAIL' }))
Write-Host ("  sqlcmd:                       {0}" -f $(if ($sqlcmdPass) { 'PASS' } else { 'FAIL' }))
Write-Host ("  SQL Server connection:        {0}" -f $(if ($connectionPass) { 'PASS' } else { 'FAIL' }))
Write-Host ("  Instance identity:            {0}" -f $(if ($identityPass) { 'PASS' } else { 'FAIL' }))
Write-Host ("  Windows Authentication:       {0}" -f $(if ($winAuthPass) { 'PASS' } else { 'FAIL' }))
Write-Host ("  Database creation permission: {0}" -f $(if ($permissionPass) { 'PASS' } else { 'FAIL' }))
Write-Host ("  Environment variables:        {0}" -f $(if ($envPass) { 'PASS' } else { 'FAIL' }))
Write-Host ("  Overall environment:          {0}" -f $(if ($allReady) { 'READY' } else { 'NOT READY' })) -ForegroundColor $(if ($allReady) { 'Green' } else { 'Red' })

if ($allReady) {
    exit 0
}
exit 1
