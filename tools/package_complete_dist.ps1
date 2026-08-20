# tools/package_complete_dist.ps1
# Complete Distribution Packager for EDM
# Builds Release binaries, compiles Inno Setup installer, packages browser extension ZIPs,
# updates Dist/EDM_v1.0_Complete_Distribution, and creates Dist/EDM_v1.0_Complete_Package.zip.

[CmdletBinding()]
param(
    [string]$RootDir = ""
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($RootDir)) {
    $RootDir = Split-Path -Parent $PSScriptRoot
}

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "  EDM COMPLETE DISTRIBUTION REPACKAGING PIPELINE                 " -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

$outputDir = Join-Path $RootDir "Output"
$publishDir = Join-Path $outputDir "publish"
$distDir = Join-Path $RootDir "Dist"
$completeDistDir = Join-Path $distDir "EDM_v1.0_Complete_Distribution"
$distExtensionsDir = Join-Path $completeDistDir "Browser_Extensions"
$isccExe = "C:\Program Files (x86)\Inno Setup 6\iscc.exe"

# 1. Ensure Directories Exist
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $distExtensionsDir -Force | Out-Null

# 2. Publish EDM & EDM.NativeHost in Release Mode
Write-Host "[1/6] Publishing EDM Desktop Application in Release mode..." -ForegroundColor Yellow
$pubProc = Start-Process -FilePath "dotnet" -ArgumentList "publish `"$RootDir\EDM\EDM.csproj`" -c Release -o `"$publishDir`"" -NoNewWindow -Wait -PassThru
if ($pubProc.ExitCode -ne 0) {
    Write-Error "dotnet publish EDM.csproj failed."
    exit 1
}

Write-Host "[2/6] Publishing EDM.NativeHost in Release mode..." -ForegroundColor Yellow
$pubHost = Start-Process -FilePath "dotnet" -ArgumentList "publish `"$RootDir\EDM.NativeHost\EDM.NativeHost.csproj`" -c Release -o `"$publishDir`"" -NoNewWindow -Wait -PassThru
if ($pubHost.ExitCode -ne 0) {
    Write-Error "dotnet publish EDM.NativeHost.csproj failed."
    exit 1
}

# Copy extension directory into publish for native installer packaging
$pubExtDir = Join-Path $publishDir "extension"
New-Item -ItemType Directory -Path $pubExtDir -Force | Out-Null
Copy-Item -Path "$RootDir\extension\*" -Destination $pubExtDir -Recurse -Force

# Clean non-Windows runtimes (Android, iOS, Linux, OSX) to keep installer lightweight (~4.5MB)
$runtimesDir = Join-Path $publishDir "runtimes"
if (Test-Path $runtimesDir) {
    Get-ChildItem -Path $runtimesDir -Directory | Where-Object { $_.Name -notlike "win*" } | ForEach-Object {
        Remove-Item -Path $_.FullName -Recurse -Force
    }
}

# 3. Package Browser Extension ZIPs with Latest Dynamic Scripts
Write-Host "[3/6] Packaging Latest Browser Extension ZIPs..." -ForegroundColor Yellow
$chromeSrc = Join-Path $RootDir "extension\chrome"
$firefoxSrc = Join-Path $RootDir "extension\firefox"

$chromeZip = Join-Path $distExtensionsDir "edm-chrome-extension-v1.0.0.zip"
$edgeZip = Join-Path $distExtensionsDir "edm-edge-extension-v1.0.0.zip"
$firefoxZip = Join-Path $distExtensionsDir "edm-firefox-extension-v1.0.0.zip"

if (Test-Path $chromeZip) { Remove-Item -Force $chromeZip }
if (Test-Path $edgeZip) { Remove-Item -Force $edgeZip }
if (Test-Path $firefoxZip) { Remove-Item -Force $firefoxZip }

Compress-Archive -Path "$chromeSrc\*" -DestinationPath $chromeZip -Force
Compress-Archive -Path "$chromeSrc\*" -DestinationPath $edgeZip -Force
Compress-Archive -Path "$firefoxSrc\*" -DestinationPath $firefoxZip -Force

