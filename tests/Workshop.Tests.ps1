$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
foreach ($name in @('Workshop.Common.ps1', 'Prepare-Workshop.ps1', 'Publish-Workshop.ps1', 'Workshop.Build.ps1')) {
    $tokens = $null; $errors = $null
    $null = [Management.Automation.Language.Parser]::ParseFile((Join-Path $repo "scripts/$name"), [ref]$tokens, [ref]$errors)
    if ($errors.Count) { throw ($errors | Out-String) }
}
. (Join-Path $repo 'scripts/Workshop.Common.ps1')
function Expect-Failure([scriptblock]$Action) {
    $failed = $false
    try { & $Action } catch { $failed = $true }
    if (!$failed) { throw 'Expected rejection' }
}
$root = Join-Path ([IO.Path]::GetTempPath()) ('uvs workshop 日本語 ' + [guid]::NewGuid().ToString('N'))
$oldOs = $env:OS
try {
    New-Item -ItemType Directory -Path $root -Force | Out-Null
    # A real child process tests argument quoting, both streams and exit propagation.
    $child = Join-Path $root 'native fixture.ps1'
    Write-UvsText $child @'
param([string]$Value, [int]$Code)
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
[Console]::WriteLine($Value)
[Console]::Error.WriteLine('fixture stderr')
exit $Code
'@
    $shell = Get-UvsShell
    $report = Join-Path $root 'native.txt'
    $value = '日本語 path with "quotes" and trailing slash\'
    $output = Invoke-UvsNative $shell.File ($shell.Prefix + @('-NoProfile', '-File', $child, '-Value', $value, '-Code', '0')) $report $root
    if (!$output.Contains($value) -or !$output.Contains('fixture stderr')) { throw 'Native output/argument corruption' }
    Expect-Failure { Invoke-UvsNative $shell.File ($shell.Prefix + @('-NoProfile', '-File', $child, '-Value', 'failure', '-Code', '7')) $report $root }
    if (!(Get-Content $report -Raw).Contains('EXIT_CODE=7')) { throw 'Lost native failure code' }
    Write-Host 'PASS native streams, UTF-8, argument quoting and nonzero exit'

    $fixture = Join-Path $root 'repo'
    $scripts = Join-Path $fixture 'scripts'
    $local = Join-Path $fixture 'local'
    $data = Join-Path $root 'game/data_sts2_windows_x86_64'
    New-Item -ItemType Directory -Path $scripts, $local, $data, (Join-Path $fixture 'workshop') -Force | Out-Null
    Copy-Item (Join-Path $repo 'scripts/*Workshop*.ps1') $scripts
    Copy-Item (Join-Path $repo 'workshop/*.txt') (Join-Path $fixture 'workshop')
    Set-Content (Join-Path $data 'sts2.dll') 'fixture; never a real game assembly'
    $paths = Join-Path $local 'paths.txt'
    [IO.File]::WriteAllLines($paths, [string[]]@((Split-Path $data -Parent), (Join-Path $root 'saves'), (Join-Path $root 'config')))
    $preview = Join-Path $root 'original.png'
    [IO.File]::WriteAllBytes($preview, [Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aOZ8AAAAASUVORK5CYII='))
    # Replace only external process/API boundaries in a temporary copy. No production bypass switches.
    $common = Join-Path $scripts 'Workshop.Common.ps1'
    [IO.File]::AppendAllText($common, @'

function Invoke-UvsNative($File, [string[]]$Arguments, $Report, $Directory) {
    if (Test-Path (Join-Path $Directory 'local/fail-build')) { throw 'fixture build failed' }
    if ($Arguments -contains 'upload') {
        if (($Arguments -join '|') -notmatch '\|--id\|3841494553$') { throw 'Missing fixed update ID' }
        Write-UvsText $Report ($Arguments -join '|')
        if (Test-Path (Join-Path (Split-Path $Report -Parent) 'fail-upload')) { throw 'fixture uploader failure' }
        if (Test-Path (Join-Path (Split-Path $Report -Parent) 'warn-upload')) { return 'Failed to upload content!' }
        return 'Successfully uploaded fixture'
    }
    $stage = Join-Path $Directory 'artifacts/mods/UnifiedVanillaSave'
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    Set-Content (Join-Path $stage 'UnifiedVanillaSave.dll') 'fixture DLL'
    Write-UvsText (Join-Path $stage 'UnifiedVanillaSave.json') '{"id":"UnifiedVanillaSave","version":"0.4.0","has_dll":true,"has_pck":false}'
    Set-Content (Join-Path $stage 'sts2.dll') 'must not ship'
    Set-Content (Join-Path $stage 'config.json') 'must not ship'
    return 'fixture audit/build'
}
function Invoke-RestMethod {
    if (!(Test-Path (Join-Path $local 'api-success'))) { throw 'fixture API unavailable' }
    return @{ response = @{ publishedfiledetails = @(@{ result = 1; publishedfileid = '3841494553'; consumer_app_id = 2868840; preview_url = 'https://fixture.invalid/current.png' }) } }
}
function Invoke-WebRequest($Uri, $OutFile, [switch]$UseBasicParsing) {
    if ($Uri -ne 'https://fixture.invalid/current.png') { throw 'Unexpected network request' }
    Copy-Item -LiteralPath $preview -Destination $OutFile
}

'@)
    $prepare = Join-Path $scripts 'Prepare-Workshop.ps1'
    & $prepare -PathsFile $paths -PreviewImage $preview
    $workspace = Join-Path $local 'workshop/UnifiedVanillaSave'
    Assert-UvsWorkspace $workspace $false
    $config = Get-Content (Join-Path $workspace 'workshop.json') -Raw | ConvertFrom-Json
    if ($null -ne $config.title -or $null -ne $config.description -or $null -ne $config.visibility) { throw 'Listing changed by default' }
    if (@(Get-ChildItem (Join-Path $workspace 'content') -File).Count -ne 2) { throw 'Private payload leaked' }
    Write-Host 'PASS preparation preserves listing and stages only DLL plus manifest'
    & $prepare -PathsFile $paths -PreviewImage $preview -UpdateListingText
    Assert-UvsWorkspace $workspace $true
    Expect-Failure { Assert-UvsWorkspace $workspace $false }
    $config = Get-Content (Join-Path $workspace 'workshop.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if (!$config.description.Contains('初回設定') -or $null -ne $config.visibility) { throw 'Optional text/visibility wrong' }
    Write-Host 'PASS listing update requires opt-in and never changes visibility'
    & $prepare -PathsFile $paths -PreviewImage $preview
    $before = Get-Content (Join-Path $workspace 'payload-hashes.json') -Raw
    Set-Content (Join-Path $local 'fail-build') 'fail'
    Expect-Failure { & $prepare -PathsFile $paths -PreviewImage $preview }
    Remove-Item (Join-Path $local 'fail-build')
    Expect-Failure { & $prepare -PathsFile $paths }
    if ((Get-Content (Join-Path $workspace 'payload-hashes.json') -Raw) -ne $before) { throw 'Failed prepare replaced workspace' }
    Set-Content (Join-Path $local 'api-success') 'allow fixture API'
    & $prepare -PathsFile $paths
    Assert-UvsWorkspace $workspace $false
    Remove-Item (Join-Path $local 'api-success')
    Write-Host 'PASS preview reuse and build/API failures preserve previous workspace'
    Write-UvsText (Join-Path $workspace 'mod_id.txt') '123'
    Expect-Failure { & $prepare -PathsFile $paths -PreviewImage $preview }
    Write-UvsText (Join-Path $workspace 'mod_id.txt') '3841494553'
    Set-Content (Join-Path $workspace 'content/UnifiedVanillaSave.dll') 'tamper'
    Expect-Failure { Assert-UvsWorkspace $workspace $false }
    Write-Host 'PASS conflicting ID and changed payload rejected'
    $env:OS = 'Windows_NT' # Only the fixture runner executes; no real uploader or Steam.
    $publish = Join-Path $scripts 'Publish-Workshop.ps1'
    & $publish -PathsFile $paths -PreviewImage $preview -PrepareOnly
    if (Test-Path (Join-Path $local 'workshop-upload-report.txt')) { throw 'PrepareOnly attempted upload' }
    & $publish -PathsFile $paths -PreviewImage $preview -UploaderPath $shell.File
    $uploadReport = Join-Path $local 'workshop-upload-report.txt'
    if (!(Get-Content $uploadReport -Raw).Contains('--id|3841494553')) { throw 'Uploader may create another item' }
    Set-Content (Join-Path $local 'fail-upload') 'fail'
    Expect-Failure { & $publish -PathsFile $paths -PreviewImage $preview -UploaderPath $shell.File }
    if (!(Get-Content $uploadReport -Raw).Contains('FAILED:')) { throw 'Lost upload failure' }
    Remove-Item (Join-Path $local 'fail-upload')
    Set-Content (Join-Path $local 'warn-upload') 'warn'
    Expect-Failure { & $publish -PathsFile $paths -PreviewImage $preview -UploaderPath $shell.File }
    Assert-UvsWorkspace $workspace $false
    Write-Host 'PASS prepare-only, fixed existing item upload and retained failure report'
    Write-Host '6/6 Workshop fixture groups passed; no game build or Steam upload performed'
} finally {
    $env:OS = $oldOs
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
