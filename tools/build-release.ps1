<#
.SYNOPSIS
    Automated Master Release Packaging Script for Exclusive Download Manager (EDM)
.DESCRIPTION
    Builds the C# WPF Application in Release configuration, packages Chrome & Firefox extensions with PNG icons,
    compiles the modern Inno Setup installer (EDM_Setup.exe), and creates the standalone Portable ZIP archive.
#>

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = "D:\Project 2\10 AUG - 2.07AM\5 AUG\EDM"
$projectPath = Join-Path $root "EDM\EDM.csproj"
$outputDir = Join-Path $root "Output"
$innoCompiler = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  🚀 BUILDING EXCLUSIVE DOWNLOAD MANAGER (EDM) RELEASE" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

# Step 1: Build C# WPF Project
Write-Host "`n[Step 1/5] Compiling C# WPF Application ($Configuration)..." -ForegroundColor Yellow
dotnet build $projectPath -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed!" }
Write-Host "  -> Build succeeded." -ForegroundColor Green

# Step 2: Package Chrome & Firefox Extensions
Write-Host "`n[Step 2/5] Packaging Chrome & Firefox MV3 Extensions..." -ForegroundColor Yellow
& powershell -ExecutionPolicy Bypass -File (Join-Path $root "tools\package-extension.ps1")
Write-Host "  -> Extensions packaged." -ForegroundColor Green

# Step 3: Build Modern Inno Setup Installer
Write-Host "`n[Step 3/5] Compiling Modern Inno Setup Installer (EDM_Setup.exe)..." -ForegroundColor Yellow
if (-not (Test-Path $innoCompiler)) {
    throw "Inno Setup Compiler not found at: $innoCompiler"
}
& $innoCompiler (Join-Path $root "EDMSetup.iss")
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed!" }
Write-Host "  -> EDM_Setup.exe generated." -ForegroundColor Green

# Step 4: Create Standalone Portable Archive
Write-Host "`n[Step 4/5] Creating Standalone Portable ZIP..." -ForegroundColor Yellow
$portableZip = Join-Path $outputDir "EDM-v1.0.0-Portable.zip"
if (Test-Path $portableZip) { Remove-Item $portableZip -Force }
Compress-Archive -Path (Join-Path $root "EDM\bin\Release\net10.0-windows\*"), (Join-Path $root "extension") -DestinationPath $portableZip -Force
Write-Host "  -> Portable ZIP created: $portableZip" -ForegroundColor Green

# Step 5: Generate Cryptographic SHA256 Checksums
Write-Host "`n[Step 5/5] Generating SHA256 Checksums for Release Assets..." -ForegroundColor Yellow
$hashes = Get-FileHash -Path "$outputDir\*" -Algorithm SHA256
$hashes | Format-Table -AutoSize

Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "  ✅ ALL RELEASE ARTIFACTS PRODUCED SUCCESSFULLY IN OUTPUT!" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Cyan
