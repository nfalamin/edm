# PackageFirefoxExtension.ps1 - Package Firefox WebExtension zip for Firefox Add-ons (AMO) upload
$ErrorActionPreference = "Stop"

Write-Host "=== Packaging EDM Firefox WebExtension ===" -ForegroundColor Cyan

$workspaceRoot = Split-Path -Parent $PSScriptRoot
$extensionDir  = Join-Path $workspaceRoot "extension\firefox"
$outputDir     = Join-Path $workspaceRoot "Output"

if (-not (Test-Path $outputDir)) { New-Item -ItemType Directory -Path $outputDir -Force | Out-Null }
if (-not (Test-Path $extensionDir)) { New-Item -ItemType Directory -Path $extensionDir -Force | Out-Null }

# Ensure manifest.json exists
$manifestPath = Join-Path $extensionDir "manifest.json"
if (-not (Test-Path $manifestPath)) {
    $manifestObj = @{
        manifest_version = 2
        name = "Exclusive Download Manager (EDM) Firefox Extension"
        version = "1.0.0"
        description = "High-speed multi-threaded browser download interception for EDM"
        permissions = @("downloads", "nativeMessaging")
        background = @{
            scripts = @("background.js")
        }
        browser_specific_settings = @{
            gecko = @{
                id = "edm-extension@edm.app"
                strict_min_version = "109.0"
            }
        }
    }
    $manifestObj | ConvertTo-Json -Depth 5 | Set-Content -Path $manifestPath -Encoding UTF8
}

# Ensure background.js exists
$bgPath = Join-Path $extensionDir "background.js"
if (-not (Test-Path $bgPath)) {
    $bgCode = @"
// EDM Firefox WebExtension Background Script
browser.downloads.onCreated.addListener((downloadItem) => {
    browser.runtime.sendNativeMessage('com.edm.downloader', {
        action: 'intercept',
        url: downloadItem.url,
        filename: downloadItem.filename
    }).then((response) => {
        if (response && response.status === 'handed_off') {
            browser.downloads.cancel(downloadItem.id);
        }
    });
});
"@
    Set-Content -Path $bgPath -Value $bgCode -Encoding UTF8
}

$zipPath = Join-Path $outputDir "EDM_Firefox_Extension_v1.0.0.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Compress-Archive -Path "$extensionDir\*" -DestinationPath $zipPath -Force
$zipHash = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash

Write-Host "[🟢 SUCCESS] Created Firefox Extension Zip Package:" -ForegroundColor Green
Write-Host "File Path: $zipPath"
Write-Host "File Size: $((Get-Item $zipPath).Length) bytes"
Write-Host "SHA256:    $zipHash"
