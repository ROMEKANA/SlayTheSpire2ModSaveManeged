[CmdletBinding()]
param([Parameter(Mandatory)][string]$GameDir)
$ErrorActionPreference = 'Stop'
if (Get-Process -Name 'sts2*' -ErrorAction SilentlyContinue) { throw 'Close STS2 before installing' }
$repo = Split-Path $PSScriptRoot -Parent
$source = Join-Path $repo 'artifacts/mods/UnifiedVanillaSave'
$destination = Join-Path $GameDir 'mods/UnifiedVanillaSave'
if (!(Test-Path -LiteralPath (Join-Path $GameDir 'data_sts2_windows_x86_64/sts2.dll'))) { throw 'Unexpected GameDir' }
foreach ($name in @('UnifiedVanillaSave.dll', 'UnifiedVanillaSave.json')) {
    if (!(Test-Path -LiteralPath (Join-Path $source $name))) { throw "Run Build.ps1 first: missing $name" }
}
if (Test-Path -LiteralPath $destination) { throw "Already installed. Move the old mod folder outside mods before retrying: $destination" }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Get-ChildItem -LiteralPath $source -File | Copy-Item -Destination $destination
Write-Host "Installed at $destination. Check config, backup and ACTIVE log before playing."
