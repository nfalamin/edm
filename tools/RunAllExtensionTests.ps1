# EDM Extension - Master Qualification Test Runner
# Version: 1.0.0
# Runs all 9 test suites across unit, IPC, native messaging, video detection, download pipeline, adaptive engine, UI, and real browser E2E.

$ErrorActionPreference = "Stop"

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host " EDM EXTENSION 1.0.0 - MASTER RELEASE QUALIFICATION SUITE        " -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

$suites = @(
    @{ Name = "[1/9] UI & Design System Live Tests"; Cmd = "node tools/RunUILiveTests.js" },
    @{ Name = "[2/9] Media Extractors & Cipher Live Tests"; Cmd = "node tools/RunExtractorPipelineLiveTests.js" },
    @{ Name = "[3/9] Adaptive Media Engine Live Tests"; Cmd = "node tools/RunAdaptivePipelineLiveTests.js" },
    @{ Name = "[4/9] Download Pipeline & Lifecycle Live Tests"; Cmd = "node tools/RunDownloadPipelineLiveTests.js" },
    @{ Name = "[5/9] Format Discovery & Quality Enumeration Tests"; Cmd = "node tools/RunFormatDiscoveryLiveTests.js" },
    @{ Name = "[6/9] Browser Manifest & Installer Verification"; Cmd = "powershell.exe -ExecutionPolicy Bypass -File tools/TestBrowserIntegration.ps1" },
    @{ Name = "[7/9] Native Messaging Stdio Framing & IPC"; Cmd = "powershell.exe -ExecutionPolicy Bypass -File tools/TestNativeMessaging.ps1" },
    @{ Name = "[8/9] Real Video Detection & HLS/DASH Resolver E2E"; Cmd = "powershell.exe -ExecutionPolicy Bypass -File tools/TestVideoDetectionE2E.ps1" },
    @{ Name = "[9/9] Headless Chromium Real Browser Handoff E2E"; Cmd = "powershell.exe -ExecutionPolicy Bypass -File tools/TestRealBrowserIntegrationE2E.ps1" }
)

$passed = 0
$failed = 0

foreach ($suite in $suites) {
    Write-Host "`n>>> Running $($suite.Name)..." -ForegroundColor Yellow
    try {
        Invoke-Expression $suite.Cmd
        if ($LASTEXITCODE -eq 0) {
            Write-Host "--> $($suite.Name): PASSED" -ForegroundColor Green
            $passed++
        } else {
            Write-Host "--> $($suite.Name): FAILED (Exit Code $LASTEXITCODE)" -ForegroundColor Red
            $failed++
        }
    } catch {
        Write-Host "--> $($suite.Name): ERROR - $_" -ForegroundColor Red
        $failed++
    }
}

Write-Host "`n=================================================================" -ForegroundColor Cyan
Write-Host " MASTER QUALIFICATION SUMMARY: Passed: $passed | Failed: $failed" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })
Write-Host "=================================================================" -ForegroundColor Cyan

if ($failed -gt 0) {
    exit 1
} else {
    exit 0
}
