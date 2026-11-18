param([Parameter(Mandatory)][string]$GameDataDir)
$ErrorActionPreference = 'Stop'
# Child-only UTF-8 output, including Windows PowerShell 5.1 native error paths.
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$OutputEncoding = [Console]::OutputEncoding
& (Join-Path $PSScriptRoot 'Build.ps1') -GameDataDir $GameDataDir
