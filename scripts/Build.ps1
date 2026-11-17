[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$GameDataDir,
    [string]$GameAssemblyPath,
    [string]$HarmonyAssemblyPath,
    [string]$GodotAssemblyPath
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (!$GameAssemblyPath) { $GameAssemblyPath = Join-Path $GameDataDir 'sts2.dll' }
if (!$HarmonyAssemblyPath) { $HarmonyAssemblyPath = Join-Path $GameDataDir '0Harmony.dll' }
if (!$GodotAssemblyPath) { $GodotAssemblyPath = Join-Path $GameDataDir 'GodotSharp.dll' }
foreach ($file in @($GameAssemblyPath, $HarmonyAssemblyPath, $GodotAssemblyPath)) {
    if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing game-owned assembly: $file" }
}
Push-Location $repo
try {
    if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet was not found. Install .NET 9 SDK: winget install --id Microsoft.DotNet.SDK.9 --exact'
    }
    $sdkVersion = & dotnet --version
    if ($LASTEXITCODE -ne 0) {
        throw 'The repository requires a .NET 9 SDK (9.0.203 or later in the 9.0 series). .NET 10 alone does not satisfy global.json. Run: winget install --id Microsoft.DotNet.SDK.9 --exact, then reopen PowerShell.'
    }
    Write-Host "Using SDK: $sdkVersion"
    dotnet run --project tests/UnifiedVanillaSave.Tests -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Independent tests failed' }
    dotnet run --project tests/UnifiedVanillaSave.HarmonyTests -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Independent Harmony checks failed' }
    dotnet run --project tools/SaveAudit -c Release -- inspect $GameAssemblyPath
    if ($LASTEXITCODE -ne 0) { throw 'Metadata inspection failed' }
    dotnet build src/UnifiedVanillaSave -c Release "-p:GameAssemblyPath=$GameAssemblyPath" "-p:HarmonyAssemblyPath=$HarmonyAssemblyPath" "-p:GodotAssemblyPath=$GodotAssemblyPath"
    if ($LASTEXITCODE -ne 0) { throw 'Game-referenced build failed' }
    $stage = Join-Path $repo 'artifacts/mods/UnifiedVanillaSave'
    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    foreach ($name in @('UnifiedVanillaSave.dll')) {
        Copy-Item -LiteralPath (Join-Path $repo "src/UnifiedVanillaSave/bin/Release/net9.0/$name") -Destination $stage
    }
    Copy-Item -LiteralPath (Join-Path $repo 'src/UnifiedVanillaSave/UnifiedVanillaSave.json') -Destination $stage
    Write-Host "Staged: $stage (runtime/caller compatibility still requires local verification)"
} finally { Pop-Location }
