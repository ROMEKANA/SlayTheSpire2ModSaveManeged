# Shared release helpers. No game installation or save-data mutations.
$UvsItemId = '3841494553'
function Write-UvsText($Path, $Text) {
    [IO.File]::WriteAllText($Path, $Text, [Text.UTF8Encoding]::new($false))
}
function Start-UvsReport($Path, $Text) {
    if (Test-Path -LiteralPath $Path) { Copy-Item -LiteralPath $Path -Destination ($Path + '.previous') -Force }
    Write-UvsText $Path $Text
}
function Get-UvsShell {
    foreach ($name in @('pwsh.exe', 'powershell.exe', 'pwsh')) {
        $path = Join-Path $PSHOME $name
        if (Test-Path -LiteralPath $path -PathType Leaf) { return @{ File = $path; Prefix = @() } }
    }
    # PowerShell installed as a dotnet global tool runs under the dotnet host.
    return @{ File = (Get-Process -Id $PID).Path; Prefix = @((Join-Path $PSHOME 'pwsh.dll')) }
}
function Invoke-UvsNative($File, [string[]]$Arguments, $Report, $Directory) {
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $File
    $info.WorkingDirectory = $Directory
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.StandardOutputEncoding = [Text.UTF8Encoding]::new($false)
    $info.StandardErrorEncoding = [Text.UTF8Encoding]::new($false)
    $info.Arguments = (@($Arguments | ForEach-Object { '"' + ($_ -replace '(\\*)"', '$1$1\"' -replace '(\\+)$', '$1$1') + '"' }) -join ' ')
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    try {
        if (!$process.Start()) { throw 'Process did not start' }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        $output = $stdout.GetAwaiter().GetResult() + "`n" + $stderr.GetAwaiter().GetResult()
        Write-UvsText $Report ($output + "`nEXIT_CODE=$($process.ExitCode)`n")
        Write-Host $output
        if ($process.ExitCode -ne 0) { throw "Command failed ($($process.ExitCode)). Report: $Report" }
        return $output
    } finally { $process.Dispose() }
}
function Assert-UvsWorkspace($Workspace, [bool]$UpdateListingText) {
    if (([IO.File]::ReadAllText((Join-Path $Workspace 'mod_id.txt'))).Trim() -ne $UvsItemId) { throw 'Unexpected Workshop item ID' }
    $config = Get-Content -LiteralPath (Join-Path $Workspace 'workshop.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($null -ne $config.visibility -or (!$UpdateListingText -and ($null -ne $config.title -or $null -ne $config.description))) { throw 'Listing preservation check failed' }
    $content = [IO.Path]::GetFullPath((Join-Path $Workspace 'content'))
    $files = @(Get-ChildItem -Force -LiteralPath $content -Recurse -File)
    if ($files.Count -ne 2 -or @($files | Where-Object { $_.DirectoryName -ne $content -or $_.Name -notin @('UnifiedVanillaSave.dll', 'UnifiedVanillaSave.json') }).Count) { throw 'Unexpected release payload' }
    $receipt = Get-Content -LiteralPath (Join-Path $Workspace 'payload-hashes.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($file in $files) {
        if ((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne $receipt.($file.Name)) { throw 'Payload changed after preparation' }
    }
}
