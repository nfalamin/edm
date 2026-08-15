# PackageChromeExtension.ps1 - Package Chrome WebExtension Manifest V3 zip for Chrome Web Store upload
$ErrorActionPreference = "Stop"

Write-Host "=== Packaging EDM Chrome WebExtension (Manifest V3) ===" -ForegroundColor Cyan

$workspaceRoot = Split-Path -Parent $PSScriptRoot
$extensionDir  = Join-Path $workspaceRoot "extension\chrome"
$outputDir     = Join-Path $workspaceRoot "Output"

if (-not (Test-Path $outputDir)) { New-Item -ItemType Directory -Path $outputDir -Force | Out-Null }
if (-not (Test-Path $extensionDir)) { New-Item -ItemType Directory -Path $extensionDir -Force | Out-Null }

# Ensure manifest.json exists
$manifestPath = Join-Path $extensionDir "manifest.json"
if (-not (Test-Path $manifestPath)) {
    $manifestObj = @{
        manifest_version = 3
        name = "Exclusive Download Manager (EDM) Extension"
        version = "1.0.0"
        description = "High-speed multi-threaded browser download interception for EDM"
        permissions = @("downloads", "nativeMessaging")
        background = @{
            service_worker = "background.js"
        }
        icons = @{
            "16" = "icon16.png"
            "48" = "icon48.png"
            "128" = "icon128.png"
        }
    }
    $manifestObj | ConvertTo-Json -Depth 5 | Set-Content -Path $manifestPath -Encoding UTF8
}

# Ensure background.js exists
$bgPath = Join-Path $extensionDir "background.js"
if (-not (Test-Path $bgPath)) {
    $bgCode = @"
// EDM Chrome WebExtension Service Worker
chrome.downloads.onCreated.addListener((downloadItem) => {
    chrome.runtime.sendNativeMessage('com.edm.downloader', {
        action: 'intercept',
        url: downloadItem.url,
        filename: downloadItem.filename
    }, (response) => {
        if (response && response.status === 'handed_off') {
            chrome.downloads.cancel(downloadItem.id);
        }
    });
});
"@
    Set-Content -Path $bgPath -Value $bgCode -Encoding UTF8
}

$zipPath = Join-Path $outputDir "EDM_Chrome_Extension_v1.0.0.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Compress-Archive -Path "$extensionDir\*" -DestinationPath $zipPath -Force
$zipHash = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash

Write-Host "[🟢 SUCCESS] Created Chrome Extension Zip Package:" -ForegroundColor Green
Write-Host "File Path: $zipPath"
Write-Host "File Size: $((Get-Item $zipPath).Length) bytes"
Write-Host "SHA256:    $zipHash"
