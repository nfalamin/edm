# tools/TestDownloadProgressExperience.ps1
# Deterministic Real E2E Download Progress & Stress Verification Harness
# Tests real progress updates, speed limiting, 32-segment downloads, simultaneous downloads, and pause/resume storms.

[CmdletBinding()]
param(
    [string]$RootDir = ""
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($RootDir)) {
    $RootDir = Split-Path -Parent $PSScriptRoot
}

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host " EDM STAGE 4 PROMPT 5: IDM-GRADE DOWNLOAD PROGRESS CERTIFICATION " -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

$testDll = Join-Path $RootDir "EDM.Tests\bin\Release\net10.0-windows7.0\EDM.Tests.dll"
if (-not (Test-Path $testDll)) {
    Write-Host "Building EDM.Tests in Release mode..." -ForegroundColor Gray
    dotnet build (Join-Path $RootDir "EDM.slnx") -c Release | Out-Null
}

# 1. UI Telemetry & Speed Limiter Calculations
Write-Host "[1/3] Running DownloadProgressWindowTelemetryTests suite..." -ForegroundColor Yellow
$proc1 = Start-Process -FilePath "dotnet" -ArgumentList "test `"$testDll`" --filter `"FullyQualifiedName~DownloadProgressWindowTelemetryTests`" --no-build" -NoNewWindow -Wait -PassThru
if ($proc1.ExitCode -ne 0) {
    Write-Error "DownloadProgressWindowTelemetryTests failed."
    exit 1
}
Write-Host "-> PASS: Speed limit mappings, chunk stats, and pause token toggles verified." -ForegroundColor Green

# 2. Add-URL Download Progress & Checksum Suite
Write-Host "[2/3] Running AddUrlE2ETests suite..." -ForegroundColor Yellow
$proc2 = Start-Process -FilePath "dotnet" -ArgumentList "test `"$testDll`" --filter `"FullyQualifiedName~AddUrlE2ETests`" --no-build" -NoNewWindow -Wait -PassThru
if ($proc2.ExitCode -ne 0) {
    Write-Error "AddUrlE2ETests failed."
    exit 1
}
Write-Host "-> PASS: Add-URL workflow, progress events, and SHA-256 checksums verified." -ForegroundColor Green

# 3. Core E2E Multi-Segment & Stress Suite (12 Scenarios)
Write-Host "[3/3] Running DownloadE2ETests suite (12/12 tests including 32 segments & stress storms)..." -ForegroundColor Yellow
$proc3 = Start-Process -FilePath "dotnet" -ArgumentList "test `"$testDll`" --filter `"FullyQualifiedName~DownloadE2ETests`" --no-build" -NoNewWindow -Wait -PassThru
if ($proc3.ExitCode -ne 0) {
    Write-Error "DownloadE2ETests suite failed."
    exit 1
}
Write-Host "-> PASS: 32 segments, concurrent simultaneous downloads, pause/resume storms, and dynamic throttling verified." -ForegroundColor Green

Write-Host "=================================================================" -ForegroundColor Green
Write-Host " ALL DOWNLOAD PROGRESS & STRESS TESTS PASSED [IDM-GRADE CERTIFIED]" -ForegroundColor Green
Write-Host "=================================================================" -ForegroundColor Green
exit 0
