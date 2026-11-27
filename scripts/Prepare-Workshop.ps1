[CmdletBinding()]
param([string]$PathsFile, [string]$PreviewImage, [switch]$UpdateListingText)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'Workshop.Common.ps1')
$local = Join-Path $repo 'local'
New-Item -ItemType Directory -Path $local -Force | Out-Null
$guard = [IO.File]::Open((Join-Path $local 'workshop.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
$report = Join-Path $local 'workshop-prepare-report.txt'
$temp = Join-Path $local ('workshop-stage-' + [guid]::NewGuid().ToString('N'))
$workspace = Join-Path $local 'workshop/UnifiedVanillaSave'
try {
    Start-UvsReport $report "Preparing existing item $UvsItemId. No upload requested.`n"
    if (!$PathsFile) { $PathsFile = Join-Path $local 'paths.txt' }
    if (!(Test-Path -LiteralPath $PathsFile)) {
        if (!${env:ProgramFiles(x86)} -or !$env:APPDATA -or !$env:LOCALAPPDATA) { throw 'Provide a three-line Windows paths file with -PathsFile.' }
        New-Item -ItemType Directory -Path (Split-Path ([IO.Path]::GetFullPath($PathsFile)) -Parent) -Force | Out-Null
        [IO.File]::WriteAllLines($PathsFile, [string[]]@((Join-Path ${env:ProgramFiles(x86)} 'Steam/steamapps/common/Slay the Spire 2'), (Join-Path $env:APPDATA 'SlayTheSpire2'), (Join-Path $env:LOCALAPPDATA 'UnifiedVanillaSave')))
    }
    $lines = [IO.File]::ReadAllLines($PathsFile)
    if ($lines.Count -ne 3 -or @($lines | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count) { throw 'Expected exactly three non-empty paths lines.' }
    $game = [IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($lines[0].Trim()))
    $dll = Join-Path $game 'data_sts2_windows_x86_64/sts2.dll'
    if (!(Test-Path -LiteralPath $dll)) {
        $candidates = @((Join-Path $game 'sts2.dll')) + @(Get-ChildItem -LiteralPath $game -Directory -Filter 'data_sts2_*' | ForEach-Object { Join-Path $_.FullName 'sts2.dll' })
        $found = @($candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
        if ($found.Count -ne 1) { throw 'Expected one game sts2.dll in the game root/data directory.' }
        $dll = $found[0]
    }
    if (Test-Path -LiteralPath (Join-Path $workspace 'mod_id.txt')) {
        if (([IO.File]::ReadAllText((Join-Path $workspace 'mod_id.txt'))).Trim() -ne $UvsItemId) { throw 'Existing workspace has a conflicting Workshop item ID.' }
    }
    $before = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash
    $shell = Get-UvsShell
    Start-UvsReport (Join-Path $local 'workshop-build-report.txt') 'Starting build'
    Start-UvsReport (Join-Path $local 'workshop-audit-report.txt') 'Starting audit'
    Invoke-UvsNative $shell.File ($shell.Prefix + @('-NoProfile', '-File', (Join-Path $PSScriptRoot 'Workshop.Build.ps1'), '-GameDataDir', (Split-Path $dll -Parent))) (Join-Path $local 'workshop-build-report.txt') $repo | Out-Null
    Invoke-UvsNative 'dotnet' @((Join-Path $repo 'tools/SaveAudit/bin/Release/net9.0/SaveAudit.dll'), 'release-audit', $dll) (Join-Path $local 'workshop-audit-report.txt') $repo | Out-Null
    if ($before -ne (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash) { throw 'Game DLL changed during preparation.' }
    New-Item -ItemType Directory -Path (Join-Path $temp 'content') -Force | Out-Null
    $hashes = @{}
    foreach ($name in @('UnifiedVanillaSave.dll', 'UnifiedVanillaSave.json')) {
        Copy-Item -LiteralPath (Join-Path $repo "artifacts/mods/UnifiedVanillaSave/$name") -Destination (Join-Path $temp 'content')
        $hashes[$name] = (Get-FileHash -LiteralPath (Join-Path $temp "content/$name") -Algorithm SHA256).Hash
    }
    $manifest = Get-Content -LiteralPath (Join-Path $temp 'content/UnifiedVanillaSave.json') -Raw | ConvertFrom-Json
    if ($manifest.id -ne 'UnifiedVanillaSave' -or !$manifest.has_dll -or $manifest.has_pck -or $manifest.version -ne '0.5.0') { throw 'Unexpected release manifest' }
    $config = @{ title = $null; description = $null; visibility = $null; changeNote = ''; dependencies = @() }
    if ($UpdateListingText) {
        $config.title = [IO.File]::ReadAllText((Join-Path $repo 'workshop/title.txt')).Trim()
        $config.description = [IO.File]::ReadAllText((Join-Path $repo 'workshop/description.txt')).Trim()
    }
    Write-UvsText (Join-Path $temp 'workshop.json') ($config | ConvertTo-Json -Depth 8)
    Write-UvsText (Join-Path $temp 'mod_id.txt') $UvsItemId
    Write-UvsText (Join-Path $temp 'payload-hashes.json') ($hashes | ConvertTo-Json)
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $rememberedPreview = Join-Path $local 'workshop-preview.png'
    $selectedPreview = $PreviewImage
    if (!$selectedPreview -and (Test-Path -LiteralPath $rememberedPreview -PathType Leaf)) { $selectedPreview = $rememberedPreview }
    if ($selectedPreview) {
        Write-Host "Using owner-selected preview: $selectedPreview"
        Copy-Item -LiteralPath $selectedPreview -Destination (Join-Path $temp 'image.png')
    } else {
        # Always fetch the current preview; a stale cache could undo a later web edit.
        try { $response = Invoke-RestMethod -Method Post -Uri 'https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/' -Body @{ itemcount = 1; 'publishedfileids[0]' = $UvsItemId } }
        catch { throw 'Cannot retrieve the current Workshop preview. Retry with -PreviewImage pointing to the current original image. No upload performed.' }
        $item = @($response.response.publishedfiledetails)[0]
        $previewUri = Get-UvsPreviewUri $item $report
        Invoke-WebRequest -UseBasicParsing -Uri $previewUri -OutFile (Join-Path $temp 'image.png')
    }
    $image = [IO.File]::ReadAllBytes((Join-Path $temp 'image.png'))
    $png = $image.Length -ge 8 -and [BitConverter]::ToString($image[0..7]) -eq '89-50-4E-47-0D-0A-1A-0A'
    $jpeg = $image.Length -ge 3 -and [BitConverter]::ToString($image[0..2]) -eq 'FF-D8-FF'
    if (!($png -or $jpeg)) { throw 'Preview is not a PNG or JPEG. Renaming a file does not convert its format.' }
    if ($image.Length -ge 1MB) {
        $compressed = Compress-UvsPreview $image
        [IO.File]::WriteAllBytes((Join-Path $temp 'image.png'), $compressed)
        Write-Host "Compressed Workshop preview: $($image.Length) -> $($compressed.Length) bytes"
    }
    if ($PreviewImage) {
        # An explicit original image becomes persistent owner input, not an API cache.
        $rememberedTemp = $rememberedPreview + '.tmp-' + [guid]::NewGuid().ToString('N')
        try {
            [IO.File]::WriteAllBytes($rememberedTemp, $image)
            if (Test-Path -LiteralPath $rememberedPreview) { [IO.File]::Replace($rememberedTemp, $rememberedPreview, ($rememberedPreview + '.previous')) }
            else { [IO.File]::Move($rememberedTemp, $rememberedPreview) }
        } finally { if (Test-Path -LiteralPath $rememberedTemp) { Remove-Item -LiteralPath $rememberedTemp } }
    }
    Assert-UvsWorkspace $temp $UpdateListingText.IsPresent
    New-Item -ItemType Directory -Path (Split-Path $workspace -Parent) -Force | Out-Null
    $previous = $null
    if (Test-Path -LiteralPath $workspace) {
        $previous = Join-Path $local ('previous-workshop-' + [guid]::NewGuid().ToString('N'))
        Move-Item -LiteralPath $workspace -Destination $previous
    }
    try { Move-Item -LiteralPath $temp -Destination $workspace }
    catch { if ($previous) { Move-Item -LiteralPath $previous -Destination $workspace }; throw }
    Write-UvsText $report ("PREPARED item=$UvsItemId; version=$($manifest.version); UpdateListingText=$UpdateListingText`nWorkspace=$workspace`n" + ($hashes | ConvertTo-Json))
    Write-Host "Prepared only: $workspace. Listing text preserved unless -UpdateListingText was supplied."
} catch {
    [IO.File]::AppendAllText($report, "FAILED: $($_.Exception.Message)`n")
    throw
} finally { $guard.Dispose() }
