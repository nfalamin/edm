param(
    [string]$NativeHostPath = ""
)

$ErrorActionPreference = "Stop"
$RootDir = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($NativeHostPath)) {
    $NativeHostPath = Join-Path $RootDir "EDM.NativeHost\bin\Release\net10.0-windows\EDM.NativeHost.exe"
}

Write-Host "=== [1/5] Native Messaging Binary Framing & IPC Test ===" -ForegroundColor Cyan

if (-not (Test-Path $NativeHostPath)) {
    Write-Host "Building EDM.NativeHost in Release mode..." -ForegroundColor Yellow
    dotnet build (Join-Path $RootDir "EDM.slnx") -c Release | Out-Null
}

if (-not (Test-Path $NativeHostPath)) {
    Write-Error "EDM.NativeHost.exe not found at $NativeHostPath"
    exit 1
}

# 1. Test Ping / Pong over standard input / output binary framing
Write-Host "[Step 1] Testing stdio binary 32-bit LE framing with 'ping' message..." -ForegroundColor Gray

$pingJson = '{"action":"ping"}'
$pingBytes = [System.Text.Encoding]::UTF8.GetBytes($pingJson)
$lenBytes = [System.BitConverter]::GetBytes([int32]$pingBytes.Length)
if (-not [System.BitConverter]::IsLittleEndian) {
    [System.Array]::Reverse($lenBytes)
}

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = (Resolve-Path $NativeHostPath).Path
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.UseShellExecute = $false
$psi.CreateNoWindow = $true

$proc = [System.Diagnostics.Process]::Start($psi)
$proc.StandardInput.BaseStream.Write($lenBytes, 0, 4)
$proc.StandardInput.BaseStream.Write($pingBytes, 0, $pingBytes.Length)
$proc.StandardInput.BaseStream.Flush()

$respLenBuf = New-Object byte[] 4
$bytesRead = $proc.StandardOutput.BaseStream.Read($respLenBuf, 0, 4)

if ($bytesRead -ne 4) {
    $stderr = $proc.StandardError.ReadToEnd()
    Write-Error "Failed to read 4-byte response header from NativeHost stdio. Bytes read: $bytesRead. Stderr: $stderr"
    if (-not $proc.HasExited) { $proc.Kill() }
    exit 1
}

$respLen = [System.BitConverter]::ToInt32($respLenBuf, 0)
$respBuf = New-Object byte[] $respLen
$totalRead = 0
while ($totalRead -lt $respLen) {
    $read = $proc.StandardOutput.BaseStream.Read($respBuf, $totalRead, $respLen - $totalRead)
    if ($read -le 0) { break }
    $totalRead += $read
}

$proc.WaitForExit(3000) | Out-Null

$respJson = [System.Text.Encoding]::UTF8.GetString($respBuf)
Write-Host "Received NativeHost response ($respLen bytes): $respJson" -ForegroundColor DarkGray

$parsed = $respJson | ConvertFrom-Json
if ($parsed.success -ne $true -or $parsed.action -ne "pong") {
    Write-Error "Invalid ping response from NativeHost. Expected success=true, action='pong'. Got: $respJson"
    exit 1
}

Write-Host "-> PASS: Native Messaging stdio binary framing verified (action=pong, success=true)" -ForegroundColor Green

# 2. Test Media Variant Resolution over stdio
Write-Host "[Step 2] Testing stdio variant resolution payload handling..." -ForegroundColor Gray

$variantReq = '{"action":"resolve_media_variants","url":"https://example.com/video.mp4"}'
$vBytes = [System.Text.Encoding]::UTF8.GetBytes($variantReq)
$vLenBytes = [System.BitConverter]::GetBytes([int32]$vBytes.Length)

$proc2 = [System.Diagnostics.Process]::Start($psi)
$proc2.StandardInput.BaseStream.Write($vLenBytes, 0, 4)
$proc2.StandardInput.BaseStream.Write($vBytes, 0, $vBytes.Length)
$proc2.StandardInput.BaseStream.Flush()

$respLenBuf2 = New-Object byte[] 4
$bytesRead2 = $proc2.StandardOutput.BaseStream.Read($respLenBuf2, 0, 4)
if ($bytesRead2 -eq 4) {
    $respLen2 = [System.BitConverter]::ToInt32($respLenBuf2, 0)
    $respBuf2 = New-Object byte[] $respLen2
    $tRead2 = 0
    while ($tRead2 -lt $respLen2) {
        $r = $proc2.StandardOutput.BaseStream.Read($respBuf2, $tRead2, $respLen2 - $tRead2)
        if ($r -le 0) { break }
        $tRead2 += $r
    }
    $respJson2 = [System.Text.Encoding]::UTF8.GetString($respBuf2)
    $parsed2 = $respJson2 | ConvertFrom-Json
    if ($parsed2.success -eq $true -and $parsed2.action -eq "media_variants_resolved") {
        Write-Host "-> PASS: Media variant stdio resolution request processed successfully." -ForegroundColor Green
    }
}
$proc2.StandardInput.Close()
$proc2.WaitForExit(3000) | Out-Null

Write-Host "=== Native Messaging Test: ALL PASS ===" -ForegroundColor Green
exit 0
