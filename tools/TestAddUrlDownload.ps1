# tools/TestAddUrlDownload.ps1
# Deterministic Real E2E Test for EDM Add-URL Download Pipeline (URL normalization, Progress, SHA-256 verification)
[CmdletBinding()]
param(
    [string]$RootDir = ""
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($RootDir)) {
    $RootDir = Split-Path -Parent $PSScriptRoot
}

Write-Host "=== [3/5] Add-URL End-to-End Download Pipeline Test ===" -ForegroundColor Cyan

$testDll = Join-Path $RootDir "EDM.Tests\bin\Release\net10.0-windows7.0\EDM.Tests.dll"
if (-not (Test-Path $testDll)) {
    Write-Host "Building EDM.Tests in Release mode..." -ForegroundColor Yellow
    dotnet build (Join-Path $RootDir "EDM.slnx") -c Release | Out-Null
}

Write-Host "[Step 1] Running AddUrlE2ETests suite (Workflow Execution, Normalization, SHA256 integrity)..." -ForegroundColor Gray
$testProc = Start-Process -FilePath "dotnet" -ArgumentList "test `"$testDll`" --filter `"FullyQualifiedName~AddUrlE2ETests`" --no-build" -NoNewWindow -Wait -PassThru

if ($testProc.ExitCode -ne 0) {
    Write-Error "AddUrlE2ETests execution failed."
    exit 1
}

Write-Host "-> PASS: Add-URL real pipeline verified with cryptographic checksum validation." -ForegroundColor Green
Write-Host "=== Add-URL Download Test: ALL PASS ===" -ForegroundColor Green
exit 0
