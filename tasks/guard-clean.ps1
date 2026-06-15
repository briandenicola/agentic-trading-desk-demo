#!/usr/bin/env pwsh
# Provenance guard: refuse to build container images unless the working tree is clean AND
# the HEAD commit is pushed to a remote. `az acr build` packs the on-disk working tree (not
# git HEAD), so this is what guarantees a built image maps to a real, pushed GitHub commit.
# Override for throwaway local iteration with ALLOW_DIRTY=1 (provenance is then NOT reliable).
$ErrorActionPreference = 'Stop'

if ($env:ALLOW_DIRTY -in @('1', 'true', 'yes')) {
    Write-Warning 'ALLOW_DIRTY set - skipping clean-tree/pushed guard. Image provenance will NOT be reliable.'
    exit 0
}

$porcelain = git status --porcelain
if ($porcelain) {
    Write-Host ''
    Write-Host 'BUILD BLOCKED: working tree is dirty.' -ForegroundColor Red
    Write-Host 'Commit or stash your changes so the image maps to a pushed commit, then rebuild.'
    Write-Host 'Uncommitted changes:'
    Write-Host $porcelain
    Write-Host '(Override with $env:ALLOW_DIRTY=''1'' for a throwaway build.)'
    exit 1
}

$sha = (git rev-parse HEAD).Trim()
$onRemote = git branch -r --contains $sha 2>$null
if (-not $onRemote) {
    Write-Host ''
    Write-Host "BUILD BLOCKED: HEAD ($sha) is not pushed to any remote." -ForegroundColor Red
    Write-Host 'Run: git push   so the image can be traced back to a GitHub commit, then rebuild.'
    Write-Host '(Override with $env:ALLOW_DIRTY=''1'' for a throwaway build.)'
    exit 1
}

$short = (git rev-parse --short HEAD).Trim()
Write-Host "Provenance guard OK - clean tree at $short, pushed to remote." -ForegroundColor Green
exit 0
