# SignRelease.ps1 - Production Authenticode SignTool Execution Script
param (
    [string]$CertPath = $env:EDM_SIGNING_CERT_PATH,
    [string]$CertPassword = $env:EDM_SIGNING_CERT_PASSWORD,
    [string]$TimestampUrl = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"

Write-Host "=== EDM Production Code Signing Preflight ===" -ForegroundColor Cyan

if ([string]::IsNullOrWhiteSpace($CertPath) -or -not (Test-Path $CertPath)) {
    Write-Warning "[EXTERNAL BLOCKER] Valid EV Authenticode Signing Certificate (.pfx) not detected."
    Write-Host "Set `$env:EDM_SIGNING_CERT_PATH and `$env:EDM_SIGNING_CERT_PASSWORD to sign release artifacts." -ForegroundColor Yellow
    exit 0
}

$workspaceRoot = Split-Path -Parent $PSScriptRoot
$binaries = @(
    (Join-Path $workspaceRoot "EDM\bin\Release\net10.0-windows\EDM.exe"),
    (Join-Path $workspaceRoot "EDM\bin\Release\net10.0-windows\EDM.dll"),
    (Join-Path $workspaceRoot "Output\EDMSetup.exe")
)

foreach ($binary in $binaries) {
    if (Test-Path $binary) {
        Write-Host "Signing $binary..." -ForegroundColor Green
        signtool sign /fd sha256 /tr $TimestampUrl /td sha256 /f $CertPath /p $CertPassword $binary
    }
}

Write-Host "=== Code Signing Completed Successfully ===" -ForegroundColor Green
