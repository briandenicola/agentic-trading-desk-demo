<#
.SYNOPSIS
  Stage a clean, fully-hydrated build context outside OneDrive for `az acr build`.

.DESCRIPTION
  This repo lives under OneDrive Files-On-Demand and is reached via a symlink.
  Files are dehydrated reparse-point placeholders that `az acr build`'s tar
  packer silently skips, producing "file does not exist" errors. robocopy reads
  (and therefore hydrates) each file as it mirrors the tree to a real local path.

  robocopy exit codes 0-7 indicate success (>=8 is a genuine failure), so this
  wrapper normalises them to a 0/1 exit code that `task` can consume.
#>
param(
  [Parameter(Mandatory = $true)][string]$Source,
  [Parameter(Mandatory = $true)][string]$Destination
)

$ErrorActionPreference = 'Stop'

$excludeDirs = @('.git', 'bin', 'obj', 'node_modules', 'dist', '.terraform', '.vs', 'TestResults', '.squad', 'mockup', 'specs', 'docs')
$excludeFiles = @('*.tfstate*', 'tfplan', '*.tfplan', '.env')

robocopy $Source $Destination /MIR /COPY:DT /XD $excludeDirs /XF $excludeFiles /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null

if ($LASTEXITCODE -ge 8) {
  Write-Error "robocopy failed with exit code $LASTEXITCODE"
  exit 1
}

Write-Host "Staged hydrated build context at $Destination"
exit 0
