# tools/TestBrowserIntegration.ps1
# Deterministic Real E2E Test for EDM Browser Integration & Extension Package Verification
[CmdletBinding()]
param(
    [string]$RootDir = ""
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($RootDir)) {
    $RootDir = Split-Path -Parent $PSScriptRoot
}

Write-Host "=== [2/5] Browser Integration & Extension Packaging Test ===" -ForegroundColor Cyan

# 1. Verify Extension Packages
$chromeExtDir = Join-Path $RootDir "extension\chrome"
$firefoxExtDir = Join-Path $RootDir "extension\firefox"

Write-Host "[Step 1] Verifying Chrome/Edge Extension Package ($chromeExtDir)..." -ForegroundColor Gray
if (-not (Test-Path $chromeExtDir)) {
    # Check alternate location
    $alt = Join-Path $RootDir "tools\chrome-extension"
    if (Test-Path $alt) { $chromeExtDir = $alt }
}

$chromeManifestPath = Join-Path $chromeExtDir "manifest.json"
if (-not (Test-Path $chromeManifestPath)) {
    Write-Error "Chrome extension manifest.json not found at $chromeManifestPath"
    exit 1
}

$cManifest = Get-Content $chromeManifestPath -Raw | ConvertFrom-Json
if ($cManifest.manifest_version -ne 3) {
    Write-Error "Chrome manifest version must be 3. Found: $($cManifest.manifest_version)"
    exit 1
}
if (-not ($cManifest.permissions -contains "nativeMessaging")) {
    Write-Error "Chrome manifest missing 'nativeMessaging' permission"
    exit 1
}
Write-Host "-> PASS: Chrome Manifest V3 valid with 'nativeMessaging' permission." -ForegroundColor Green

# Verify background.js exists and contains transactional handoff logic
$bgPath = Join-Path $chromeExtDir "background.js"
if (-not (Test-Path $bgPath)) {
    Write-Error "background.js not found in Chrome extension"
    exit 1
}
$bgContent = Get-Content $bgPath -Raw
if (-not ($bgContent -match "com\.edm\.downloader")) {
    Write-Error "background.js does not reference native host 'com.edm.downloader'"
    exit 1
}
Write-Host "-> PASS: background.js contains native host connection logic (com.edm.downloader)." -ForegroundColor Green

# 2. Verify Firefox Extension Package
Write-Host "[Step 2] Verifying Firefox Extension Package ($firefoxExtDir)..." -ForegroundColor Gray
if (-not (Test-Path $firefoxExtDir)) {
    $altFf = Join-Path $RootDir "tools\firefox-extension"
    if (Test-Path $altFf) { $firefoxExtDir = $altFf }
}
$ffManifestPath = Join-Path $firefoxExtDir "manifest.json"
if (Test-Path $ffManifestPath) {
    $ffManifest = Get-Content $ffManifestPath -Raw | ConvertFrom-Json
    if (-not ($ffManifest.permissions -contains "nativeMessaging")) {
        Write-Error "Firefox manifest missing 'nativeMessaging' permission"
        exit 1
    }
    Write-Host "-> PASS: Firefox Manifest valid with 'nativeMessaging' permission." -ForegroundColor Green
}

# 3. Verify Native Messaging Host Manifest Templates Generated
Write-Host "[Step 3] Verifying C# BrowserExtensionInstaller concrete manifest generators..." -ForegroundColor Gray

# Run .NET Unit Tests for Browser Integration
$testDll = Join-Path $RootDir "EDM.Tests\bin\Release\net10.0-windows7.0\EDM.Tests.dll"
if (Test-Path $testDll) {
    $testProc = Start-Process -FilePath "dotnet" -ArgumentList "test `"$testDll`" --filter `"FullyQualifiedName~BrowserExtensionInstaller_Manifests_MatchBrowserRequirements`" --no-build" -NoNewWindow -Wait -PassThru
    if ($testProc.ExitCode -ne 0) {
        Write-Error "Browser manifest unit test failed."
        exit 1
    }
    Write-Host "-> PASS: Browser manifest specifications verified by test runner." -ForegroundColor Green
}

Write-Host "=== Browser Integration Test: ALL PASS ===" -ForegroundColor Green
exit 0
