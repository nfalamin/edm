# tools/TestSiteGrabberAndQueueParity.ps1
# Deterministic Real E2E Site Grabber, Queue Manager, Login Vault & Category Parity Harness

[CmdletBinding()]
param(
    [string]$RootDir = ""
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($RootDir)) {
    $RootDir = Split-Path -Parent $PSScriptRoot
}

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host " EDM STAGE 4 PROMPT 6: SITE GRABBER, LOGIN & CATEGORY PARITY     " -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

$testDll = Join-Path $RootDir "EDM.Tests\bin\Release\net10.0-windows7.0\EDM.Tests.dll"
if (-not (Test-Path $testDll)) {
    Write-Host "Building EDM.Tests in Release mode..." -ForegroundColor Gray
    dotnet build (Join-Path $RootDir "EDM.slnx") -c Release | Out-Null
}

# 1. Run SiteGrabberAndQueueParityTests (6/6 tests)
Write-Host "[1/3] Running SiteGrabberAndQueueParityTests suite..." -ForegroundColor Yellow
$proc1 = Start-Process -FilePath "dotnet" -ArgumentList "test `"$testDll`" --filter `"FullyQualifiedName~SiteGrabberAndQueueParityTests`" --no-build" -NoNewWindow -Wait -PassThru
if ($proc1.ExitCode -ne 0) {
    Write-Error "SiteGrabberAndQueueParityTests failed."
    exit 1
}
Write-Host "-> PASS: Site grabber normalization, DPAPI vault, pattern expansion, and category routing verified." -ForegroundColor Green

# 2. Run Queue and Scheduler Tests
Write-Host "[2/3] Running Advanced Features and Queue integration tests..." -ForegroundColor Yellow
$proc2 = Start-Process -FilePath "dotnet" -ArgumentList "test `"$testDll`" --filter `"FullyQualifiedName~AdvancedFeaturesTestSuite`" --no-build" -NoNewWindow -Wait -PassThru
if ($proc2.ExitCode -ne 0) {
    Write-Error "AdvancedFeaturesTestSuite failed."
    exit 1
}
Write-Host "-> PASS: Advanced queue manager, scheduling engine, and sync queues verified." -ForegroundColor Green

# 3. Full Download Pipeline Integrity
Write-Host "[3/3] Running Add-URL Download Pipeline Integration..." -ForegroundColor Yellow
$addUrlScript = Join-Path $PSScriptRoot "TestAddUrlDownload.ps1"
$auProc = & powershell.exe -ExecutionPolicy Bypass -File $addUrlScript
if ($LASTEXITCODE -ne 0) {
    Write-Error "TestAddUrlDownload.ps1 failed."
    exit 1
}
Write-Host "-> PASS: Selected assets and batch URLs route into real DownloadManager with cryptographic SHA-256 integrity." -ForegroundColor Green

Write-Host "=================================================================" -ForegroundColor Green
Write-Host " ALL SITE GRABBER, LOGIN VAULT & CATEGORY CHECKS PASSED [VERIFIED]" -ForegroundColor Green
Write-Host "=================================================================" -ForegroundColor Green
exit 0
