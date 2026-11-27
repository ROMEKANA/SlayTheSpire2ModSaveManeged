[CmdletBinding(DefaultParameterSetName = 'Source', PositionalBinding = $false)]
param(
    [Parameter(Position = 0)]
    [string]$PathsFile,
    [Parameter(ParameterSetName = 'GitHub')]
    [switch]$GitHubReport,
    [Parameter(ParameterSetName = 'Source')]
    [string]$OutputPath,
    [Parameter(ParameterSetName = 'Source', Position = 1)]
    [string]$ToolPath,
    [Parameter(ParameterSetName = 'Source', Position = 2)]
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
$PathsFile = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($PathsFile)
if (!(Test-Path -LiteralPath $PathsFile -PathType Leaf)) { throw 'Run Setup.ps1 first to create local/paths.txt.' }
$paths = [IO.File]::ReadAllLines($PathsFile)
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
$githubReportPath = Join-Path $repo 'diagnostics/save-build.txt'
if ($GitHubReport) {
    $report = $githubReportPath
} elseif ($OutputPath) {
    $report = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
    if ($report -eq $githubReportPath) { throw 'Use -GitHubReport to write the public metadata file.' }
} else {
    $report = Join-Path $local 'save-sources.txt'
}

$hash = (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash
$text = New-Object System.Text.StringBuilder
if ($GitHubReport) {
    $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($assembly).Version
    $null = $text.AppendLine('Slay the Spire 2 build metadata')
    $null = $text.AppendLine('Assembly version: ' + $assemblyVersion)
    $null = $text.AppendLine('Assembly SHA256: ' + $hash)
} else {
    if ($ToolPath) { $ToolPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ToolPath) }
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
        $version = @(& $ToolPath --version)
        if ($LASTEXITCODE -ne 0 -or !($version -match '^ilspycmd: 9\.1\.0\.7988$')) { throw 'Expected ILSpyCmd 9.1.0.7988.' }
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
    } finally { Pop-Location }
}

if ((Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash -ne $hash) {
    throw 'Game DLL changed during export. Previous report retained; retry after the update finishes.'
}
New-Item -ItemType Directory -Path (Split-Path $report -Parent) -Force | Out-Null
$temporary = $report + '.tmp-' + [guid]::NewGuid().ToString('N')
try {
    [IO.File]::WriteAllText($temporary, $text.ToString())
    if (Test-Path -LiteralPath $report -PathType Leaf) {
        $backup = if ($GitHubReport) { [NullString]::Value } else { $report + '.previous-' + [guid]::NewGuid().ToString('N') }
        [IO.File]::Replace($temporary, $report, $backup)
    } else {
        [IO.File]::Move($temporary, $report)
    }
} finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
}
Write-Host "Report ready: $report"
