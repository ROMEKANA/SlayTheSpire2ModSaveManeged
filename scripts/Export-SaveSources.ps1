[CmdletBinding()]
param(
    [string]$PathsFile,
    [string]$ToolPath,
    [string[]]$TypeNames = @(
        'MegaCrit.Sts2.Core.Saves.UserDataPathProvider',
        'MegaCrit.Sts2.Core.Saves.SaveManager',
        'MegaCrit.Sts2.Core.Saves.Managers.ProfileSaveManager',
        'MegaCrit.Sts2.Core.Saves.Managers.PrefsSaveManager',
        'MegaCrit.Sts2.Core.Saves.Managers.ProgressSaveManager',
        'MegaCrit.Sts2.Core.Saves.Managers.RunSaveManager',
        'MegaCrit.Sts2.Core.Saves.Managers.RunHistorySaveManager'
    )
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (!$PathsFile) { $PathsFile = Join-Path $repo 'local/paths.txt' }
if (!(Test-Path -LiteralPath $PathsFile -PathType Leaf)) { throw 'Run Setup.ps1 first to create local/paths.txt.' }
$paths = [IO.File]::ReadAllLines([IO.Path]::GetFullPath($PathsFile))
if ($paths.Count -ne 3 -or @($paths | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count) {
    throw 'Expected three non-empty lines in the paths file.'
}
$game = [IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($paths[0].Trim()))
$assembly = Join-Path $game 'data_sts2_windows_x86_64/sts2.dll'
if (!(Test-Path -LiteralPath $assembly -PathType Leaf)) {
    $matches = @(Get-ChildItem -LiteralPath $game -Filter sts2.dll -Recurse -File)
    if ($matches.Count -ne 1) { throw "Expected one sts2.dll under game root; found $($matches.Count)." }
    $assembly = $matches[0].FullName
}
$data = Split-Path $assembly -Parent
$local = Join-Path $repo 'local'
New-Item -ItemType Directory -Path $local -Force | Out-Null
Push-Location $repo
try {
    if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install .NET 9 SDK before exporting source.' }
    if (!$ToolPath) {
        $toolDir = Join-Path $local 'ilspy9'
        $exe = if ([IO.Path]::DirectorySeparatorChar -eq '\') { 'ilspycmd.exe' } else { 'ilspycmd' }
        $ToolPath = Join-Path $toolDir $exe
        if (!(Test-Path -LiteralPath $ToolPath -PathType Leaf)) {
            & dotnet tool install --tool-path $toolDir ilspycmd --version 9.1.0.7988 --allow-roll-forward
            if ($LASTEXITCODE -ne 0) { throw 'Pinned ILSpyCmd installation failed.' }
        }
    }
    $ToolPath = [IO.Path]::GetFullPath($ToolPath)
    $version = @(& $ToolPath --version)
    if ($LASTEXITCODE -ne 0 -or !($version -match '^ilspycmd: 9\.1\.0\.7988$')) { throw 'Expected ILSpyCmd 9.1.0.7988.' }
    $hash = (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash
    $text = New-Object System.Text.StringBuilder
    $null = $text.AppendLine('STATIC SAVE SOURCE REPORT: source only; not a runtime acceptance result.')
    $null = $text.AppendLine('Assembly SHA256: ' + $hash)
    $null = $text.AppendLine('Tool: ILSpyCmd 9.1.0.7988')
    foreach ($type in $TypeNames) {
        Write-Host "Exporting $type"
        $source = @(& $ToolPath --disable-updatecheck --referencepath $data -t $type $assembly)
        if ($LASTEXITCODE -ne 0 -or !$source.Count) { throw "Source export failed: $type. Previous report retained." }
        $null = $text.AppendLine("`n=== $type ===")
        foreach ($line in $source) { $null = $text.AppendLine($line) }
    }
    if ((Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash -ne $hash) {
        throw 'Game DLL changed during export. Previous report retained; retry after the update finishes.'
    }
    $report = Join-Path $local 'save-sources.txt'
    $temporary = $report + '.tmp-' + [guid]::NewGuid().ToString('N')
    try {
        [IO.File]::WriteAllText($temporary, $text.ToString())
        if (Test-Path -LiteralPath $report) {
            Move-Item -LiteralPath $report -Destination ($report + '.previous-' + [guid]::NewGuid().ToString('N'))
        }
        Move-Item -LiteralPath $temporary -Destination $report
    } finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
    }
    Write-Host "Source report ready: $report"
    Write-Host 'Game DLL was read for decompilation; no game execution, save access, config change or installation was performed.'
} finally { Pop-Location }
