# InstallNativeHost.ps1 - Registers EDM Native Messaging Host in Windows Registry for Chrome & Edge

$ErrorActionPreference = "Stop"

$ExePath = "D:\Project 2\5 AUG\EDM\EDM\bin\Debug\net10.0-windows\EDM.exe"
$ManifestPath = "D:\Project 2\5 AUG\EDM\tools\native-host\com.edm.downloader.json"

Write-Host "Registering EDM Native Messaging Host..." -ForegroundColor Cyan

if (-not (Test-Path $ExePath)) {
    Write-Host "Warning: EDM.exe executable not found at $ExePath. Build the project first." -ForegroundColor Yellow
}

# 1. Update manifest JSON path to point to absolute EDM.exe path
$ManifestJson = @{
    name = "com.edm.downloader"
    description = "Exclusive Download Manager Native Host Messaging Agent"
    path = $ExePath
    type = "stdio"
    allowed_origins = @(
        "chrome-extension://*",
        "extension://*"
    )
} | ConvertTo-Json -Depth 4

Set-Content -Path $ManifestPath -Value $ManifestJson -Encoding UTF8
Write-Host "[OK] Updated Native Host Manifest at $ManifestPath" -ForegroundColor Green

# 2. Register in Windows Registry for Chrome
$ChromeRegPath = "HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.edm.downloader"
if (-not (Test-Path $ChromeRegPath)) {
    New-Item -Path $ChromeRegPath -Force | Out-Null
}
Set-ItemProperty -Path $ChromeRegPath -Name "(default)" -Value $ManifestPath
Write-Host "[OK] Registered Native Host for Google Chrome" -ForegroundColor Green

# 3. Register in Windows Registry for Edge
$EdgeRegPath = "HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\com.edm.downloader"
if (-not (Test-Path $EdgeRegPath)) {
    New-Item -Path $EdgeRegPath -Force | Out-Null
}
Set-ItemProperty -Path $EdgeRegPath -Name "(default)" -Value $ManifestPath
Write-Host "[OK] Registered Native Host for Microsoft Edge" -ForegroundColor Green

Write-Host "`nSUCCESS: EDM Extension Native Messaging Host successfully installed!" -ForegroundColor Cyan
