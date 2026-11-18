[CmdletBinding()]
param([switch]$NoOpen)
$ErrorActionPreference = 'Stop'

$repo = Split-Path $PSScriptRoot -Parent
$outputDir = Join-Path $repo 'local'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$report = Join-Path $outputDir 'startup-report.txt'
$modLog = Join-Path $env:LOCALAPPDATA 'UnifiedVanillaSave/UnifiedVanillaSave.log'
$gameLog = Join-Path $env:APPDATA 'SlayTheSpire2/logs/godot.log'

$lines = @(
    "Unified Vanilla Save startup report | UTC $([DateTimeOffset]::UtcNow.ToString('o'))"
    ''
    foreach ($entry in @(
        @{ Label = 'UnifiedVanillaSave.log (last 120 lines)'; Path = $modLog }
        @{ Label = 'godot.log (recent UnifiedVanillaSave messages)'; Path = $gameLog }
    )) {
        $entry.Label
        try {
            if ($entry.Path -eq $modLog) {
                Get-Content -LiteralPath $entry.Path -Encoding UTF8 -Tail 120
            } else {
                $matchesInLog = @(Get-Content -LiteralPath $entry.Path -Encoding UTF8 -Tail 2000 |
                    Select-String -SimpleMatch 'UnifiedVanillaSave' | Select-Object -Last 40)
                if (!$matchesInLog.Count) { '(No matching lines)' }
                $matchesInLog | ForEach-Object { $_.Line }
            }
        } catch {
            "Could not read log: $($_.Exception.Message)"
        }
        ''
    }
)
$text = $lines -join [Environment]::NewLine
if ($env:USERPROFILE) {
    $text = [regex]::Replace($text, [regex]::Escape($env:USERPROFILE), '%USERPROFILE%', 'IgnoreCase')
}
$text = $text -replace '(?<!\d)7656119\d{10}(?!\d)', '[Steam account ID]'
[IO.File]::WriteAllText($report, $text, [Text.UTF8Encoding]::new($true))
Write-Host "Report: $report"
if (!$NoOpen -and $env:OS -eq 'Windows_NT') {
    Start-Process explorer.exe -ArgumentList ('/select,"{0}"' -f $report)
}
