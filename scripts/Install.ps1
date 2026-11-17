[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$GameDir,
    [string]$GameDataDir
)
$ErrorActionPreference = 'Stop'
if (Get-Process -Name 'sts2*' -ErrorAction SilentlyContinue) { throw 'Close STS2 before installing' }
$repo = Split-Path $PSScriptRoot -Parent
$source = Join-Path $repo 'artifacts/mods/UnifiedVanillaSave'
if (!(Test-Path -LiteralPath $GameDir -PathType Container)) {
    throw "GameDir does not exist: '$GameDir'. In Steam, open STS2 > Manage > Browse local files and use that folder."
}
$GameDir = (Resolve-Path -LiteralPath $GameDir).ProviderPath
if (!$GameDataDir) { $GameDataDir = Join-Path $GameDir 'data_sts2_windows_x86_64' }
$expectedAssembly = Join-Path $GameDataDir 'sts2.dll'
if (!(Test-Path -LiteralPath $expectedAssembly -PathType Leaf)) {
    throw "Cannot find '$expectedAssembly'. GameDir='$GameDir'. Use the game installation root, not this source repository or its data folder. If the data folder has another name, pass -GameDataDir with the directory containing sts2.dll."
}
$GameDataDir = (Resolve-Path -LiteralPath $GameDataDir).ProviderPath
$gamePrefix = $GameDir.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (!$GameDataDir.StartsWith($gamePrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "GameDataDir must be inside GameDir. Installation root='$GameDir'; data directory='$GameDataDir'."
}
$destination = Join-Path $GameDir 'mods/UnifiedVanillaSave'
foreach ($name in @('UnifiedVanillaSave.dll', 'UnifiedVanillaSave.json')) {
    if (!(Test-Path -LiteralPath (Join-Path $source $name))) { throw "Run Build.ps1 first: missing $name" }
}
if (Test-Path -LiteralPath $destination) { throw "Already installed. Move the old mod folder outside mods before retrying: $destination" }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
foreach ($name in @('UnifiedVanillaSave.dll', 'UnifiedVanillaSave.json')) {
    Copy-Item -LiteralPath (Join-Path $source $name) -Destination $destination
}
Write-Host "Installed at $destination. Check PATCH_INSTALLED and RELATIVE_ROUTE logs before playing."
