$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
foreach ($name in @('Setup.ps1', 'Build.ps1', 'Install.ps1', 'Export-SaveSources.ps1')) {
    $tokens = $null; $errors = $null
    $null = [Management.Automation.Language.Parser]::ParseFile((Join-Path $repo "scripts/$name"), [ref]$tokens, [ref]$errors)
    if ($errors.Count) { throw ($errors | Out-String) }
}
$root = Join-Path ([IO.Path]::GetTempPath()) ('uvs setup checks ' + [guid]::NewGuid().ToString('N'))
$oldConfigOverride = $env:UVS_CONFIG
$oldProgramFiles = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
$oldRoaming = $env:APPDATA
$oldLocal = $env:LOCALAPPDATA
try {
    $env:UVS_CONFIG = ''
    $fixture = Join-Path $root 'repo'
    $scripts = Join-Path $fixture 'scripts'
    $programFiles = Join-Path $root 'program files'
    [Environment]::SetEnvironmentVariable('ProgramFiles(x86)', $programFiles)
    $env:APPDATA = Join-Path $root 'roaming'
    $env:LOCALAPPDATA = Join-Path $root 'local app data'
    $game = Join-Path $programFiles 'Steam/steamapps/common/Slay the Spire 2'
    $data = Join-Path $game 'data_sts2_windows_x86_64'
    $userRoot = Join-Path $env:APPDATA 'SlayTheSpire2'
    $configDir = Join-Path $env:LOCALAPPDATA 'UnifiedVanillaSave'
    $backup = Join-Path $root 'existing backup'
    $toolDir = Join-Path $fixture 'tools/SaveAudit/bin/Release/net9.0'
    New-Item -ItemType Directory -Path $scripts, $data, $userRoot, $configDir, $toolDir -Force | Out-Null
    Copy-Item (Join-Path $repo 'scripts/Setup.ps1') $scripts
    Copy-Item (Join-Path $repo 'scripts/Install.ps1') $scripts
    Copy-Item (Join-Path $repo 'tools/SaveAudit/bin/Release/net9.0/*') $toolDir
    # Location/copy fixtures, not a game or MOD build. The real metadata tool is exercised.
    Copy-Item (Join-Path $repo 'tests/UnifiedVanillaSave.Tests/bin/Release/net9.0/UnifiedVanillaSave.Tests.dll') (Join-Path $data 'sts2.dll')
    Set-Content (Join-Path $userRoot 'save-marker') 'preserve progress'
    @'
param([string]$GameDataDir)
$stage = Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/mods/UnifiedVanillaSave'
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Set-Content (Join-Path $stage 'UnifiedVanillaSave.dll') 'fixture payload'
Set-Content (Join-Path $stage 'UnifiedVanillaSave.json') 'fixture manifest'
'@ | Set-Content (Join-Path $scripts 'Build.ps1')
    $pathsFile = Join-Path $fixture 'local/paths.txt'
    New-Item -ItemType Directory -Path (Split-Path $pathsFile -Parent) -Force | Out-Null
    $assemblyHash = (Get-FileHash (Join-Path $data 'sts2.dll') -Algorithm SHA256).Hash
    $configPath = Join-Path $configDir 'config.json'
    @{
        GameAssemblySha256=$assemblyHash; MethodIlSha256='legacy'; UserDataRoot=$userRoot;
        BackupDirectory=$backup; AuditedGameVersion='fixture'; CallersAndVanillaLayoutVerified=$true
    } | ConvertTo-Json | Set-Content $configPath
    $setup = Join-Path $scripts 'Setup.ps1'
    & $setup
    $generated = [IO.File]::ReadAllLines($pathsFile)
    if ($generated.Count -ne 3 -or $generated[0] -ne $game -or $generated[1] -ne $userRoot -or $generated[2] -ne $configDir) { throw 'Generated paths incorrect' }
    Write-Host 'PASS paths file is created automatically with exactly three resolved paths'
    $installed = Join-Path $game 'mods/UnifiedVanillaSave'
    if (!(Test-Path (Join-Path $installed 'UnifiedVanillaSave.dll'))) { throw 'Installation missing' }
    if ((Get-Content (Join-Path $userRoot 'save-marker')) -ne 'preserve progress') { throw 'Source save changed' }
    $config = Get-Content $configPath -Raw | ConvertFrom-Json
    if ($config.BackupDirectory -ne $backup -or !$config.PathMethodIlSha256.GetAccountDir) { throw 'Config migration wrong' }
    if (!(Test-Path (Join-Path $backup 'receipt.json'))) { throw 'First backup missing' }
    Write-Host 'PASS setup migrates config, preserves saves/backup destination and installs'
    & $setup -PathsFile $pathsFile
    if (@(Get-ChildItem $configDir -Directory -Filter 'previous-mod-*').Count -ne 1) { throw 'Old MOD not preserved' }
    Write-Host 'PASS repeated setup preserves prior installation'
    $configBeforeFailure = Get-Content $configPath -Raw
    $assemblyBefore = [IO.File]::ReadAllBytes((Join-Path $data 'sts2.dll'))
    Set-Content (Join-Path $data 'sts2.dll') 'unknown DLL'
    $rejected = $false
    try { & $setup -PathsFile $pathsFile } catch { $rejected = $true }
    if (!$rejected -or (Get-Content $configPath -Raw) -ne $configBeforeFailure -or !(Test-Path $installed)) { throw 'Unknown DLL was not rejected cleanly' }
    [IO.File]::WriteAllBytes((Join-Path $data 'sts2.dll'), $assemblyBefore)
    Write-Host 'PASS unknown DLL stops before modifying config or installed MOD'
    @'
param([string]$GameDir, [string]$GameDataDir)
$target = Join-Path $GameDir 'mods/UnifiedVanillaSave'
New-Item -ItemType Directory -Path $target -Force | Out-Null
Set-Content (Join-Path $target 'partial-marker') 'partial install'
throw 'Simulated copy failure'
'@ | Set-Content (Join-Path $scripts 'Install.ps1')
    $rejected = $false
    try { & $setup -PathsFile $pathsFile } catch { $rejected = $true }
    if (!$rejected -or !(Test-Path (Join-Path $installed 'UnifiedVanillaSave.dll')) -or (Test-Path (Join-Path $installed 'partial-marker'))) { throw 'Rollback failed' }
    if (@(Get-ChildItem $configDir -Directory -Filter 'failed-install-*').Count -ne 1) { throw 'Failed payload not preserved' }
    Write-Host 'PASS failed install restores old MOD and preserves partial payload outside mods'
    [IO.File]::WriteAllLines($pathsFile, [string[]]@($game, $userRoot))
    $rejected = $false
    try { & $setup -PathsFile $pathsFile } catch { $rejected = $true }
    if (!$rejected) { throw 'Invalid paths file accepted' }
    Write-Host 'PASS malformed three-line file rejected'
} finally {
    $env:UVS_CONFIG = $oldConfigOverride
    [Environment]::SetEnvironmentVariable('ProgramFiles(x86)', $oldProgramFiles)
    $env:APPDATA = $oldRoaming
    $env:LOCALAPPDATA = $oldLocal
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
