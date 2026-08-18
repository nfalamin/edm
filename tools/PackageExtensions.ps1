# EDM Extension - Production Release Packaging Script
# Version: 1.0.0
# Packages Chrome and Firefox extension distributions into clean zip archives under Dist/

$ErrorActionPreference = "Stop"

$baseDir = Split-Path -Parent $PSScriptRoot
$distDir = Join-Path $baseDir "Dist"

if (-not (Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir -Force | Out-Null
}

$chromeSrc = Join-Path $baseDir "extension\chrome"
$firefoxSrc = Join-Path $baseDir "extension\firefox"

$chromeZip = Join-Path $distDir "EDM_Extension_Chrome_v1.0.0.zip"
$firefoxZip = Join-Path $distDir "EDM_Extension_Firefox_v1.0.0.zip"

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host " EDM EXTENSION 1.0.0 - PRODUCTION RELEASE PACKAGING               " -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

# 1. Package Chrome Extension
if (Test-Path $chromeZip) { Remove-Item $chromeZip -Force }
Write-Host "[1/2] Packaging Chromium Distribution ($chromeSrc)..." -ForegroundColor Yellow
Compress-Archive -Path "$chromeSrc\*" -DestinationPath $chromeZip -CompressionLevel Optimal
$chromeSize = (Get-Item $chromeZip).Length / 1KB
Write-Host "-> PASS: Chromium package created at: $chromeZip ($([math]::Round($chromeSize, 2)) KB)" -ForegroundColor Green

# 2. Package Firefox Extension
if (Test-Path $firefoxZip) { Remove-Item $firefoxZip -Force }
Write-Host "[2/2] Packaging Firefox Distribution ($firefoxSrc)..." -ForegroundColor Yellow
Compress-Archive -Path "$firefoxSrc\*" -DestinationPath $firefoxZip -CompressionLevel Optimal
$firefoxSize = (Get-Item $firefoxZip).Length / 1KB
Write-Host "-> PASS: Firefox package created at: $firefoxZip ($([math]::Round($firefoxSize, 2)) KB)" -ForegroundColor Green

Write-Host "=================================================================" -ForegroundColor Green
Write-Host " PRODUCTION PACKAGING COMPLETED SUCCESSFULLY                      " -ForegroundColor Green
Write-Host "=================================================================" -ForegroundColor Green
