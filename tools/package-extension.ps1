<#
.SYNOPSIS
    Packages EDM Chrome & Firefox Extensions into clean unpacked folders and .zip archives.
.DESCRIPTION
    1. Generates crisp PNG icon assets (16, 32, 48, 128).
    2. Packages Output/chrome-extension and Output/firefox-extension ready for "Load unpacked".
    3. Archives both into versioned .zip files in Output directory.
#>

$root = "D:\Project 2\10 AUG - 2.07AM\5 AUG\EDM"
$chromeSrc = Join-Path $root "extension\chrome"
$firefoxSrc = Join-Path $root "extension\firefox"
$outputDir = Join-Path $root "Output"
$chromeUnpacked = Join-Path $outputDir "chrome-extension"
$firefoxUnpacked = Join-Path $outputDir "firefox-extension"

# 1. Ensure icons exist
& powershell -ExecutionPolicy Bypass -File (Join-Path $root "tools\generate-extension-icons.ps1")

# 2. Prepare Output directories
if (-not (Test-Path $outputDir)) { New-Item -ItemType Directory -Path $outputDir -Force | Out-Null }
if (Test-Path $chromeUnpacked) { Remove-Item -Path $chromeUnpacked -Recurse -Force }
if (Test-Path $firefoxUnpacked) { Remove-Item -Path $firefoxUnpacked -Recurse -Force }

# 3. Copy clean Chrome extension
New-Item -ItemType Directory -Path $chromeUnpacked -Force | Out-Null
Copy-Item -Path "$chromeSrc\*" -Destination $chromeUnpacked -Recurse -Force
Write-Host "Created Chrome unpacked folder: $chromeUnpacked" -ForegroundColor Green

# 4. Copy clean Firefox extension
New-Item -ItemType Directory -Path $firefoxUnpacked -Force | Out-Null
Copy-Item -Path "$firefoxSrc\*" -Destination $firefoxUnpacked -Recurse -Force
Write-Host "Created Firefox unpacked folder: $firefoxUnpacked" -ForegroundColor Green

# 5. Build ZIP archives
$chromeZip = Join-Path $outputDir "EDM_Chrome_Extension_v1.0.0.zip"
$firefoxZip = Join-Path $outputDir "EDM_Firefox_Extension_v1.0.0.zip"

if (Test-Path $chromeZip) { Remove-Item -Path $chromeZip -Force }
if (Test-Path $firefoxZip) { Remove-Item -Path $firefoxZip -Force }

Compress-Archive -Path "$chromeUnpacked\*" -DestinationPath $chromeZip -Force
Write-Host "Created Chrome Zip Archive: $chromeZip" -ForegroundColor Cyan

Compress-Archive -Path "$firefoxUnpacked\*" -DestinationPath $firefoxZip -Force
Write-Host "Created Firefox Zip Archive: $firefoxZip" -ForegroundColor Cyan

Write-Host "`nAll extension packages built successfully!" -ForegroundColor Green
