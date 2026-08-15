# tools/TestNetworkAndWindowsIntegration.ps1
# Deterministic Real E2E Network + Windows Integration Parity Harness

[CmdletBinding()]
param(
    [string]$RootDir = ""
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($RootDir)) {
    $RootDir = Split-Path -Parent $PSScriptRoot
}

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host " EDM STAGE 4 PROMPT 7: NETWORK & WINDOWS INTEGRATION HARNESS    " -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

$testDll = Join-Path $RootDir "EDM.Tests\bin\Release\net10.0-windows7.0\EDM.Tests.dll"
if (-not (Test-Path $testDll)) {
    Write-Host "Building EDM.Tests in Release mode..." -ForegroundColor Gray
    dotnet build (Join-Path $RootDir "EDM.slnx") -c Release | Out-Null
}

# 1. Run Network and Windows Integration Parity Suite
Write-Host "[1/3] Running Network & Windows Integration Parity Tests (FTP, Proxy, PAC, AV, Update)..." -ForegroundColor Yellow
$proc1 = Start-Process -FilePath "dotnet" -ArgumentList "test `"$testDll`" --filter `"FullyQualifiedName~NetworkAndWindowsIntegrationParityTests`" --no-build" -NoNewWindow -Wait -PassThru
if ($proc1.ExitCode -ne 0) {
    Write-Error "NetworkAndWindowsIntegrationParityTests failed."
    exit 1
}
Write-Host "-> PASS: FTP probes, HTTP/HTTPS/SOCKS5 WebProxy, PAC script rules, safe AV execution, and Update SHA-256 verified." -ForegroundColor Green

# 2. Run FTP and Torrent Engine Tests
Write-Host "[2/3] Running FTP and Torrent Engine Tests..." -ForegroundColor Yellow
$proc2 = Start-Process -FilePath "dotnet" -ArgumentList "test `"$testDll`" --filter `"FullyQualifiedName~FtpAndTorrentEngineTests`" --no-build" -NoNewWindow -Wait -PassThru
if ($proc2.ExitCode -ne 0) {
    Write-Error "FtpAndTorrentEngineTests failed."
    exit 1
}
Write-Host "-> PASS: FTP probing fallback and P2P payload assembly verified." -ForegroundColor Green

# 3. Full Download Pipeline Integrity
Write-Host "[3/3] Running Full Download Pipeline Integration..." -ForegroundColor Yellow
$addUrlScript = Join-Path $PSScriptRoot "TestAddUrlDownload.ps1"
$auProc = & powershell.exe -ExecutionPolicy Bypass -File $addUrlScript
if ($LASTEXITCODE -ne 0) {
    Write-Error "TestAddUrlDownload.ps1 failed."
    exit 1
}
Write-Host "-> PASS: Real network download pipeline executes with cryptographic SHA-256 verification." -ForegroundColor Green

Write-Host "=================================================================" -ForegroundColor Green
Write-Host " ALL NETWORK & WINDOWS INTEGRATION CHECKS PASSED [CERTIFIED]    " -ForegroundColor Green
Write-Host "=================================================================" -ForegroundColor Green
exit 0
