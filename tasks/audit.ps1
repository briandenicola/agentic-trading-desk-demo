#!/usr/bin/env pwsh
# Provenance + parity audit: prove that what is running in Azure matches this repo.
#
# Three independent checks, reported as one PASS/FAIL table:
#   1. PROVENANCE (image tag) - every Container App's running image is tagged with the
#      expected short git SHA (images are deployed by :<sha>, not mutable :latest).
#   2. PROVENANCE (runtime /version) - the live process self-reports the same SHA it was
#      built from. ui-app exposes /version; orchestration-api is reachable via the ui-app
#      nginx /api/ proxy at /api/version. (mock-api is internal-only -> image-tag check only.)
#   3. PARITY (terraform drift) - `terraform plan -detailed-exitcode` shows no config drift
#      between Azure and the committed Terraform (exit 0 = in sync, 2 = drift).
#
# Usage (via Taskfile): task cloud:audit -- <region>
# Exit code: 0 = everything matches the repo; 1 = at least one mismatch/drift.

param(
    [Parameter(Mandatory = $true)][string]$Region,
    [string]$InfraDir = 'infra',
    [string]$ExpectedSha = '',
    [string]$DemoMode = 'true',
    [string]$Environment = 'demo',
    [string]$EnableFoundry = 'false'
)

$ErrorActionPreference = 'Stop'

if (-not $ExpectedSha) { $ExpectedSha = (git rev-parse --short HEAD).Trim() }
$gitClean = -not (git status --porcelain)

$results = [System.Collections.Generic.List[object]]::new()
function Add-Result([string]$check, [bool]$pass, [string]$detail) {
    $results.Add([pscustomobject]@{ Check = $check; Result = $(if ($pass) { 'PASS' } else { 'FAIL' }); Detail = $detail })
}

Write-Host ''
Write-Host "=== Provenance/parity audit - region '$Region' vs git $ExpectedSha ===" -ForegroundColor Cyan

# Working tree must be clean for the SHA comparison to be meaningful.
Add-Result 'git working tree clean' $gitClean ($(if ($gitClean) { 'no uncommitted changes' } else { 'uncommitted changes present - running image may not match source' }))

# --- Terraform outputs for the selected workspace -------------------------------------------
$rg = (terraform -chdir=$InfraDir output -raw resource_group_name 2>$null)
$uiUrl = (terraform -chdir=$InfraDir output -raw ui_app_url 2>$null)
if (-not $rg) {
    Add-Result 'terraform outputs' $false 'could not read resource_group_name (wrong workspace or not deployed)'
    $rg = $null
}
$uiUrl = ($uiUrl ?? '').TrimEnd('/')

# --- Check 1: running image tag == expected SHA ---------------------------------------------
$apps = @('ui-app', 'orchestration-api', 'mock-api')
if ($rg) {
    foreach ($app in $apps) {
        try {
            $image = (az containerapp show --name $app --resource-group $rg `
                    --query "properties.template.containers[0].image" -o tsv 2>$null)
        }
        catch { $image = $null }
        if (-not $image) {
            Add-Result "image tag: $app" $false 'container app not found / not deployed'
            continue
        }
        $tag = ($image -split ':')[-1]
        Add-Result "image tag: $app" ($tag -eq $ExpectedSha) "running '$image' (tag=$tag, expected=$ExpectedSha)"
    }
}

# --- Check 2: runtime /version self-report == expected SHA ----------------------------------
# ui-app exposes /version; orchestration-api via the ui-app /api/ reverse proxy at /api/version.
$versionProbes = @(
    @{ Name = 'ui-app';            Url = "$uiUrl/version" },
    @{ Name = 'orchestration-api'; Url = "$uiUrl/api/version" }
)
if ($uiUrl) {
    foreach ($probe in $versionProbes) {
        try {
            $resp = Invoke-RestMethod -Uri $probe.Url -TimeoutSec 20 -Method Get
            $sha = "$($resp.sha)"
            Add-Result "/version: $($probe.Name)" ($sha -eq $ExpectedSha) "$($probe.Url) -> sha=$sha (build $($resp.buildTime))"
        }
        catch {
            Add-Result "/version: $($probe.Name)" $false "$($probe.Url) unreachable: $($_.Exception.Message)"
        }
    }
}
else {
    Add-Result '/version probes' $false 'ui_app_url output unavailable - skipped runtime checks'
}

# --- Check 3: terraform drift ----------------------------------------------------------------
$planArgs = @(
    "-chdir=$InfraDir", 'plan', '-detailed-exitcode', '-compact-warnings',
    '-var', "location=$Region", '-var', "environment=$Environment",
    '-var', "demo_mode=$DemoMode", '-var', "enable_foundry=$EnableFoundry",
    '-var', 'deploy_apps=true'
)
& terraform @planArgs | Out-Host
$driftCode = $LASTEXITCODE
switch ($driftCode) {
    0 { Add-Result 'terraform drift' $true 'no config drift (Azure == terraform)' }
    2 { Add-Result 'terraform drift' $false 'DRIFT: terraform plan proposes changes (see plan above)' }
    default { Add-Result 'terraform drift' $false "terraform plan errored (exit $driftCode)" }
}

# --- Report ----------------------------------------------------------------------------------
Write-Host ''
Write-Host '=== Audit summary ===' -ForegroundColor Cyan
$results | Format-Table -AutoSize | Out-Host

$failed = @($results | Where-Object { $_.Result -eq 'FAIL' })
if ($failed.Count -gt 0) {
    Write-Host "AUDIT FAILED: $($failed.Count) check(s) do not match the repo." -ForegroundColor Red
    exit 1
}
Write-Host 'AUDIT PASSED: running containers + Azure config match the repo.' -ForegroundColor Green
exit 0
