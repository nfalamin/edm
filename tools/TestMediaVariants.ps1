# tools/TestMediaVariants.ps1
# Deterministic Real E2E Test for EDM Floating Video Variant Resolver (HLS/DASH/Direct Stream extraction)
[CmdletBinding()]
param(
    [string]$RootDir = ""
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($RootDir)) {
    $RootDir = Split-Path -Parent $PSScriptRoot
}

Write-Host "=== [4/5] Floating Video & Media Variant Resolver Test ===" -ForegroundColor Cyan

$testDll = Join-Path $RootDir "EDM.Tests\bin\Release\net10.0-windows7.0\EDM.Tests.dll"
if (-not (Test-Path $testDll)) {
    Write-Host "Building EDM.Tests in Release mode..." -ForegroundColor Yellow
    dotnet build (Join-Path $RootDir "EDM.slnx") -c Release | Out-Null
}

Write-Host "[Step 1] Running MediaVariantE2ETests suite (HLS Master Playlist resolution, Direct stream probing)..." -ForegroundColor Gray
$testProc = Start-Process -FilePath "dotnet" -ArgumentList "test `"$testDll`" --filter `"FullyQualifiedName~MediaVariantE2ETests`" --no-build" -NoNewWindow -Wait -PassThru

if ($testProc.ExitCode -ne 0) {
    Write-Error "MediaVariantE2ETests execution failed."
    exit 1
}

Write-Host "-> PASS: Media Variant resolver successfully parses adaptive streams and direct video formats." -ForegroundColor Green
Write-Host "=== Media Variants Test: ALL PASS ===" -ForegroundColor Green
exit 0
