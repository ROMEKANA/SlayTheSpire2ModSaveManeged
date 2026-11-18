[CmdletBinding()]
param([string]$PathsFile, [string]$PreviewImage, [string]$UploaderPath, [switch]$UpdateListingText, [switch]$PrepareOnly)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'Workshop.Common.ps1')
if (!$PrepareOnly -and (Get-Process -Name ModUploader -ErrorAction SilentlyContinue)) { throw 'An uploader is still running. Wait for it before retrying.' }
& (Join-Path $PSScriptRoot 'Prepare-Workshop.ps1') -PathsFile $PathsFile -PreviewImage $PreviewImage -UpdateListingText:$UpdateListingText
if ($PrepareOnly) { return }
if ($env:OS -ne 'Windows_NT') { throw 'Uploading requires Windows and Steam running as the Workshop item owner.' }
$local = Join-Path $repo 'local'
$workspace = Join-Path $local 'workshop/UnifiedVanillaSave'
$report = Join-Path $local 'workshop-upload-report.txt'
$guard = [IO.File]::Open((Join-Path $local 'workshop.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
try {
    Start-UvsReport $report "Starting update of item $UvsItemId`n"
    Assert-UvsWorkspace $workspace $UpdateListingText.IsPresent
    if (!$UploaderPath) {
        $toolDir = Join-Path $local ('uploader-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $toolDir -Force | Out-Null
        $zip = Join-Path $toolDir 'uploader.zip'
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -UseBasicParsing -Uri 'https://github.com/megacrit/sts2-mod-uploader/releases/download/v0.2.0/ModUploader-win-x64.zip' -OutFile $zip
        if ((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash -ne '2B55C19CC5932235CA9DBD663CA07D04E5D5A0018402303199F9B4EC8CA06578') { throw 'Official uploader archive checksum mismatch' }
        Expand-Archive -LiteralPath $zip -DestinationPath $toolDir
        $executables = @(Get-ChildItem -LiteralPath $toolDir -Recurse -Filter 'ModUploader.exe' -File)
        if ($executables.Count -ne 1) { throw 'Unexpected uploader archive layout' }
        $UploaderPath = $executables[0].FullName
    }
    $UploaderPath = (Resolve-Path -LiteralPath $UploaderPath).ProviderPath
    Write-Host 'Uploading via Steam; output is saved when the uploader exits. Please wait.'
    $output = Invoke-UvsNative $UploaderPath @('upload', '-w', $workspace, '--id', $UvsItemId) $report (Split-Path $UploaderPath -Parent)
    if ($output -match '(?i)failed to|you will need to accept the workshop legal agreement') { throw "Uploader reported a failure or required action. Inspect $report before retrying." }
    Write-Host "Uploader completed for existing item $UvsItemId. Verify the Workshop item in Steam. Report: $report"
} catch {
    [IO.File]::AppendAllText($report, "FAILED: $($_.Exception.Message)`n")
    throw
} finally { $guard.Dispose() }
