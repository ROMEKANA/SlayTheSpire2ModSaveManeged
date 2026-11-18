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
function Get-UvsPreviewUri($Item, $Report) {
    $uri = $null
    $validUri = [Uri]::TryCreate([string]$Item.preview_url, [UriKind]::Absolute, [ref]$uri)
    # The public legacy API can return an HTTP Steam CDN URL. Upgrade only known
    # Steam image hosts, never download over plaintext or bypass item validation.
    $scheme = if ($validUri) { $uri.Scheme } else { '(invalid)' }
    $hostName = if ($validUri) { $uri.DnsSafeHost } else { '(invalid)' }
    [IO.File]::AppendAllText($Report, "PREVIEW result=$($Item.result); item=$($Item.publishedfileid); app=$($Item.consumer_app_id); scheme=$scheme; host=$hostName`n")
    if ($Item.result -eq 9) {
        throw 'Steam anonymous API returned FileNotFound (9); the item may be private, unavailable, or the ID may be wrong. Visibility was not changed. Supply the current original image once with -PreviewImage; it will be remembered locally for later updates.'
    }
    if ($Item.result -ne 1 -or "$($Item.publishedfileid)" -ne $UvsItemId -or "$($Item.consumer_app_id)" -ne '2868840' -or !$validUri) {
        throw "Cannot verify existing Workshop preview metadata. Send local/workshop-prepare-report.txt, or supply -PreviewImage with the current original image."
    }
    if ($uri.Scheme -eq 'http') {
        $steamHost = $hostName.EndsWith('.steamusercontent.com', [StringComparison]::OrdinalIgnoreCase) -or
            $hostName.EndsWith('.steamstatic.com', [StringComparison]::OrdinalIgnoreCase) -or
            $hostName -eq 'steamuserimages-a.akamaihd.net'
        if (!$steamHost -or !$uri.IsDefaultPort -or $uri.UserInfo) { throw 'Unexpected HTTP preview host/port. No image downloaded; send local/workshop-prepare-report.txt.' }
        $builder = [UriBuilder]::new($uri)
        $builder.Scheme = 'https'
        $builder.Port = -1
        $uri = $builder.Uri
    }
    if ($uri.Scheme -ne 'https' -or $uri.UserInfo) { throw 'Preview must use HTTPS. Send local/workshop-prepare-report.txt.' }
    return $uri.AbsoluteUri
}

function Compress-UvsPreview([byte[]]$Bytes) {
    if ($env:OS -ne 'Windows_NT') { throw 'Automatic preview compression requires Windows.' }
    Add-Type -AssemblyName System.Drawing
    $inputStream = [IO.MemoryStream]::new($Bytes)
    try {
        $sourceImage = [Drawing.Image]::FromStream($inputStream)
        try {
            $scale = [Math]::Min(1.0, 1024.0 / [Math]::Max($sourceImage.Width, $sourceImage.Height))
            do {
                $width = [int][Math]::Max(1, [Math]::Floor($sourceImage.Width * $scale))
                $height = [int][Math]::Max(1, [Math]::Floor($sourceImage.Height * $scale))
                $bitmap = [Drawing.Bitmap]::new($width, $height)
                $outputStream = [IO.MemoryStream]::new()
                try {
                    $graphics = [Drawing.Graphics]::FromImage($bitmap)
                    try {
                        $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                        $graphics.DrawImage($sourceImage, 0, 0, $width, $height)
                    } finally { $graphics.Dispose() }
                    $bitmap.Save($outputStream, [Drawing.Imaging.ImageFormat]::Png)
                    if ($outputStream.Length -lt 1MB) { return ,($outputStream.ToArray()) }
                } finally {
                    $outputStream.Dispose()
                    $bitmap.Dispose()
                }
                $scale *= 0.8
            } while ($width -gt 1 -or $height -gt 1)
            throw 'Could not reduce the preview below 1 MB.'
        } finally { $sourceImage.Dispose() }
    } finally { $inputStream.Dispose() }
}