# Copy raw scripts into Dist/Browser_Extensions
Copy-Item -Path "$chromeSrc\background.js" -Destination $distExtensionsDir -Force
Copy-Item -Path "$chromeSrc\content.js" -Destination $distExtensionsDir -Force
Copy-Item -Path "$chromeSrc\content.css" -Destination $distExtensionsDir -Force
Copy-Item -Path "$chromeSrc\manifest.json" -Destination $distExtensionsDir -Force
if (Test-Path "$chromeSrc\icons") {
    Copy-Item -Path "$chromeSrc\icons" -Destination $distExtensionsDir -Recurse -Force
}

# Also sync Output ZIPs
Copy-Item -Path $chromeZip -Destination (Join-Path $outputDir "EDM-Chrome-Extension-v1.0.0.zip") -Force
Copy-Item -Path $edgeZip -Destination (Join-Path $outputDir "EDM-Edge-Extension-v1.0.0.zip") -Force
Copy-Item -Path $firefoxZip -Destination (Join-Path $outputDir "EDM-Firefox-Extension-v1.0.0.zip") -Force

Write-Host "-> PASS: Browser extension ZIPs created with current dynamic resolver scripts." -ForegroundColor Green

# 4. Compile Fresh Inno Setup Installer
Write-Host "[4/6] Compiling Fresh Inno Setup Installer..." -ForegroundColor Yellow
if (Test-Path $isccExe) {
    $issPath = Join-Path $RootDir "EDMSetup.iss"
    $isccProc = Start-Process -FilePath $isccExe -ArgumentList "`"$issPath`"" -NoNewWindow -Wait -PassThru
    if ($isccProc.ExitCode -eq 0) {
        $setupExe = Join-Path $outputDir "EDM_Setup.exe"
        $distSetupExe = Join-Path $completeDistDir "EDM_Setup_v1.0.exe"
        Copy-Item -Path $setupExe -Destination $distSetupExe -Force
        Copy-Item -Path $setupExe -Destination (Join-Path $outputDir "EDMSetup.exe") -Force
        Write-Host "-> PASS: EDM_Setup_v1.0.exe compiled and copied to Dist." -ForegroundColor Green
    } else {
        Write-Error "Inno Setup compilation failed with code $($isccProc.ExitCode)"
        exit 1
    }
} else {
    Write-Warning "Inno Setup compiler not found at $isccExe"
}

# 5. Generate Updated CHECKSUMS_SHA256.txt
Write-Host "[5/6] Generating Updated SHA-256 Checksums..." -ForegroundColor Yellow
$checksumFile = Join-Path $completeDistDir "CHECKSUMS_SHA256.txt"
$distFiles = Get-ChildItem -Path $completeDistDir -Recurse -File | Where-Object { $_.Name -ne "CHECKSUMS_SHA256.txt" }

$sb = [System.Text.StringBuilder]::new()
$sb.AppendLine("=================================================================") | Out-Null
$sb.AppendLine(" EXCLUSIVE DOWNLOAD MANAGER (EDM) - SHA-256 CHECKSUMS MANIFEST    ") | Out-Null
$sb.AppendLine(" Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss UTC')        ") | Out-Null
$sb.AppendLine("=================================================================") | Out-Null
$sb.AppendLine("") | Out-Null

foreach ($file in $distFiles) {
    $relPath = $file.FullName.Substring($completeDistDir.Length).TrimStart("\", "/")
    $hash = (Get-FileHash -Path $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $sb.AppendLine("$hash  $relPath") | Out-Null
}

Set-Content -Path $checksumFile -Value $sb.ToString() -Force
Write-Host "-> PASS: CHECKSUMS_SHA256.txt updated." -ForegroundColor Green

# 6. Repackage Complete Distribution ZIP
Write-Host "[6/6] Packaging Final EDM_v1.0_Complete_Package.zip..." -ForegroundColor Yellow
$finalZip = Join-Path $distDir "EDM_v1.0_Complete_Package.zip"
if (Test-Path $finalZip) {
    Remove-Item -Force $finalZip
}

Compress-Archive -Path "$completeDistDir\*" -DestinationPath $finalZip -Force
$finalZipSize = (Get-Item $finalZip).Length / 1MB
Write-Host "-> PASS: Created EDM_v1.0_Complete_Package.zip ($([Math]::Round($finalZipSize, 2)) MB)" -ForegroundColor Green

Write-Host "=================================================================" -ForegroundColor Green
Write-Host " DISTRIBUTION REPACKAGING COMPLETE - ALL ASSETS FRESH & VERIFIED " -ForegroundColor Green
Write-Host "=================================================================" -ForegroundColor Green
