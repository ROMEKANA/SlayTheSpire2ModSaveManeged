[CmdletBinding()]
param([string]$PathsFile)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (!$PathsFile) { $PathsFile = Join-Path $repo 'local/paths.txt' }
$PathsFile = [IO.Path]::GetFullPath($PathsFile)
if (Get-Process -Name 'sts2*' -ErrorAction SilentlyContinue) { throw 'Close STS2 before setup' }
if (!(Test-Path -LiteralPath $PathsFile)) {
    if (![Environment]::Is64BitOperatingSystem -or !${env:ProgramFiles(x86)} -or !$env:APPDATA -or !$env:LOCALAPPDATA) {
        throw 'Automatic paths require Windows. Provide a three-line paths file with -PathsFile.'
    }
    New-Item -ItemType Directory -Path (Split-Path $PathsFile -Parent) -Force | Out-Null
    $defaults = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Steam/steamapps/common/Slay the Spire 2'),
        (Join-Path $env:APPDATA 'SlayTheSpire2'),
        (Join-Path $env:LOCALAPPDATA 'UnifiedVanillaSave')
    )
    [IO.File]::WriteAllLines($PathsFile, [string[]]$defaults)
    Write-Host "Created three-line paths file: $PathsFile"
}
$lines = [IO.File]::ReadAllLines($PathsFile)
if ($lines.Count -ne 3 -or @($lines | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count) {
    throw "Expected exactly three non-empty lines: game root, save root, config directory. File: $PathsFile"
}
$game, $userRoot, $configDir = @($lines | ForEach-Object {
    [IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($_.Trim()))
})
foreach ($directory in @($game, $userRoot)) {
    if (!(Test-Path -LiteralPath $directory -PathType Container)) { throw "Directory from paths file does not exist: $directory" }
}
$data = Join-Path $game 'data_sts2_windows_x86_64'
if (!(Test-Path -LiteralPath (Join-Path $data 'sts2.dll') -PathType Leaf)) {
    $matches = @(Get-ChildItem -LiteralPath $game -Filter sts2.dll -File -Recurse)
    if ($matches.Count -ne 1) { throw "Expected one sts2.dll under '$game'; found $($matches.Count)." }
    $data = $matches[0].DirectoryName
}
$mods = [IO.Path]::GetFullPath((Join-Path $game 'mods')).TrimEnd([IO.Path]::DirectorySeparatorChar)
if ($configDir -eq $mods -or $configDir.StartsWith($mods + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The config directory must be outside the game mods directory.'
}
$config = Join-Path $configDir 'config.json'
if ($env:UVS_CONFIG -and ![string]::Equals([IO.Path]::GetFullPath($env:UVS_CONFIG), $config, [StringComparison]::OrdinalIgnoreCase)) {
    throw "UVS_CONFIG points elsewhere: '$env:UVS_CONFIG'. Align it with line 3 of the paths file before setup."
}
New-Item -ItemType Directory -Path $configDir -Force | Out-Null
$guard = [IO.File]::Open((Join-Path $configDir 'setup.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
Push-Location $repo
try {
    & (Join-Path $PSScriptRoot 'Build.ps1') -GameDataDir $data
    $tool = Join-Path $repo 'tools/SaveAudit/bin/Release/net9.0/SaveAudit.dll'
    & dotnet $tool setup-config (Join-Path $data 'sts2.dll') $userRoot $config (Join-Path $configDir 'backups/first')
    if ($LASTEXITCODE -ne 0) { throw 'Automatic config/backup setup failed. Existing installation has not been moved.' }
    if (Get-Process -Name 'sts2*' -ErrorAction SilentlyContinue) { throw 'STS2 started during setup. Close it and run setup again.' }
    $destination = Join-Path $game 'mods/UnifiedVanillaSave'
    $previous = $null
    if (Test-Path -LiteralPath $destination) {
        if (!(Test-Path -LiteralPath $destination -PathType Container)) { throw "Install target is not a directory: $destination" }
        $previous = Join-Path $configDir ('previous-mod-' + [guid]::NewGuid().ToString('N'))
        Move-Item -LiteralPath $destination -Destination $previous
    }
    try {
        & (Join-Path $PSScriptRoot 'Install.ps1') -GameDir $game -GameDataDir $data
    } catch {
        $failure = $_
        if (Test-Path -LiteralPath $destination) {
            Move-Item -LiteralPath $destination -Destination (Join-Path $configDir ('failed-install-' + [guid]::NewGuid().ToString('N')))
        }
        if ($previous) { Move-Item -LiteralPath $previous -Destination $destination }
        throw $failure
    }
    Write-Host "Setup completed. Paths: $PathsFile"
    Write-Host "Config: $config"
    Write-Host 'Restart STS2 and verify RELATIVE_ROUTE and vanilla/modded progress sharing.'
} finally {
    Pop-Location
    $guard.Dispose()
}
